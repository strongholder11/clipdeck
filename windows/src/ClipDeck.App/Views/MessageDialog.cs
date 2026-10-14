using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ClipDeck.App.Views;

/// <summary>
/// Aviso com botões, equivalente ao NSAlert da versão macOS.
///
/// O Avalonia não tem caixa de mensagem pronta, e um app que vive na bandeja
/// precisa de um jeito visível de avisar — antes os avisos iam só para o console,
/// onde ninguém os via.
/// </summary>
public sealed class MessageDialog : Window
{
    private readonly TaskCompletionSource<int> _result = new();
    private int _choice = -1;

    /// <param name="buttons">O primeiro é o padrão (Enter); Esc devolve -1.</param>
    private MessageDialog(string title, string message, IReadOnlyList<string> buttons, bool destructiveFirst)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        // Exibidos da direita para a esquerda: o padrão fica na ponta, como no Windows.
        for (var i = buttons.Count - 1; i >= 0; i--)
        {
            var index = i;
            var button = new Button { Content = buttons[i], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (i == 0)
            {
                button.IsDefault = true;
                button.Classes.Add("accent");
                if (destructiveFirst) button.Foreground = new SolidColorBrush(Color.Parse("#D9443A"));
            }
            button.Click += (_, _) => { _choice = index; Close(); };
            buttonRow.Children.Add(button);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(20, 18),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = message, FontSize = 13, Opacity = 0.8, TextWrapping = TextWrapping.Wrap,
                                IsVisible = message.Length > 0 },
                new Border { Height = 6 },
                buttonRow,
            },
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { _choice = -1; Close(); e.Handled = true; }
        };
        Closed += (_, _) => _result.TrySetResult(_choice);
    }

    /// <summary>Mostra e devolve o índice do botão escolhido (-1 se fechou).</summary>
    public static Task<int> Show(string title, string message, params string[] buttons) =>
        ShowCore(title, message, buttons, destructiveFirst: false);

    /// <summary>Como <see cref="Show"/>, com o primeiro botão em vermelho.</summary>
    public static Task<int> Confirm(string title, string message, params string[] buttons) =>
        ShowCore(title, message, buttons, destructiveFirst: true);

    /// <summary>Cria sem mostrar — usado pela ferramenta de snapshots.</summary>
    public static MessageDialog Build(string title, string message, params string[] buttons) =>
        new(title, message, buttons.Length == 0 ? new[] { "OK" } : buttons, destructiveFirst: false);

    private static Task<int> ShowCore(string title, string message, string[] buttons, bool destructiveFirst)
    {
        var dialog = new MessageDialog(title, message, buttons.Length == 0 ? new[] { "OK" } : buttons, destructiveFirst);
        dialog.Show();
        dialog.Activate();
        return dialog._result.Task;
    }
}
