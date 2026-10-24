using EFCoreTerminalUI.Localization;
using Spectre.Console;

namespace EFCoreTerminalUI;

public sealed class App(string root, UserConfig userConfig)
{
    private Settings _settings = Settings.Load(root);
    private EfTool _ef = null!;
    private List<MigrationInfo> _migrations = [];
    private bool _dbConnected;

    public async Task<int> RunAsync()
    {
        Header();

        if (!await EnsureEfInstalledAsync()) return 1;
        if (!await ConfigureAsync(forceAsk: false)) return 1;

        await RefreshAsync(connect: true);

        while (true)
        {
            Header();
            RenderStatus();

            var choice = AnsiConsole.Prompt(
                Select<MenuItem>(L.T.MenuPrompt)
                    .PageSize(12)
                    .UseConverter(MenuText)
                    .AddChoices(Enum.GetValues<MenuItem>()));

            switch (choice)
            {
                case MenuItem.Add: await AddMigrationAsync(); break;
                case MenuItem.Remove: await RemoveMigrationAsync(); break;
                case MenuItem.UpdateLatest: await UpdateDatabaseAsync(null); break;
                case MenuItem.UpdateTo: await UpdateToSelectedAsync(); break;
                case MenuItem.Script: await ScriptAsync(); break;
                case MenuItem.Refresh: await RefreshAsync(connect: true); break;
                case MenuItem.Settings: await SettingsMenuAsync(); break;
                case MenuItem.Drop: await DropDatabaseAsync(); break;
                case MenuItem.Exit: return 0;
            }
        }
    }

    private enum MenuItem { Add, Remove, UpdateLatest, UpdateTo, Script, Refresh, Settings, Drop, Exit }

    private static string MenuText(MenuItem item) => item switch
    {
        MenuItem.Add => $"[green]+[/] {L.T.MenuAdd}",
        MenuItem.Remove => $"[red]-[/] {L.T.MenuRemove}",
        MenuItem.UpdateLatest => $"[deepskyblue1]↑[/] {L.T.MenuUpdate} [grey]{L.T.MenuUpdateLatestHint}[/]",
        MenuItem.UpdateTo => $"[deepskyblue1]→[/] {L.T.MenuUpdate} [grey]{L.T.MenuUpdateToHint}[/]",
        MenuItem.Script => $"[grey]#[/] {L.T.MenuScript}",
        MenuItem.Refresh => $"[grey]↻[/] {L.T.MenuRefresh}",
        MenuItem.Settings => $"[grey]*[/] {L.T.MenuSettings}",
        MenuItem.Drop => $"[red]![/] {L.T.MenuDrop}",
        MenuItem.Exit => $"[grey]x[/] {L.T.MenuExit}",
        _ => item.ToString()
    };

    // ───────────────────────────── Ayarlar ─────────────────────────────

    private enum SettingsItem { Language, Projects, Back }

    private async Task SettingsMenuAsync()
    {
        while (true)
        {
            Header();
            var choice = AnsiConsole.Prompt(
                Select<SettingsItem>(L.T.SettingsTitle)
                    .UseConverter(item => item switch
                    {
                        SettingsItem.Language => $"{L.T.SettingsLanguage} [grey]({Markup.Escape(L.Current.NativeName)})[/]",
                        SettingsItem.Projects => L.T.SettingsProjects,
                        _ => L.T.Back
                    })
                    .AddChoices(Enum.GetValues<SettingsItem>()));

            switch (choice)
            {
                case SettingsItem.Language:
                    PickLanguage(userConfig);
                    break;
                case SettingsItem.Projects:
                    if (await ConfigureAsync(forceAsk: true)) await RefreshAsync(connect: true);
                    return;
                default:
                    return;
            }
        }
    }

    /// <summary>Dil seçtirir, uygular ve kullanıcı ayarlarına kaydeder.</summary>
    public static void PickLanguage(UserConfig config)
    {
        Header();
        // Etkin dil en üstte
        var ordered = L.Languages.OrderByDescending(l => l == L.Current).ToList();
        var selected = AnsiConsole.Prompt(
            Select<Language>(L.T.PickLanguage)
                .UseConverter(l => $"{Markup.Escape(l.NativeName)} [grey]{l.Code}[/]")
                .AddChoices(ordered));

        L.Set(selected);
        config.Language = selected.Code;
        try { config.Save(); }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[yellow]{Format(L.T.SettingsSaveFailed, ex.Message)}[/]"); }
    }

    // ───────────────────────────── Kurulum ─────────────────────────────

