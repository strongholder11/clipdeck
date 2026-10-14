using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
    private Preferences? _preferences;
    private ClipboardWatcher? _clipboard;
    private IHotKeyService? _hotKeys;
    private IPasteService? _paste;
    private PaletteWindow? _palette;
    private LibraryWindow? _library;
    private PreferencesWindow? _preferencesWindow;
    private CaptureWindow? _capture;
    private TrayIcon? _tray;

    /// <summary>Atalhos que outro programa já ocupa, para avisar no menu da bandeja.</summary>
    private readonly List<string> _conflicts = new();

    private static readonly string[] Args = Environment.GetCommandLineArgs();

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
            _preferences = new Preferences(onWindows ? new WindowsStartupService() : new StubStartupService());
            _clipboard = new ClipboardWatcher();

            _palette = new PaletteWindow(new PaletteViewModel(_store, _clipboard));
            _palette.Committed += OnTemplateCommitted;
            _palette.CaptureRequested += text => CaptureClipboard(text);

            // Mudou o atalho nas Preferências: registra de novo na hora, sem
            // exigir reiniciar o app para o novo atalho valer.
            _preferences.ShortcutsChanged += RegisterHotKeys;

            SetupTray();
            RegisterHotKeys();
            HandleCommandLine();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // MARK: - Linha de comando

    /// <summary>
    /// Flags para automação e para inspecionar estados sem depender do atalho
    /// global — as mesmas da versão macOS.
    /// </summary>
    private void HandleCommandLine()
    {
        if (Args.Contains("--no-auto-hide") && _palette is not null) _palette.AutoHideOnDeactivate = false;
        if (Args.Contains("--preferences")) Dispatcher.UIThread.Post(ShowPreferences);
        if (Args.Contains("--library")) Dispatcher.UIThread.Post(ShowLibrary);
        if (Args.Contains("--capture")) Dispatcher.UIThread.Post(() => CaptureClipboard());

        if (!Args.Contains("--show-palette")) return;

        // --delay N adia a abertura, para inspecionar a paleta num estado que
        // depende de algo acontecer antes (uma cópia recente, por exemplo).
        var delay = ArgValue("--delay") is { } raw
                    && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

        DispatcherTimer.RunOnce(() =>
        {
            ShowPalette();

            // --query termo pré-preenche a busca. Precisa vir depois de mostrar,
            // que zera a consulta ao abrir.
            if (ArgValue("--query") is { } query) _palette?.SetQuery(query);

            // --commit confirma o item do topo, como um Shift+Enter.
            if (Args.Contains("--commit"))
            {
                DispatcherTimer.RunOnce(() => _palette?.CommitSelection(paste: false), TimeSpan.FromMilliseconds(300));
            }
        }, delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(1));
    }

    private static string? ArgValue(string flag)
    {
        var index = Array.IndexOf(Args, flag);
        return index >= 0 && index + 1 < Args.Length ? Args[index + 1] : null;
    }

    // MARK: - Atalhos

    private void RegisterHotKeys()
    {
        if (_hotKeys is null || _preferences is null) return;

        // Limpa antes de registrar: sem isso, trocar o atalho deixaria o antigo
        // ativo junto com o novo.
        _hotKeys.UnregisterAll();
        _conflicts.Clear();

        Register(_preferences.PaletteShortcut, ShowPalette);
        Register(_preferences.CaptureShortcut, () => CaptureClipboard());

        RebuildTrayMenu();
    }

    private void Register(KeyCombo combo, Action handler)
    {
        var ok = _hotKeys!.Register(combo, handler);
        DiagnosticsLog.Debug($"[atalho] {combo.DisplayString} registrado={ok}");
        if (!ok) _conflicts.Add(combo.DisplayString);
    }

    /// <summary>
    /// Enquanto um atalho é gravado nas Preferências, os globais ficam desligados:
    /// senão, pressionar a combinação atual abriria a paleta em vez de gravá-la.
    /// </summary>
    private void SuspendHotKeys(bool suspend)
    {
        if (suspend) _hotKeys?.UnregisterAll();
        else RegisterHotKeys();
    }

    // MARK: - Paleta

    private void ShowPalette()
    {
        // Pega uma cópia feita há menos de meio segundo, antes do próximo ciclo.
        _clipboard?.Poll();

        // Mostrar de novo com a paleta já na frente fecha, como um interruptor.
        if (_palette?.IsShown == true)
        {
            _palette.Hide();
            return;
        }

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
        fill.Activate();
    }

    private void Deliver(string text, bool paste)
    {
        ClipboardHelper.WriteText(text);

        // Preferência desligada: fica na área de transferência e para por aí.
        if (!paste || _preferences?.PasteAutomatically == false) return;

        if (_paste?.Paste(text) == false && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ = MessageDialog.Show("O texto foi copiado, mas não colado",
                "Não encontrei o programa onde você estava escrevendo. "
                + "O texto já está na área de transferência: é só apertar Ctrl+V.");
        }
    }

    // MARK: - Captura

    /// <summary>Salva um texto como template — por padrão, o da área de transferência.</summary>
    private void CaptureClipboard(string? explicitText = null)
    {
        _palette?.Hide();
        _clipboard?.Poll();

        // Prefere o que o observador registrou: se o usuário acabou de colar um
        // template, a área de transferência contém o template, não a última
        // cópia dele.
        var text = explicitText ?? _clipboard?.MostRecent?.Text ?? ClipboardHelper.ReadText();
        if (string.IsNullOrWhiteSpace(text))
        {
            var shortcut = _preferences?.CaptureShortcut.DisplayString ?? KeyCombo.CaptureClipboard.DisplayString;
            _ = MessageDialog.Show("Nada para salvar",
                $"Copie um texto com Ctrl+C antes de usar {shortcut}.");
            return;
        }

        if (_store is null) return;
        _capture?.Close();
        _capture = new CaptureWindow(_store, text, startWithNewFolder: Args.Contains("--new-folder"));
        _capture.Closed += (_, _) => _capture = null;
        _capture.Show();
        _capture.Activate();
    }

    // MARK: - Janelas

    private void ShowLibrary()
    {
        if (_store is null) return;
        if (_library is null)
        {
            _library = new LibraryWindow(_store);
            _library.Closed += (_, _) => _library = null;
            _library.Show();
        }
        _library.WindowState = WindowState.Normal;
        _library.Activate();
    }

    private void ShowPreferences()
    {
        if (_preferences is null) return;
        if (_preferencesWindow is null)
        {
            _preferencesWindow = new PreferencesWindow(_preferences, SuspendHotKeys);
            _preferencesWindow.Closed += (_, _) => _preferencesWindow = null;
            _preferencesWindow.Show();
        }
        _preferencesWindow.Activate();
    }

    // MARK: - Exportar e importar

    private async Task ExportLibrary()
    {
        if (_store is null) return;

        var file = await DialogHost.Run(storage => storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Escolha onde salvar a cópia dos seus templates",
            SuggestedFileName = "clipdeck-templates.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { DialogHost.Json },
            ShowOverwritePrompt = true,
        }));
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(LibraryTransfer.Serialize(_store.Library));
        }
        catch (Exception ex)
        {
            await MessageDialog.Show("Não foi possível exportar", ex.Message);
        }
    }

    private async Task ImportLibrary()
    {
        if (_store is null) return;

        var files = await DialogHost.Run(storage => storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolha um arquivo de templates do ClipDeck",
            AllowMultiple = false,
            FileTypeFilter = new[] { DialogHost.Json, FilePickerFileTypes.All },
        }));
        if (files.Count == 0) return;

        Library imported;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            imported = LibraryTransfer.Deserialize(await reader.ReadToEndAsync());
        }
        catch (Exception ex)
        {
            await MessageDialog.Show("Arquivo inválido",
                $"Não parece um arquivo de templates do ClipDeck.\n\n{ex.Message}");
            return;
        }

        // Importar pode substituir tudo, então pergunta antes — e oferece
        // adicionar, que é o que a maioria quer ao trazer templates de outra máquina.
        var choice = await MessageDialog.Show(
            $"Importar {imported.Templates.Count} templates?",
            $"Você tem {_store.Templates.Count} templates agora.",
            "Adicionar aos meus", "Substituir tudo", "Cancelar");

        switch (choice)
        {
            case 0:
                _store.ReplaceLibrary(LibraryTransfer.Merge(_store.Library, imported));
                break;
            case 1:
                if (await MessageDialog.Confirm("Substituir todos os templates?",
                        $"Os {_store.Templates.Count} templates atuais serão trocados pelos do arquivo. "
                        + "Uma cópia do estado anterior fica em Backups.",
                        "Substituir", "Cancelar") == 0)
                {
                    _store.ReplaceLibrary(imported);
                }
                break;
        }

        if (choice is 0 or 1) _store.Flush();
    }

    // MARK: - Bandeja

    private void SetupTray()
    {
        _tray = new TrayIcon
        {
            ToolTipText = "ClipDeck",
            Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://ClipDeck/Assets/clipdeck.png"))),
        };

        // Clique no ícone abre a paleta; o botão direito mostra o menu.
        _tray.Clicked += (_, _) => ShowPalette();

        RebuildTrayMenu();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void RebuildTrayMenu()
    {
        if (_tray is null || _preferences is null) return;

        var menu = new NativeMenu();

        foreach (var conflict in _conflicts)
        {
            menu.Items.Add(new NativeMenuItem($"⚠ {conflict} já está em uso por outro programa") { IsEnabled = false });
        }
        if (_conflicts.Count > 0)
        {
            var fix = new NativeMenuItem("    Trocar o atalho nas Preferências…");
            fix.Click += (_, _) => ShowPreferences();
            menu.Items.Add(fix);
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(Item("Abrir paleta", ShowPalette));
        menu.Items.Add(new NativeMenuItem($"    {_preferences.PaletteShortcut.DisplayString}") { IsEnabled = false });
        menu.Items.Add(Item("Biblioteca de templates…", ShowLibrary));
        menu.Items.Add(new NativeMenuItem(
            $"Salvar cópia como template: {_preferences.CaptureShortcut.DisplayString}") { IsEnabled = false });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Exportar templates…", () => _ = ExportLibrary()));
        menu.Items.Add(Item("Importar templates…", () => _ = ImportLibrary()));
        menu.Items.Add(Item("Preferências…", ShowPreferences));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("Sair do ClipDeck", () =>
        {
            Shutdown();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        }));

        _tray.Menu = menu;
        _tray.ToolTipText = _conflicts.Count > 0 ? "ClipDeck — atalho em uso por outro programa" : "ClipDeck";
    }

    private static NativeMenuItem Item(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private void Shutdown()
    {
        _hotKeys?.Dispose();
        _store?.Flush();
        _tray?.Dispose();
    }
}
