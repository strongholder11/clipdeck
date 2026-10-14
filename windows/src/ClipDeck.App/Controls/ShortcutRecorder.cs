using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClipDeck.App.Platform;
using AvaloniaModifiers = Avalonia.Input.KeyModifiers;
using PlatformModifiers = ClipDeck.App.Platform.KeyModifiers;

namespace ClipDeck.App.Controls;

/// <summary>
/// Campo que grava um atalho: clique e pressione a combinação. Esc cancela.
/// </summary>
public sealed class ShortcutRecorder : Button
{
    public static readonly StyledProperty<KeyCombo> ComboProperty =
        AvaloniaProperty.Register<ShortcutRecorder, KeyCombo>(nameof(Combo));

    private bool _recording;

    protected override Type StyleKeyOverride => typeof(Button);

    public KeyCombo Combo
    {
        get => GetValue(ComboProperty);
        set => SetValue(ComboProperty, value);
    }

    /// <summary>Uma combinação nova foi gravada.</summary>
    public event Action<KeyCombo>? Recorded;

    /// <summary>
    /// Entrou ou saiu do modo de gravação. Quem registra os atalhos globais
    /// precisa suspendê-los nesse intervalo: o RegisterHotKey intercepta a
    /// combinação antes de qualquer janela, e regravar o atalho atual seria
    /// impossível — a paleta abriria em vez de a tecla chegar aqui.
    /// </summary>
    public event Action<bool>? RecordingChanged;

    public ShortcutRecorder()
    {
        MinWidth = 160;
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        UpdateLabel();
        LostFocus += (_, _) => Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ComboProperty) UpdateLabel();
    }

    protected override void OnClick()
    {
        base.OnClick();
        SetRecording(!_recording);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_recording) { base.OnKeyDown(e); return; }
        e.Handled = true;

        if (e.Key == Key.Escape) { Stop(); return; }

        // Só modificador ainda: espera a tecla principal.
        if (IsModifier(e.Key)) return;

        var modifiers = PlatformModifiers.None;
        if (e.KeyModifiers.HasFlag(AvaloniaModifiers.Control)) modifiers |= PlatformModifiers.Control;
        if (e.KeyModifiers.HasFlag(AvaloniaModifiers.Alt)) modifiers |= PlatformModifiers.Alt;
        if (e.KeyModifiers.HasFlag(AvaloniaModifiers.Shift)) modifiers |= PlatformModifiers.Shift;
        if (e.KeyModifiers.HasFlag(AvaloniaModifiers.Meta)) modifiers |= PlatformModifiers.Win;

        // Sem Ctrl, Alt ou Win o atalho global engoliria a digitação normal:
        // "A" sozinho, ou Shift+A, deixaria de funcionar em todo programa.
        var hasRealModifier = (modifiers & ~PlatformModifiers.Shift) != PlatformModifiers.None;
        var isFunctionKey = e.Key >= Key.F1 && e.Key <= Key.F24;
        if (!hasRealModifier && !isFunctionKey) return;

        if (VirtualKey(e.Key) is not { } vk) return;

        var combo = new KeyCombo(vk, modifiers);
        Combo = combo;
        SetRecording(false);
        Recorded?.Invoke(combo);
    }

    private void Stop() => SetRecording(false);

    private void SetRecording(bool recording)
    {
        var changed = _recording != recording;
        _recording = recording;
        UpdateLabel();
        if (changed) RecordingChanged?.Invoke(recording);
    }

    private void UpdateLabel() =>
        Content = _recording ? "Pressione o atalho…" : Combo.DisplayString;

    private static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    /// <summary>Converte a tecla do Avalonia no virtual-key code que o RegisterHotKey usa.</summary>
    public static uint? VirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (uint)(0x41 + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (uint)(0x30 + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (key - Key.NumPad0)),
        >= Key.F1 and <= Key.F24 => (uint)(0x70 + (key - Key.F1)),
        Key.Space => 0x20,
        Key.Enter => 0x0D,
        Key.Tab => 0x09,
        Key.Back => 0x08,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Home => 0x24,
        Key.End => 0x23,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.OemSemicolon => 0xBA,
        Key.OemPlus => 0xBB,
        Key.OemComma => 0xBC,
        Key.OemMinus => 0xBD,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemTilde => 0xC0,
        Key.OemOpenBrackets => 0xDB,
        Key.OemPipe => 0xDC,
        Key.OemCloseBrackets => 0xDD,
        Key.OemQuotes => 0xDE,
        _ => null,
    };
}
