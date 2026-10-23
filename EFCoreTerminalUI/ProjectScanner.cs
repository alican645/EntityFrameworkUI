using System.Xml.Linq;

namespace EFCoreTerminalUI;

public sealed record ProjectInfo(
    string FullPath,
    string RelativePath,
    string Name,
    bool ReferencesEfCore,
    bool ReferencesEfDesign,
    bool IsExecutable,
    bool HasMigrationsFolder)
{
    /// <summary>Migration'ların bulunması muhtemel proje (DbContext / Migrations klasörü).</summary>
    public int MigrationScore =>
        (HasMigrationsFolder ? 4 : 0) + (ReferencesEfCore ? 2 : 0) - (IsExecutable ? 1 : 0);

    /// <summary>Startup olması muhtemel proje (Web / Exe + EF Design).</summary>
    public int StartupScore =>
        (IsExecutable ? 4 : 0) + (ReferencesEfDesign ? 2 : 0) - (Name.Contains("Test", StringComparison.OrdinalIgnoreCase) ? 5 : 0);
}

public static class ProjectScanner
{
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", "node_modules", "packages", "artifacts", "nupkg"
    };

    public static List<ProjectInfo> Scan(string root)
    {
        var result = new List<ProjectInfo>();
        foreach (var file in EnumerateProjects(root))
        {
            try { result.Add(Read(file, root)); }
            catch { /* okunamayan csproj'u atla */ }
        }
        return result.OrderBy(p => p.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> EnumerateProjects(string dir, int depth = 0)
    {
        if (depth > 6) yield break;

        string[] files;
        string[] dirs;
        try
        {
            files = Directory.GetFiles(dir, "*.csproj");
            dirs = Directory.GetDirectories(dir);
        }
        catch { yield break; }

        foreach (var f in files) yield return f;
        foreach (var d in dirs)
        {
            if (IgnoredDirs.Contains(Path.GetFileName(d))) continue;
            foreach (var f in EnumerateProjects(d, depth + 1)) yield return f;
        }
    }

    private static ProjectInfo Read(string path, string root)
    {
        var doc = XDocument.Load(path);
        var project = doc.Root!;
        var sdk = project.Attribute("Sdk")?.Value ?? "";

        var packages = project.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .ToList();

        var outputType = project.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value ?? "";

        var isExe = sdk.Contains("Web", StringComparison.OrdinalIgnoreCase)
                    || sdk.Contains("Worker", StringComparison.OrdinalIgnoreCase)
                    || outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase)
                    || outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);

        var dir = Path.GetDirectoryName(path)!;

        return new ProjectInfo(
            FullPath: path,
            RelativePath: Path.GetRelativePath(root, path),
            Name: Path.GetFileNameWithoutExtension(path),
            ReferencesEfCore: packages.Any(p => p.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                                                || p.StartsWith("Npgsql.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                                                || p.StartsWith("Pomelo.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)),
            ReferencesEfDesign: packages.Any(p => p.Equals("Microsoft.EntityFrameworkCore.Design", StringComparison.OrdinalIgnoreCase)
                                                  || p.Equals("Microsoft.EntityFrameworkCore.Tools", StringComparison.OrdinalIgnoreCase)),
            IsExecutable: isExe,
            HasMigrationsFolder: Directory.Exists(Path.Combine(dir, "Migrations"))
                                 || Directory.EnumerateDirectories(dir, "Migrations", SearchOption.AllDirectories)
                                     .Any(d => !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                               && !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")));
    }
}
