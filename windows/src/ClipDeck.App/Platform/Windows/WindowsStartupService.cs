namespace ClipDeck.App.Platform.Windows;

/// <summary>
/// Abrir junto com o Windows, via atalho na pasta Inicializar.
///
/// É o mesmo atalho que o instalador cria, com o mesmo nome: desligar aqui remove
/// o do instalador, e religar recria no mesmo lugar — em vez de manter dois
/// mecanismos (pasta e registro) que poderiam abrir o app duas vezes.
/// </summary>
public sealed class WindowsStartupService : IStartupService
{
    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClipDeck.lnk");

    public bool IsEnabled => File.Exists(ShortcutPath);

    public string? SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
                return null;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return "Não foi possível localizar o ClipDeck.exe.";

            // WScript.Shell é a forma suportada de criar um .lnk sem dependências.
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                            ?? throw new InvalidOperationException("WScript.Shell indisponível");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
            shortcut.Description = "ClipDeck";
            shortcut.Save();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
