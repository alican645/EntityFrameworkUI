using System.Diagnostics;
using System.Text;

namespace EFCoreTerminalUI;

public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Lines)
{
    public bool Success => ExitCode == 0;
}

public static class ProcessRunner
{
    /// <summary>
    /// Komutu çalıştırır; stdout ve stderr satırlarını sırayla toplar ve her satır için onLine'ı çağırır.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> args,
        string workingDirectory,
        Action<string, bool>? onLine = null)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var lines = new List<string>();
        var gate = new object();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (gate) { lines.Add(e.Data); onLine?.Invoke(e.Data, false); }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (gate) { lines.Add(e.Data); onLine?.Invoke(e.Data, true); }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, lines);
    }
}
