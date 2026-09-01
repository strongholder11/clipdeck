using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipDeck.App.Platform;
using ClipDeck.App.Platform.Windows;
using ClipDeck.App.Services;
using ClipDeck.App.ViewModels;
using ClipDeck.App.Views;
using ClipDeck.Core.Models;
using ClipDeck.Core.Variables;

namespace ClipDeck.App;

public partial class App : Application
{
    private TemplateStore? _store;
    private IHotKeyService? _hotKeys;
    private IPasteService? _paste;
    private PaletteWindow? _palette;
    private TrayIcon? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Sem janela principal: o app vive na bandeja e só aparece sob demanda.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Shutdown();

            _store = new TemplateStore();

            var onWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _hotKeys = onWindows ? new WindowsHotKeyService() : new StubHotKeyService();
            _paste = onWindows ? new WindowsPasteService() : new StubPasteService();

            _palette = new PaletteWindow(new PaletteViewModel(_store));
            _palette.Committed += OnTemplateCommitted;
            _palette.CaptureRequested += CaptureClipboard;

            SetupTray();
            RegisterHotKeys();

            // Permite inspecionar a paleta sem depender do atalho global — é o
            // que torna possível verificar a interface fora do Windows.
            if (Environment.GetCommandLineArgs().Contains("--show-palette"))
            {
                Dispatcher.UIThread.Post(ShowPalette);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterHotKeys()
    {
        if (_hotKeys is null) return;

        if (!_hotKeys.Register(KeyCombo.OpenPalette, ShowPalette))
        {
            Notify("Atalho em uso",
                $"{KeyCombo.OpenPalette.DisplayString} já está sendo usado por outro programa. "
                + "Você ainda pode abrir a paleta pelo ícone da bandeja.");
        }

        _hotKeys.Register(KeyCombo.CaptureClipboard, CaptureClipboard);
    }

    private void ShowPalette()
    {
        // Guarda a janela em foco antes de mostrarmos a nossa: é para ela que o
        // texto precisa voltar depois.
        _paste?.RememberForegroundWindow();
        _palette?.ShowPalette();
    }

    private void OnTemplateCommitted(Template template, bool paste)
    {
        _store?.RecordUse(template.Id);

        var pending = TemplateRenderer.FillableVariables(template.Body);
        var clipboard = ClipboardHelper.ReadText();

        if (pending.Count == 0)
        {
            // Sem variáveis livres, entrega direto — o caso comum não deve pagar
            // o custo de abrir um formulário vazio. Built-ins ainda são resolvidas.
            Deliver(TemplateRenderer.Render(template.Body, new Dictionary<string, string>(),
                                            clipboard), paste);
            return;
        }

        var fill = new FillWindow(template, pending, clipboard);
        fill.Confirmed += rendered => Deliver(rendered, paste);
        fill.Show();
    }

    private void Deliver(string text, bool paste)
    {
        ClipboardHelper.WriteText(text);

        if (!paste) return;
        if (_paste?.Paste(text) == false && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Notify("Copiado", "Não consegui colar automaticamente. Use Ctrl+V.");
        }
    }

    private void CaptureClipboard()
    {
        _palette?.Hide();

        var text = ClipboardHelper.ReadText();
        if (string.IsNullOrWhiteSpace(text))
        {
            Notify("Nada para salvar",
                   $"Copie um texto com Ctrl+C antes de usar {KeyCombo.CaptureClipboard.DisplayString}.");
            return;
        }

        if (_store is null) return;
        var capture = new CaptureWindow(_store, text);
        capture.Show();
    }

    private void SetupTray()
    {
        var openPalette = new NativeMenuItem("Abrir paleta");
        openPalette.Click += (_, _) => ShowPalette();

        var library = new NativeMenuItem("Biblioteca de templates…");
        library.Click += (_, _) =>
        {
            if (_store is not null) new LibraryWindow(_store).Show();
        };

        var quit = new NativeMenuItem("Sair do ClipDeck");
        quit.Click += (_, _) =>
        {
            Shutdown();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        };

        _tray = new TrayIcon
        {
            ToolTipText = "ClipDeck",
            Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://ClipDeck/Assets/clipdeck.png"))),
            Menu = new NativeMenu { Items = { openPalette, library, new NativeMenuItemSeparator(), quit } },
        };

        var icons = new TrayIcons { _tray };
        TrayIcon.SetIcons(this, icons);
    }

    private void Notify(string title, string message)
    {
        // A bandeja é o único canal sempre disponível num app sem janela principal.
        Console.WriteLine($"[clipdeck] {title}: {message}");
        if (_tray is not null) _tray.ToolTipText = $"ClipDeck — {title}";
    }

    private void Shutdown()
    {
        _hotKeys?.Dispose();
        _store?.Flush();
        _tray?.Dispose();
    }
}
