using ClipDeck.Core.Models;

namespace ClipDeck.App.Services;

/// <summary>
/// Registros de diagnóstico em %APPDATA%\ClipDeck.
///
/// clipdeck.log fica sempre ativo: falhas de foco e de tela são intermitentes e
/// dependem de monitor, então quando o usuário percebe já é tarde para pedir que
/// reproduza com uma flag. Só metadados de janela — nada do conteúdo dos
/// templates. debug.log só com --debug.
/// </summary>
public static class DiagnosticsLog
{
    private const long MaxBytes = 64_000;

    public static bool DebugEnabled =>
        Environment.GetEnvironmentVariable("CLIPDECK_DEBUG") is not null
        || Environment.GetCommandLineArgs().Contains("--debug");

    public static void Palette(string line) => Append("clipdeck.log", line);

    public static void Debug(string line)
    {
        if (DebugEnabled) Append("debug.log", line);
    }

    private static void Append(string file, string line)
    {
        try
        {
            var path = Path.Combine(LibraryStorage.DefaultDirectory, file);
            // Trunca ao passar do limite: é diagnóstico, não histórico.
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:dd/MM/yyyy HH:mm:ss}] {line}{Environment.NewLine}");
        }
        catch { /* diagnóstico nunca derruba o app */ }
    }
}
