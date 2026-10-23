using Spectre.Console;

namespace EFCoreTerminalUI;

public sealed class App(string root)
{
    private Settings _settings = Settings.Load(root);
    private EfTool _ef = null!;
    private List<MigrationInfo> _migrations = [];
    private bool _dbConnected;

    private const string Back = "← Geri";

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
                new SelectionPrompt<MenuItem>()
                    .Title("[bold]Ne yapmak istiyorsun?[/] [grey](↑/↓ ve Enter)[/]")
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
                case MenuItem.Configure:
                    if (await ConfigureAsync(forceAsk: true)) await RefreshAsync(connect: true);
                    break;
                case MenuItem.Drop: await DropDatabaseAsync(); break;
                case MenuItem.Exit: return 0;
            }
        }
    }

    private enum MenuItem { Add, Remove, UpdateLatest, UpdateTo, Script, Refresh, Configure, Drop, Exit }

    private static string MenuText(MenuItem item) => item switch
    {
        MenuItem.Add => "[green]+[/] Add Migration",
        MenuItem.Remove => "[red]-[/] Remove Last Migration",
        MenuItem.UpdateLatest => "[deepskyblue1]↑[/] Update Database [grey](son migration)[/]",
        MenuItem.UpdateTo => "[deepskyblue1]→[/] Update Database [grey](migration seç / geri al)[/]",
        MenuItem.Script => "[grey]#[/] SQL Script oluştur",
        MenuItem.Refresh => "[grey]↻[/] Listeyi yenile",
        MenuItem.Configure => "[grey]*[/] Proje / DbContext değiştir",
        MenuItem.Drop => "[red]![/] Drop Database",
        MenuItem.Exit => "[grey]x[/] Çıkış",
        _ => item.ToString()
    };

    // ───────────────────────────── Kurulum ─────────────────────────────

    private static void Header()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold deepskyblue1]EF Core Terminal UI[/]").LeftJustified());
    }

    private static async Task<bool> EnsureEfInstalledAsync()
    {
        var installed = false;
        await AnsiConsole.Status().StartAsync("dotnet-ef kontrol ediliyor...", async _ =>
            installed = await EfTool.IsInstalledAsync());
        if (installed) return true;

        AnsiConsole.MarkupLine("[yellow]dotnet-ef aracı bulunamadı.[/]");
        if (!Confirm("Global olarak yüklensin mi? (dotnet tool install -g dotnet-ef)")) return false;

        var r = await EfTool.InstallAsync(WriteOutputLine);
        return r.Success;
    }

    private async Task<bool> ConfigureAsync(bool forceAsk)
    {
        var projects = ProjectScanner.Scan(root);
        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Bu klasörde .csproj bulunamadı:[/] {Markup.Escape(root)}");
            AnsiConsole.MarkupLine("[grey]efui'yi solution klasöründe çalıştır.[/]");
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
                migration = PickProject(projects, "[bold]Migration projesi[/] (DbContext / Migrations klasörünün olduğu)",
                    projects.OrderByDescending(p => p.MigrationScore).ToList());
                startup = PickProject(projects, "[bold]Startup projesi[/] (appsettings / Program.cs olan)",
                    projects.OrderByDescending(p => p.StartupScore).ToList());
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

    private static ProjectInfo PickProject(List<ProjectInfo> all, string title, List<ProjectInfo> ordered)
    {
        Header();
        return AnsiConsole.Prompt(
            new SelectionPrompt<ProjectInfo>()
                .Title(title + " [grey](önerilen en üstte)[/]")
                .PageSize(15)
                .UseConverter(p => $"{Markup.Escape(p.Name)} [grey]{Markup.Escape(p.RelativePath)}[/]")
                .AddChoices(ordered));
    }

    private async Task<bool> PickDbContextAsync()
    {
        Header();
        JsonCommandResult<List<DbContextInfo>>? result = null;
        await AnsiConsole.Status().StartAsync("Proje derleniyor, DbContext'ler aranıyor...", async _ =>
            result = await _ef.ListDbContextsAsync());

        var contexts = result!.Data;
        if (contexts is null)
        {
            AnsiConsole.MarkupLine("[red]DbContext listesi alınamadı.[/]");
            PrintErrors(result.Process);
            if (Confirm("Proje seçimini değiştirmek ister misin?"))
                return await ConfigureAsync(forceAsk: true);
            return false;
        }

        if (contexts.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Seçili projelerde DbContext bulunamadı.[/]");
            if (Confirm("Proje seçimini değiştirmek ister misin?"))
                return await ConfigureAsync(forceAsk: true);
            return false;
        }

        var selected = contexts.Count == 1
            ? contexts[0]
            : AnsiConsole.Prompt(
                new SelectionPrompt<DbContextInfo>()
                    .Title("[bold]DbContext seç[/]")
                    .UseConverter(c => $"{Markup.Escape(c.Name)} [grey]{Markup.Escape(c.FullName)}[/]")
                    .AddChoices(contexts));

        _settings.DbContext = selected.FullName;
        _ef.DbContext = selected.FullName;
        return true;
    }

    private void SaveSettings()
    {
        try { _settings.Save(root); }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[yellow]Ayarlar kaydedilemedi: {Markup.Escape(ex.Message)}[/]"); }
    }

    // ───────────────────────────── Liste ─────────────────────────────

    private async Task RefreshAsync(bool connect, bool noBuild = false)
    {
        JsonCommandResult<List<MigrationInfo>>? result = null;
        await AnsiConsole.Status().StartAsync(noBuild ? "Migration'lar okunuyor..." : "Derleniyor ve migration'lar okunuyor...", async ctx =>
        {
            result = await _ef.ListMigrationsAsync(connect, noBuild);
            if (result.Data is null && connect)
            {
                // Veritabanına bağlanılamadıysa bağlantısız tekrar dene
                ctx.Status("Veritabanına bağlanılamadı, bağlantısız okunuyor...");
                result = await _ef.ListMigrationsAsync(connect: false, noBuild: true);
            }
        });

        if (result!.Data is null)
        {
            _migrations = [];
            _dbConnected = false;
            AnsiConsole.MarkupLine("[red]Migration listesi alınamadı.[/]");
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
        info.AddRow("[grey]Klasör[/]", Markup.Escape(root));
        info.AddRow("[grey]Migration[/]", Markup.Escape(_settings.MigrationProject ?? "-"));
        info.AddRow("[grey]Startup[/]", Markup.Escape(_settings.StartupProject ?? "-"));
        info.AddRow("[grey]DbContext[/]", Markup.Escape(_settings.DbContext ?? "-"));
        info.AddRow("[grey]Veritabanı[/]", _dbConnected ? "[green]bağlı[/]" : "[yellow]bağlanılamadı (durum bilinmiyor)[/]");
        AnsiConsole.Write(info);
        AnsiConsole.WriteLine();

        if (_migrations.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Henüz migration yok.[/]\n");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("#");
        table.AddColumn("Migration");
        table.AddColumn("Durum");

        const int maxRows = 10;
        var start = Math.Max(0, _migrations.Count - maxRows);
        if (start > 0) table.AddRow("[grey]…[/]", $"[grey]{start} eski migration gizlendi[/]", "");

        for (var i = start; i < _migrations.Count; i++)
        {
            var m = _migrations[i];
            var isLast = i == _migrations.Count - 1;
            var name = Markup.Escape(m.Id);
            if (isLast) name = $"[bold deepskyblue1]{name}[/]  [deepskyblue1]◀ son[/]";

            var status = m.Applied switch
            {
                true => "[green]✔ uygulandı[/]",
                false => "[yellow]● bekliyor[/]",
                null => "[grey]?[/]"
            };
            table.AddRow((i + 1).ToString(), name, status);
        }

        AnsiConsole.Write(table);

        var pending = _migrations.Count(m => m.Applied == false);
        if (pending > 0) AnsiConsole.MarkupLine($"[yellow]{pending} migration veritabanına uygulanmamış.[/]");
        AnsiConsole.WriteLine();
    }

    // ───────────────────────────── İşlemler ─────────────────────────────

    private async Task AddMigrationAsync()
    {
        var name = AnsiConsole.Prompt(
            new TextPrompt<string>("Migration adı [grey](boş bırak = iptal)[/]:")
                .AllowEmpty()
                .Validate(n => n.Length == 0 || n.All(c => char.IsLetterOrDigit(c) || c == '_')
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Sadece harf, rakam ve _ kullan[/]")));
        if (string.IsNullOrWhiteSpace(name)) return;

        var r = await RunWithOutputAsync($"migrations add {name}", on => _ef.AddMigrationAsync(name, null, on));
        if (r.Success && Confirm("Veritabanı şimdi güncellensin mi?", defaultValue: false))
        {
            await RunWithOutputAsync("database update", on => _ef.UpdateDatabaseAsync(null, on));
        }
        await RefreshAsync(connect: true, noBuild: true);
    }

    private async Task RemoveMigrationAsync()
    {
        if (_migrations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Silinecek migration yok.[/]");
            Pause();
            return;
        }

        var last = _migrations[^1];
        var force = false;

        if (last.Applied == true)
        {
            AnsiConsole.MarkupLine($"[yellow]Uyarı:[/] [bold]{Markup.Escape(last.Id)}[/] veritabanına uygulanmış.");
            var option = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Nasıl devam edilsin?")
                    .AddChoices("Veritabanından geri al ve sil (--force)", Back));
            if (option == Back) return;
            force = true;
        }
        else if (!Confirm($"[bold]{Markup.Escape(last.Id)}[/] silinsin mi?", defaultValue: false))
        {
            return;
        }

        await RunWithOutputAsync("migrations remove", on => _ef.RemoveLastMigrationAsync(force, on));
        await RefreshAsync(connect: true, noBuild: false);
    }

    private async Task UpdateToSelectedAsync()
    {
        const string zero = "0 — tüm migration'ları geri al (boş veritabanı)";

        var choices = new List<string> { Back };
        choices.AddRange(_migrations.AsEnumerable().Reverse().Select(m => m.Id));
        choices.Add(zero);

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Veritabanı hangi migration'a getirilsin? [grey](en yeni üstte)[/]")
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

        if (selected == Back) return;
        var target = selected == zero ? "0" : selected;

        if (target == "0" && !Confirm("[red]Tüm migration'lar geri alınacak.[/] Emin misin?", defaultValue: false))
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
            AnsiConsole.MarkupLine("[yellow]Script oluşturmak için migration yok.[/]");
            Pause();
            return;
        }

        const string fromStart = "(baştan)";
        var ids = _migrations.Select(m => m.Id).ToList();

        var from = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Başlangıç migration'ı [grey](bu migration hariç sonrası)[/]")
            .PageSize(15)
            .AddChoices([fromStart, .. ids]));

        var toChoices = from == fromStart ? ids : ids.Skip(ids.IndexOf(from) + 1).ToList();
        if (toChoices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Seçilen migration zaten en son.[/]");
            Pause();
            return;
        }

        var to = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Bitiş migration'ı")
            .PageSize(15)
            .AddChoices(toChoices.AsEnumerable().Reverse()));

        var idempotent = Confirm("Idempotent script olsun mu?", defaultValue: false);
        var output = AnsiConsole.Prompt(new TextPrompt<string>("Çıktı dosyası:").DefaultValue($"migration_{DateTime.Now:yyyyMMdd_HHmmss}.sql"));
        var outputPath = Path.GetFullPath(output, root);

        var r = await RunWithOutputAsync("migrations script",
            on => _ef.ScriptAsync(from == fromStart ? "0" : from, to, idempotent, outputPath, on), pause: false);
        if (r.Success) AnsiConsole.MarkupLine($"[green]Script yazıldı:[/] {Markup.Escape(outputPath)}");
        Pause();
    }

    private async Task DropDatabaseAsync()
    {
        AnsiConsole.MarkupLine("[red bold]Bu işlem veritabanını tamamen siler![/]");
        var confirm = AnsiConsole.Prompt(new TextPrompt<string>("Onaylamak için [red]DROP[/] yaz:").AllowEmpty());
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
        AnsiConsole.MarkupLine(r.Success ? "[green bold]✔ Tamamlandı[/]" : $"[red bold]✘ Başarısız (çıkış kodu {r.ExitCode})[/]");
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
            .Header("dotnet ef çıktısı")
            .BorderColor(Color.Red);
        AnsiConsole.Write(panel);
    }

    private static bool Confirm(string prompt, bool defaultValue = true) =>
        AnsiConsole.Prompt(new ConfirmationPrompt(prompt)
        {
            Yes = 'e',
            No = 'h',
            DefaultValue = defaultValue,
            InvalidChoiceMessage = "[red]e (evet) veya h (hayır) gir[/]"
        });

    private static void Pause()
    {
        AnsiConsole.MarkupLine("[grey]Devam etmek için bir tuşa bas...[/]");
        Console.ReadKey(intercept: true);
    }
}
