using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClipDeck.App.Controls;
using ClipDeck.App.Services;

namespace ClipDeck.App.Views;

/// <summary>Atalhos, comportamento ao confirmar e abertura com o Windows.</summary>
public sealed class PreferencesWindow : Window
{
    private readonly Preferences _preferences;
    private readonly TextBlock _pasteHint = Caption("");
    private readonly TextBlock _startupError = new()
    {
        FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#E08A2E")),
        TextWrapping = TextWrapping.Wrap, IsVisible = false,
    };

    /// <param name="suspendHotKeys">Chamado com true ao começar a gravar um atalho, e false ao terminar.</param>
    public PreferencesWindow(Preferences preferences, Action<bool> suspendHotKeys)
    {
        _preferences = preferences;

        Title = "Preferências do ClipDeck";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var palette = new ShortcutRecorder { Combo = preferences.PaletteShortcut };
        palette.Recorded += combo => preferences.PaletteShortcut = combo;

        var capture = new ShortcutRecorder { Combo = preferences.CaptureShortcut };
        capture.Recorded += combo => preferences.CaptureShortcut = combo;

        palette.RecordingChanged += suspendHotKeys;
        capture.RecordingChanged += suspendHotKeys;
        Closed += (_, _) => suspendHotKeys(false);

        var paste = new CheckBox
        {
            Content = "Colar automaticamente ao pressionar Enter",
            IsChecked = preferences.PasteAutomatically,
        };
        paste.IsCheckedChanged += (_, _) =>
        {
            preferences.PasteAutomatically = paste.IsChecked == true;
            UpdatePasteHint();
        };
        UpdatePasteHint();

        var startup = new CheckBox
        {
            Content = "Abrir o ClipDeck ao ligar o Windows",
            IsChecked = preferences.LaunchAtLogin,
        };
        startup.IsCheckedChanged += (_, _) =>
        {
            var wanted = startup.IsChecked == true;
            var error = preferences.SetLaunchAtLogin(wanted);
            _startupError.Text = error ?? string.Empty;
            _startupError.IsVisible = error is not null;

            // Reverte se o sistema recusou, para a caixa não mentir sobre o estado.
            if (preferences.LaunchAtLogin != wanted) startup.IsChecked = preferences.LaunchAtLogin;
        };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 10,
            Children =
            {
                Heading("Atalhos"),
                Row("Abrir a paleta", palette),
                Row("Salvar a cópia atual", capture),
                Caption("Clique no campo e pressione a combinação desejada. Esc cancela."),
                Separator(),
                Heading("Comportamento"),
                paste,
                _pasteHint,
                Separator(),
                Heading("Sistema"),
                startup,
                _startupError,
            },
        };
    }

    private void UpdatePasteHint() =>
        _pasteHint.Text = _preferences.PasteAutomatically
            ? "Shift+Enter continua apenas copiando."
            : "Enter apenas copia; você cola com Ctrl+V.";

    private static TextBlock Heading(string text) =>
        new() { Text = text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) };

    private static TextBlock Caption(string text) =>
        new() { Text = text, FontSize = 11, Opacity = 0.5, TextWrapping = TextWrapping.Wrap };

    private static Border Separator() =>
        new() { Height = 1, Background = new SolidColorBrush(Color.Parse("#28808080")), Margin = new Thickness(0, 6) };

    private static Grid Row(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }
}
