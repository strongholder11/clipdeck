using System.Text.Json;
using ClipDeck.App.Platform;
using ClipDeck.Core.Models;

namespace ClipDeck.App.Services;

/// <summary>
/// Preferências do usuário, em %APPDATA%\ClipDeck\settings.json — ao lado da
/// biblioteca, separadas dela para exportar templates não levar junto atalhos
/// que só fazem sentido nesta máquina.
/// </summary>
public sealed class Preferences
{
    private sealed class Data
    {
        public KeyCombo? PaletteShortcut { get; set; }
        public KeyCombo? CaptureShortcut { get; set; }
        public bool? PasteAutomatically { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly IStartupService _startup;
    private KeyCombo _paletteShortcut;
    private KeyCombo _captureShortcut;
    private bool _pasteAutomatically;

    /// <summary>Um dos atalhos mudou: é hora de registrá-los de novo.</summary>
    public event Action? ShortcutsChanged;

    public Preferences(IStartupService startup, string? directory = null)
    {
        _startup = startup;
        _path = Path.Combine(directory ?? LibraryStorage.DefaultDirectory, "settings.json");

        Data? data = null;
        try
        {
            if (File.Exists(_path)) data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_path), Options);
        }
        catch { /* arquivo ruim: volta aos padrões em vez de não abrir */ }

        _paletteShortcut = data?.PaletteShortcut ?? KeyCombo.OpenPalette;
        _captureShortcut = data?.CaptureShortcut ?? KeyCombo.CaptureClipboard;
        _pasteAutomatically = data?.PasteAutomatically ?? true;
    }

    public KeyCombo PaletteShortcut
    {
        get => _paletteShortcut;
        set { if (_paletteShortcut == value) return; _paletteShortcut = value; Save(); ShortcutsChanged?.Invoke(); }
    }

    public KeyCombo CaptureShortcut
    {
        get => _captureShortcut;
        set { if (_captureShortcut == value) return; _captureShortcut = value; Save(); ShortcutsChanged?.Invoke(); }
    }

    /// <summary>Quando falso, Enter na paleta copia em vez de colar.</summary>
    public bool PasteAutomatically
    {
        get => _pasteAutomatically;
        set { if (_pasteAutomatically == value) return; _pasteAutomatically = value; Save(); }
    }

    public bool LaunchAtLogin => _startup.IsEnabled;

    /// <summary>Retorna a mensagem de erro, ou null se deu certo.</summary>
    public string? SetLaunchAtLogin(bool enabled) => _startup.SetEnabled(enabled);

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new Data
            {
                PaletteShortcut = _paletteShortcut,
                CaptureShortcut = _captureShortcut,
                PasteAutomatically = _pasteAutomatically,
            }, Options));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[clipdeck] falha ao gravar preferências: {ex.Message}");
        }
    }
}
