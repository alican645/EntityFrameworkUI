using System.Text;
using EFCoreTerminalUI;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

// İsteğe bağlı: efui <klasör>
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Environment.CurrentDirectory;
if (!Directory.Exists(root))
{
    AnsiConsole.MarkupLine($"[red]Klasör bulunamadı:[/] {Markup.Escape(root)}");
    return 1;
}

try
{
    return await new App(root).RunAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}
