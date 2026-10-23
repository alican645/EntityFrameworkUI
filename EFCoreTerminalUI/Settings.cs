using System.Text.Json;

namespace EFCoreTerminalUI;

/// <summary>
/// Çalışılan klasörde .efui.json olarak saklanır; bir sonraki açılışta proje seçimi tekrar sorulmaz.
/// </summary>
public sealed class Settings
{
    public const string FileName = ".efui.json";

    public string? MigrationProject { get; set; }
    public string? StartupProject { get; set; }
    public string? DbContext { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Settings Load(string root)
    {
        var path = Path.Combine(root, FileName);
        if (!File.Exists(path)) return new Settings();
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save(string root) =>
        File.WriteAllText(Path.Combine(root, FileName), JsonSerializer.Serialize(this, JsonOptions));
}
