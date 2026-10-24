using System.Text.Json;

namespace EFCoreTerminalUI;

/// <summary>
/// Kullanıcıya özel, projeden bağımsız tercihler (~/.efui/config.json).
/// </summary>
public sealed class UserConfig
{
    public string? Language { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".efui", "config.json");

    public static UserConfig Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<UserConfig>(File.ReadAllText(FilePath)) ?? new UserConfig()
                : new UserConfig();
        }
        catch { return new UserConfig(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