    private static void Header()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold deepskyblue1]EF Core Terminal UI[/]").LeftJustified());
    }

    private static async Task<bool> EnsureEfInstalledAsync()
    {
        var installed = false;
        await AnsiConsole.Status().StartAsync(L.T.CheckingEf, async _ =>
            installed = await EfTool.IsInstalledAsync());
        if (installed) return true;

        AnsiConsole.MarkupLine($"[yellow]{L.T.EfNotFound}[/]");
        if (!Confirm(L.T.EfInstallConfirm)) return false;

        var r = await EfTool.InstallAsync(WriteOutputLine);
        return r.Success;
    }

    private async Task<bool> ConfigureAsync(bool forceAsk)
    {
        var projects = ProjectScanner.Scan(root);
        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine(Format(L.T.NoProjects, root));
            AnsiConsole.MarkupLine($"[grey]{L.T.NoProjectsHint}[/]");
            return false;
        }

        var migration = Find(projects, _settings.MigrationProject);
        var startup = Find(projects, _settings.StartupProject);

        if (forceAsk || migration is null || startup is null)
        {
            if (projects.Count == 1)
            {
                migration = startup = projects[0];
            }
            else
            {
                migration = PickProject(L.T.PickMigrationProject, projects.OrderByDescending(p => p.MigrationScore).ToList());
                startup = PickProject(L.T.PickStartupProject, projects.OrderByDescending(p => p.StartupScore).ToList());
            }
            _settings.DbContext = null;
        }

        _settings.MigrationProject = migration.RelativePath;
        _settings.StartupProject = startup.RelativePath;
        _ef = new EfTool(root, migration.RelativePath, startup.RelativePath, _settings.DbContext);

        if (forceAsk || _settings.DbContext is null)
        {
            if (!await PickDbContextAsync()) return false;
        }

        SaveSettings();
        return true;
    }

    private static ProjectInfo? Find(List<ProjectInfo> projects, string? relativePath) =>
        relativePath is null ? null : projects.FirstOrDefault(p => p.RelativePath == relativePath);

    private static ProjectInfo PickProject(string title, List<ProjectInfo> ordered)
    {
        Header();
        return AnsiConsole.Prompt(
            Select<ProjectInfo>($"{title} [grey]{L.T.SuggestedFirst}[/]")
                .PageSize(15)
                .UseConverter(p => $"{Markup.Escape(p.Name)} [grey]{Markup.Escape(p.RelativePath)}[/]")
                .AddChoices(ordered));
    }

    private async Task<bool> PickDbContextAsync()
    {
        Header();
        JsonCommandResult<List<DbContextInfo>>? result = null;
        await AnsiConsole.Status().StartAsync(L.T.LoadingContexts, async _ =>
            result = await _ef.ListDbContextsAsync());

        var contexts = result!.Data;
        if (contexts is null || contexts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]{(contexts is null ? L.T.ContextListFailed : L.T.NoContexts)}[/]");
            if (contexts is null) PrintErrors(result.Process);
            if (Confirm(L.T.ChangeProjectsConfirm))
                return await ConfigureAsync(forceAsk: true);
            return false;
        }

        var selected = contexts.Count == 1
            ? contexts[0]
            : AnsiConsole.Prompt(
                Select<DbContextInfo>(L.T.PickContext)
                    .UseConverter(c => $"{Markup.Escape(c.Name)} [grey]{Markup.Escape(c.FullName)}[/]")
                    .AddChoices(contexts));

        _settings.DbContext = selected.FullName;
        _ef.DbContext = selected.FullName;
        return true;
    }

    private void SaveSettings()
    {
        try { _settings.Save(root); }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[yellow]{Format(L.T.SettingsSaveFailed, ex.Message)}[/]"); }
    }

    // ───────────────────────────── Liste ─────────────────────────────

    private async Task RefreshAsync(bool connect, bool noBuild = false)
    {
        JsonCommandResult<List<MigrationInfo>>? result = null;
        await AnsiConsole.Status().StartAsync(noBuild ? L.T.ReadingMigrations : L.T.BuildingAndReading, async ctx =>
        {
            result = await _ef.ListMigrationsAsync(connect, noBuild);
            if (result.Data is null && connect)
            {
                // Veritabanına bağlanılamadıysa bağlantısız tekrar dene
                ctx.Status(L.T.DbUnreachableRetry);
                result = await _ef.ListMigrationsAsync(connect: false, noBuild: true);
            }
        });

        if (result!.Data is null)
        {
            _migrations = [];
            _dbConnected = false;
            AnsiConsole.MarkupLine($"[red]{L.T.MigrationListFailed}[/]");
            PrintErrors(result.Process);
            Pause();
            return;
        }

        _migrations = result.Data;
        _dbConnected = _migrations.Count == 0 ? connect : _migrations.Any(m => m.Applied is not null);
    }

    private void RenderStatus()
    {
        var info = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn();
        info.AddRow($"[grey]{L.T.LabelFolder}[/]", Markup.Escape(root));
        info.AddRow($"[grey]{L.T.LabelMigration}[/]", Markup.Escape(_settings.MigrationProject ?? "-"));
        info.AddRow($"[grey]{L.T.LabelStartup}[/]", Markup.Escape(_settings.StartupProject ?? "-"));
        info.AddRow($"[grey]{L.T.LabelContext}[/]", Markup.Escape(_settings.DbContext ?? "-"));
        info.AddRow($"[grey]{L.T.LabelDatabase}[/]", _dbConnected ? $"[green]{L.T.DbConnected}[/]" : $"[yellow]{L.T.DbNotConnected}[/]");
        AnsiConsole.Write(info);
        AnsiConsole.WriteLine();

        if (_migrations.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]{L.T.NoMigrations}[/]\n");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("#");
        table.AddColumn(L.T.ColMigration);
        table.AddColumn(L.T.ColStatus);

        const int maxRows = 10;
        var start = Math.Max(0, _migrations.Count - maxRows);
        if (start > 0) table.AddRow("[grey]…[/]", $"[grey]{Format(L.T.HiddenOlder, start)}[/]", "");

        for (var i = start; i < _migrations.Count; i++)
        {
            var m = _migrations[i];
            var isLast = i == _migrations.Count - 1;
            var name = Markup.Escape(m.Id);
            if (isLast) name = $"[bold deepskyblue1]{name}[/]  [deepskyblue1]{L.T.Latest}[/]";

            var status = m.Applied switch
            {
                true => $"[green]{L.T.Applied}[/]",
                false => $"[yellow]{L.T.Pending}[/]",
                null => "[grey]?[/]"
            };
            table.AddRow((i + 1).ToString(), name, status);
        }

        AnsiConsole.Write(table);

        var pending = _migrations.Count(m => m.Applied == false);
        if (pending > 0) AnsiConsole.MarkupLine($"[yellow]{Format(L.T.PendingCount, pending)}[/]");
        AnsiConsole.WriteLine();
    }

    // ───────────────────────────── İşlemler ─────────────────────────────

    private async Task AddMigrationAsync()
    {
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>(L.T.MigrationNamePrompt)
                .AllowEmpty()
                .Validate(n => n.Length == 0 || n.All(c => char.IsLetterOrDigit(c) || c == '_')
                    ? ValidationResult.Success()
                    : ValidationResult.Error(L.T.MigrationNameInvalid)));
        if (string.IsNullOrWhiteSpace(name)) return;

        var r = await RunWithOutputAsync($"migrations add {name}", on => _ef.AddMigrationAsync(name, null, on));
        if (r.Success && Confirm(L.T.UpdateNowConfirm, defaultValue: false))
        {
            await RunWithOutputAsync("database update", on => _ef.UpdateDatabaseAsync(null, on));
        }
        await RefreshAsync(connect: true, noBuild: true);
    }

    private async Task RemoveMigrationAsync()
    {
        if (_migrations.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{L.T.NothingToRemove}[/]");
            Pause();
            return;
        }

        var last = _migrations[^1];
        var force = false;

        if (last.Applied == true)
        {
            AnsiConsole.MarkupLine(Format(L.T.WarningApplied, last.Id));
            var option = AnsiConsole.Prompt(
                Select<string>(L.T.HowToContinue)
                    .AddChoices(L.T.RevertAndRemove, L.T.Back));
            if (option == L.T.Back) return;
            force = true;
        }
        else if (!Confirm(Format(L.T.RemoveConfirm, last.Id), defaultValue: false))
        {
            return;
        }

        await RunWithOutputAsync("migrations remove", on => _ef.RemoveLastMigrationAsync(force, on));
        await RefreshAsync(connect: true, noBuild: false);
    }

    private async Task UpdateToSelectedAsync()
    {
        var back = L.T.Back;
        var zero = L.T.RevertAllOption;

        var choices = new List<string> { back };
        choices.AddRange(_migrations.AsEnumerable().Reverse().Select(m => m.Id));
        choices.Add(zero);

        var selected = AnsiConsole.Prompt(
            Select<string>(L.T.UpdateTargetPrompt)
                .PageSize(15)
                .UseConverter(id =>
                {
                    var m = _migrations.FirstOrDefault(x => x.Id == id);
                    return m?.Applied switch
                    {
                        true => $"{Markup.Escape(id)} [green]✔[/]",
                        false => $"{Markup.Escape(id)} [yellow]●[/]",
                        _ => Markup.Escape(id)
                    };
                })
                .AddChoices(choices));

        if (selected == back) return;
        var target = selected == zero ? "0" : selected;

        if (target == "0" && !Confirm(L.T.RevertAllConfirm, defaultValue: false))
            return;

        await UpdateDatabaseAsync(target);
    }

    private async Task UpdateDatabaseAsync(string? target)
    {
        await RunWithOutputAsync(target is null ? "database update" : $"database update {target}",
            on => _ef.UpdateDatabaseAsync(target, on));
        await RefreshAsync(connect: true, noBuild: true);
    }

    private async Task ScriptAsync()
    {
        if (_migrations.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{L.T.NoMigrationsForScript}[/]");
            Pause();
            return;
        }

        var fromStart = L.T.FromStart;
        var ids = _migrations.Select(m => m.Id).ToList();

        var from = AnsiConsole.Prompt(Select<string>(L.T.ScriptFromPrompt)
            .PageSize(15)
            .AddChoices([fromStart, .. ids]));

        var toChoices = from == fromStart ? ids : ids.Skip(ids.IndexOf(from) + 1).ToList();
        if (toChoices.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{L.T.AlreadyLatest}[/]");
            Pause();
            return;
        }

        var to = AnsiConsole.Prompt(Select<string>(L.T.ScriptToPrompt)
            .PageSize(15)
            .AddChoices(toChoices.AsEnumerable().Reverse()));

        var idempotent = Confirm(L.T.IdempotentConfirm, defaultValue: false);
        var output = AnsiConsole.Prompt(new TextPrompt<string>(L.T.OutputFilePrompt).DefaultValue($"migration_{DateTime.Now:yyyyMMdd_HHmmss}.sql"));
        var outputPath = Path.GetFullPath(output, root);

        var r = await RunWithOutputAsync("migrations script",
            on => _ef.ScriptAsync(from == fromStart ? "0" : from, to, idempotent, outputPath, on), pause: false);
        if (r.Success) AnsiConsole.MarkupLine(Format(L.T.ScriptWritten, outputPath));
        Pause();
    }

    private async Task DropDatabaseAsync()
    {
        AnsiConsole.MarkupLine(L.T.DropWarning);
        var confirm = AnsiConsole.Prompt(new TextPrompt<string>(L.T.DropConfirmPrompt).AllowEmpty());
        if (confirm != "DROP") return;

        await RunWithOutputAsync("database drop", on => _ef.DropDatabaseAsync(on));
        await RefreshAsync(connect: true, noBuild: true);
    }

    // ───────────────────────────── Çıktı ─────────────────────────────

    private static async Task<ProcessResult> RunWithOutputAsync(string title, Func<Action<string, bool>, Task<ProcessResult>> action, bool pause = true)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]dotnet ef {Markup.Escape(title)}[/]").LeftJustified());

        var r = await action(WriteOutputLine);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(r.Success ? $"[green bold]{L.T.Done}[/]" : $"[red bold]{Format(L.T.Failed, r.ExitCode)}[/]");
        if (pause) Pause();
        return r;
    }

    private static void WriteOutputLine(string line, bool isError)
    {
        var (level, text) = EfTool.SplitPrefix(line);
        var escaped = Markup.Escape(text);
        var markup = level switch
        {
            "error" => $"[red]{escaped}[/]",
            "warn" => $"[yellow]{escaped}[/]",
            "verbose" => $"[grey]{escaped}[/]",
            "info" or "data" => escaped,
            _ => isError ? $"[red]{escaped}[/]" : $"[grey]{escaped}[/]"
        };
        AnsiConsole.MarkupLine(markup);
    }

    private static void PrintErrors(ProcessResult result)
    {
        var lines = result.Lines
            .Select(EfTool.SplitPrefix)
            .Where(x => x.Level is "error" or "warn" or "")
            .Select(x => x.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .TakeLast(25)
            .ToList();
        if (lines.Count == 0) lines = result.Lines.TakeLast(25).ToList();

        var panel = new Panel(new Markup($"[red]{Markup.Escape(string.Join('\n', lines))}[/]"))
            .Header(Markup.Escape(L.T.EfOutputHeader))
            .BorderColor(Color.Red);
        AnsiConsole.Write(panel);
    }

    // ───────────────────────────── Yardımcılar ─────────────────────────────

    /// <summary>Yerelleştirilmiş metne kaçırılmış (escape) değer yerleştirir.</summary>
    private static string Format(string template, object value) =>
        string.Format(template, Markup.Escape(value.ToString() ?? ""));

    private static SelectionPrompt<T> Select<T>(string title) where T : notnull =>
        new SelectionPrompt<T>().Title(title).MoreChoicesText(L.T.MoreChoices);

    private static bool Confirm(string prompt, bool defaultValue = true) =>
        AnsiConsole.Prompt(new ConfirmationPrompt(prompt)
        {
            Yes = L.T.Yes,
            No = L.T.No,
            DefaultValue = defaultValue,
            InvalidChoiceMessage = L.T.InvalidYesNo
        });

    private static void Pause()
    {
        AnsiConsole.MarkupLine($"[grey]{L.T.PressAnyKey}[/]");
        Console.ReadKey(intercept: true);
    }
}
