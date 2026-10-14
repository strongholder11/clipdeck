using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClipDeck.App.Services;
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

    /// <summary>
    /// Ctrl+N: salvar a área de transferência como template. Também usado ao
    /// confirmar uma cópia recente, com o texto dela — nesse caso a ação não é
    /// colar, e sim transformá-la em template, que é o motivo de ela estar ali.
    /// </summary>
    public event Action<string?>? CaptureRequested;

    /// <summary>
    /// Desliga o fechamento ao perder o foco. Só para inspecionar a interface
    /// durante o desenvolvimento (--no-auto-hide).
    /// </summary>
    public bool AutoHideOnDeactivate { get; set; } = true;

    /// <summary>Instante em que a paleta apareceu. Ver o tratamento de Deactivated.</summary>
    private DateTimeOffset? _shownAt;

    public bool IsShown => IsVisible && IsActive;

    public PaletteWindow(PaletteViewModel model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();

        var box = this.FindControl<TextBox>("SearchBox");
        Opened += (_, _) => Dispatcher.UIThread.Post(() => box?.Focus());

        // Clicar fora fecha, como qualquer painel desse tipo.
        //
        // A carência existe porque mostrar e ativar não são atômicos: com o
        // sistema ocupado, o programa que estava em foco pode reassumi-lo no meio
        // da transição e disparar um Deactivated espúrio, fechando a paleta no
        // exato instante em que ela deveria abrir.
        Deactivated += (_, _) =>
        {
            if (!AutoHideOnDeactivate) return;
            if (_shownAt is { } shown && DateTimeOffset.Now - shown < TimeSpan.FromMilliseconds(350))
            {
                Dispatcher.UIThread.Post(() => { if (IsVisible) Activate(); });
                return;
            }
            Hide();
        };

        var list = this.FindControl<ListBox>("ResultsList");
        if (list is not null)
        {
            // Um clique confirma, como na versão macOS; passar o mouse seleciona.
            list.Tapped += (_, e) =>
            {
                // Só clique numa linha: a barra de rolagem também gera Tapped.
                if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null)
                    CommitSelection(paste: true);
            };
            list.AddHandler(PointerMovedEvent, (_, e) =>
            {
                if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } item
                    && list.IndexFromContainer(item) is var index and >= 0
                    && index != _model.SelectedIndex)
                {
                    _model.SelectedIndex = index;
                }
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    public void SetQuery(string text) => _model.Query = text;

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
                CaptureRequested?.Invoke(null);
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

    /// <summary>Confirma o item selecionado, como um Enter.</summary>
    public void CommitSelection(bool paste)
    {
        if (_model.SelectedRow is not { } row) return;
        Hide();

        if (row.RecentCopy is { } entry) CaptureRequested?.Invoke(entry.Text);
        else if (row.Template is { } template) Committed?.Invoke(template, paste);
    }

    /// <summary>Mostra a paleta na tela em que o usuário está trabalhando.</summary>
    public void ShowPalette()
    {
        _model.Reset();
        PositionOnActiveScreen();

        _shownAt = DateTimeOffset.Now;
        Show();
        Activate();

        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("SearchBox")?.Focus());

        DiagnosticsLog.Palette($"abertura visivel={IsVisible} ativa={IsActive} itens={_model.Rows.Count}");
        DispatcherTimer.RunOnce(
            () => DiagnosticsLog.Palette($"apos-0.8s visivel={IsVisible} ativa={IsActive}"),
            TimeSpan.FromMilliseconds(800));
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
