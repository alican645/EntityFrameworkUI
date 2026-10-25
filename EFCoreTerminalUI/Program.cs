using System.Text;
using EFCoreTerminalUI;
using EFCoreTerminalUI.Localization;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var userConfig = UserConfig.Load();
var language = L.Find(userConfig.Language);
L.Set(language ?? L.Detect());

// İsteğe bağlı: efui <klasör>
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Environment.CurrentDirectory;
if (!Directory.Exists(root))
{
    AnsiConsole.MarkupLine(string.Format(L.T.FolderNotFound, Markup.Escape(root)));
    return 1;
}

// Bir alt projeden çalıştırıldıysa solution klasörüne çık
var solutionRoot = ProjectScanner.FindSolutionRoot(root);
if (solutionRoot != root)
{
    AnsiConsole.MarkupLine(string.Format(L.T.SolutionRootUsed, Markup.Escape(solutionRoot)));
    root = solutionRoot;
}

try
{
    // İlk çalıştırmada dil sor (sistem dili en üstte)
    if (language is null) App.PickLanguage(userConfig);

    return await new App(root, userConfig).RunAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}
