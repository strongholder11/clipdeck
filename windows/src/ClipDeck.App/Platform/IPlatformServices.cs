using System.Text.Json.Serialization;

namespace ClipDeck.App.Platform;

/// <summary>Uma combinação de teclas global. KeyCode é o virtual-key code do Windows.</summary>
public readonly record struct KeyCombo(uint KeyCode, KeyModifiers Modifiers)
{
    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
            parts.Add(KeyName);
            return string.Join("+", parts);
        }
    }

    private string KeyName => KeyCode switch
    {
        0x20 => "Espaço",
        0x0D => "Enter",
        0x09 => "Tab",
        0x08 => "Backspace",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ when KeyCode >= 0x41 && KeyCode <= 0x5A => ((char)KeyCode).ToString(),
        _ when KeyCode >= 0x30 && KeyCode <= 0x39 => ((char)KeyCode).ToString(),
        _ when KeyCode >= 0x60 && KeyCode <= 0x69 => $"Num{KeyCode - 0x60}",
        _ when KeyCode >= 0x70 && KeyCode <= 0x87 => $"F{KeyCode - 0x6F}",
        _ => $"0x{KeyCode:X2}",
    };

    /// <summary>Alt+Espaço abre a paleta.</summary>
    public static readonly KeyCombo OpenPalette = new(0x20, KeyModifiers.Alt);

    /// <summary>Ctrl+Shift+C salva a área de transferência como template.</summary>
    public static readonly KeyCombo CaptureClipboard = new(0x43, KeyModifiers.Control | KeyModifiers.Shift);
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Atalhos globais, ativos com qualquer app em foco.
/// </summary>
public interface IHotKeyService : IDisposable
{
    /// <summary>
    /// Registra o atalho. Retorna false quando outro app já ocupa a combinação —
    /// a interface avisa em vez de o atalho simplesmente não funcionar.
    /// </summary>
    bool Register(KeyCombo combo, Action handler);

    void UnregisterAll();
}

/// <summary>
/// Cola texto no app que estava em foco antes da paleta abrir.
/// </summary>
public interface IPasteService
{
    /// <summary>
    /// Cola o texto no app anterior. Retorna false se só conseguiu copiar —
    /// quem chama decide o que dizer ao usuário.
    /// </summary>
    bool Paste(string text);

    /// <summary>Guarda qual janela estava em foco, antes de mostrarmos a nossa.</summary>
    void RememberForegroundWindow();
}

/// <summary>
/// Abrir o ClipDeck junto com o Windows. O estado vive no sistema (o atalho na
/// pasta Inicializar), não numa preferência nossa — ler de lá evita a tela dizer
/// "ligado" enquanto o Windows acha que está desligado.
/// </summary>
public interface IStartupService
{
    bool IsEnabled { get; }

    /// <summary>Retorna a mensagem de erro, ou null se deu certo.</summary>
    string? SetEnabled(bool enabled);
}
