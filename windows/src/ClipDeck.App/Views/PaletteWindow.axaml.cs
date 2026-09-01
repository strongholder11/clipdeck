using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipDeck.App.ViewModels;
using ClipDeck.Core.Models;

namespace ClipDeck.App.Views;

public partial class PaletteWindow : Window
{
    private readonly PaletteViewModel _model;

    /// <summary>Confirmação: o template escolhido e se deve colar ou só copiar.</summary>
    public event Action<Template, bool>? Committed;

    /// <summary>Ctrl+N: salvar a área de transferência como template.</summary>
    public event Action? CaptureRequested;

    public PaletteWindow(PaletteViewModel model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();

        var box = this.FindControl<TextBox>("SearchBox");
        Opened += (_, _) => Dispatcher.UIThread.Post(() => box?.Focus());

        // Clicar fora fecha, como qualquer painel desse tipo.
        Deactivated += (_, _) => Hide();

        var list = this.FindControl<ListBox>("ResultsList");
        if (list is not null)
        {
            list.DoubleTapped += (_, _) => CommitSelection(paste: true);
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// As teclas de navegação são tratadas na janela, com Handled, para o campo de
    /// busca não consumi-las antes: setas e Enter pertencem à lista, não ao texto.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                return;

            case Key.Down:
                _model.MoveSelection(1);
                ScrollToSelection();
                e.Handled = true;
                return;

            case Key.Up:
                _model.MoveSelection(-1);
                ScrollToSelection();
                e.Handled = true;
                return;

            case Key.Enter:
                CommitSelection(paste: !e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                e.Handled = true;
                return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+N / Ctrl+P: navegação alternativa, comum em paletas.
            if (e.Key == Key.P) { _model.MoveSelection(-1); ScrollToSelection(); e.Handled = true; return; }

            if (e.Key == Key.N)
            {
                CaptureRequested?.Invoke();
                e.Handled = true;
                return;
            }

            // Ctrl+1..9: escolhe direto pela posição.
            if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                var index = e.Key - Key.D1;
                if (index < _model.Rows.Count)
                {
                    _model.SelectedIndex = index;
                    CommitSelection(paste: true);
                }
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    private void ScrollToSelection()
    {
        // Sem isso, descer com a seta passa do fim da área visível e o usuário
        // seleciona algo que não está na tela.
        var list = this.FindControl<ListBox>("ResultsList");
        if (list is not null && _model.SelectedIndex >= 0)
        {
            list.ScrollIntoView(_model.SelectedIndex);
        }
    }

    private void CommitSelection(bool paste)
    {
        if (_model.SelectedRow is not { } row) return;
        Hide();
        Committed?.Invoke(row.Template, paste);
    }

    /// <summary>Mostra a paleta na tela em que o usuário está trabalhando.</summary>
    public void ShowPalette()
    {
        _model.Reset();
        PositionOnActiveScreen();

        Show();
        Activate();

        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("SearchBox")?.Focus());
    }

    /// <summary>
    /// Escolhe a tela pelo ponteiro apenas como último recurso.
    ///
    /// A versão macOS tinha um bug exatamente aqui: escolhia pelo cursor do mouse,
    /// e num setup de dois monitores é comum digitar num enquanto o mouse está
    /// parado no outro — a paleta abria fora do campo de visão. Preferimos a tela
    /// da janela ativa, que é onde o usuário está de fato trabalhando.
    /// </summary>
    private void PositionOnActiveScreen()
    {
        var screen = Screens.ScreenFromWindow(this)
                     ?? Screens.ScreenFromPoint(default)
                     ?? Screens.Primary;
        if (screen is null) return;

        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var width = (int)(Width * scale);
        var height = (int)(Height * scale);

        Position = new Avalonia.PixelPoint(
            area.X + (area.Width - width) / 2,
            // Terço superior: mais perto da linha de visão que o centro geométrico.
            area.Y + (int)(area.Height * 0.18));
    }
}
