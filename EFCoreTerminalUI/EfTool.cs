using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EFCoreTerminalUI;

public sealed record MigrationInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("applied")] bool? Applied);

public sealed record DbContextInfo(
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("name")] string Name);

public sealed record JsonCommandResult<T>(T? Data, ProcessResult Process);

/// <summary>
/// `dotnet ef` komutlarını seçili migration/startup projesi ve DbContext ile çalıştırır.
/// </summary>
public sealed class EfTool(string root, string migrationProject, string startupProject, string? dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string? DbContext { get; set; } = dbContext;

    public static async Task<bool> IsInstalledAsync()
    {
        try
        {
            var r = await ProcessRunner.RunAsync("dotnet", ["ef", "--version"], Environment.CurrentDirectory);
            return r.Success;
        }
        catch { return false; }
    }

    public static Task<ProcessResult> InstallAsync(Action<string, bool> onLine) =>
        ProcessRunner.RunAsync("dotnet", ["tool", "install", "--global", "dotnet-ef"], Environment.CurrentDirectory, onLine);

    public Task<JsonCommandResult<List<DbContextInfo>>> ListDbContextsAsync(Action<string, bool>? onLine = null) =>
        RunJsonAsync<List<DbContextInfo>>(["dbcontext", "list"], includeContext: false, onLine);

    public async Task<JsonCommandResult<List<MigrationInfo>>> ListMigrationsAsync(bool connect, bool noBuild, Action<string, bool>? onLine = null)
    {
        var args = new List<string> { "migrations", "list" };
        if (!connect) args.Add("--no-connect");
        if (noBuild) args.Add("--no-build");
        return await RunJsonAsync<List<MigrationInfo>>(args, includeContext: true, onLine);
    }

    public Task<ProcessResult> AddMigrationAsync(string name, string? outputDir, Action<string, bool> onLine)
    {
        var args = new List<string> { "migrations", "add", name };
        if (!string.IsNullOrWhiteSpace(outputDir)) { args.Add("--output-dir"); args.Add(outputDir); }
        return RunAsync(args, onLine);
    }

    public Task<ProcessResult> RemoveLastMigrationAsync(bool force, Action<string, bool> onLine)
    {
        var args = new List<string> { "migrations", "remove" };
        if (force) args.Add("--force");
        return RunAsync(args, onLine);
    }

    /// <param name="target">null = son migration, "0" = tüm migration'ları geri al.</param>
    public Task<ProcessResult> UpdateDatabaseAsync(string? target, Action<string, bool> onLine)
    {
        var args = new List<string> { "database", "update" };
        if (target is not null) args.Add(target);
        return RunAsync(args, onLine);
    }

    public Task<ProcessResult> ScriptAsync(string? from, string? to, bool idempotent, string output, Action<string, bool> onLine)
    {
        var args = new List<string> { "migrations", "script" };
        if (from is not null) args.Add(from);
        if (to is not null) args.Add(to);
        if (idempotent) args.Add("--idempotent");
        args.Add("--output");
        args.Add(output);
        return RunAsync(args, onLine);
    }

    public Task<ProcessResult> DropDatabaseAsync(Action<string, bool> onLine) =>
        RunAsync(["database", "drop", "--force"], onLine);

    private Task<ProcessResult> RunAsync(IEnumerable<string> commandArgs, Action<string, bool>? onLine, bool includeContext = true) =>
        ProcessRunner.RunAsync("dotnet", BuildArgs(commandArgs, includeContext), root, onLine);

    private List<string> BuildArgs(IEnumerable<string> commandArgs, bool includeContext)
    {
        var args = new List<string> { "ef" };
        args.AddRange(commandArgs);
        args.AddRange(["--project", migrationProject, "--startup-project", startupProject]);
        if (includeContext && !string.IsNullOrWhiteSpace(DbContext)) args.AddRange(["--context", DbContext]);
        args.AddRange(["--no-color", "--prefix-output"]);
        return args;
    }

    private async Task<JsonCommandResult<T>> RunJsonAsync<T>(List<string> commandArgs, bool includeContext, Action<string, bool>? onLine)
    {
        commandArgs.Add("--json");
        var result = await RunAsync(commandArgs, onLine, includeContext);

        // --prefix-output ile JSON satırları "data:" önekiyle gelir.
        var json = new StringBuilder();
        foreach (var line in result.Lines)
        {
            var (level, text) = SplitPrefix(line);
            if (level == "data") json.AppendLine(text);
        }

        T? data = default;
        if (result.Success && json.Length > 0)
        {
            try { data = JsonSerializer.Deserialize<T>(json.ToString(), JsonOptions); }
            catch (JsonException) { /* veri yok sayılır, çağıran tarafta hata gösterilir */ }
        }
        return new JsonCommandResult<T>(data, result);
    }

    /// <summary>"info:    Build started..." gibi bir satırı (info, "Build started...") olarak ayırır.</summary>
    public static (string Level, string Text) SplitPrefix(string line)
    {
        string[] levels = ["data", "info", "warn", "error", "verbose"];
        foreach (var level in levels)
        {
            var prefix = level + ":";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                var text = line[prefix.Length..];
                // önek sonrası hizalama boşluklarını at, JSON girintisini koru
                return (level, text.Length > 0 && text[0] == ' ' ? TrimAlignment(text, level) : text);
            }
        }
        return ("", line);
    }

    // dotnet ef önekleri en uzun önek (verbose:) kadar boşlukla hizalar.
    private static string TrimAlignment(string text, string level)
    {
        var pad = "verbose:".Length - (level.Length + 1) + 1;
        var i = 0;
        while (i < pad && i < text.Length && text[i] == ' ') i++;
        return text[i..];
    }
}
