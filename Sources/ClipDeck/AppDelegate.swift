import AppKit
import ClipDeckKit
import Combine

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var store: Store!
    private var palette: PaletteController!
    private var statusItem: StatusItemController!
    private var capture: CaptureController!
    private var clipboard: ClipboardWatcher!
    private var fill: FillController!
    private var library: LibraryController!
    private var preferences: Preferences!
    private var preferencesWindow: PreferencesController!
    private var hotKeyIDs: [UInt32] = []
    private var cancellables: Set<AnyCancellable> = []

    // main.swift é código de topo, fora do MainActor, então o init precisa ser
    // não-isolado. As dependências são criadas depois, já no main thread, quando
    // o AppKit chama applicationDidFinishLaunching.
    nonisolated override init() { super.init() }

    func applicationDidFinishLaunching(_ notification: Notification) {
        store = Store()
        clipboard = ClipboardWatcher()
        palette = PaletteController(store: store, clipboard: clipboard)
        capture = CaptureController(store: store)
        fill = FillController()
        library = LibraryController(store: store)
        preferences = Preferences()
        preferencesWindow = PreferencesController(preferences: preferences)
        statusItem = StatusItemController(
            openPalette: { [weak self] in self?.palette.show() },
            openLibrary: { [weak self] in self?.library.show() },
            openPreferences: { [weak self] in self?.preferencesWindow.show() },
            exportLibrary: { [weak self] in self?.library.exportLibrary() },
            importLibrary: { [weak self] in self?.library.importLibrary() }
        )

        // A paleta não conhece Acessibilidade nem CGEvent: ela pede "cole isso
        // ali" e o AppDelegate decide como. Sem a permissão, o Paster copia e
        // devolve false, e aí avisamos em vez de deixar o usuário no escuro.
        palette.pasteHandler = { [weak self] text, target in
            guard let self else { return }

            // Preferência desligada: escreve no clipboard e para por aí.
            guard self.preferences.pasteAutomatically else {
                Clipboard.write(text)
                return
            }

            let pasted = Paster.paste(text, into: target)
            if !pasted {
                self.statusItem.reportMissingAccessibilityPermission()
            }
        }

        // ⌘N na paleta salva o que está na área de transferência.
        palette.captureHandler = { [weak self] text in self?.captureClipboard(text: text) }

        palette.fillHandler = { [weak self] template, _, deliver in
            self?.fill.present(
                templateTitle: template.title,
                body: template.body,
                variables: TemplateRenderer.fillableVariables(in: template.body),
                clipboardText: Clipboard.readText(),
                onConfirm: deliver
            )
        }

        registerHotKeys()
        statusItem.setAccessibilityGranted(Paster.hasPermission)
        requestAccessibilityOnFirstRun()

        // Mudou o atalho nas Preferências, re-registra na hora — sem exigir
        // que o usuário reinicie o app para o novo atalho valer.
        preferences.$paletteShortcut
            .dropFirst()
            .sink { [weak self] _ in self?.registerHotKeys() }
            .store(in: &cancellables)

        preferences.$captureShortcut
            .dropFirst()
            .sink { [weak self] _ in self?.registerHotKeys() }
            .store(in: &cancellables)

        // Abre a paleta já na inicialização. Serve para inspecionar a UI sem
        // depender do atalho global — útil em teste e em automações externas
        // (Atalhos, Raycast) via `open -a ClipDeck --args --show-palette`.
        if CommandLine.arguments.contains("--no-auto-hide") {
            palette.autoHideOnResignKey = false
        }

        if CommandLine.arguments.contains("--preferences") {
            preferencesWindow.show()
        }

        if CommandLine.arguments.contains("--library") {
            library.show()
        }

        if CommandLine.arguments.contains("--capture") {
            captureClipboard()
        }

        if CommandLine.arguments.contains("--show-palette") {
            // `--delay N` adia a abertura, para inspecionar a paleta num estado
            // que depende de algo acontecer antes (uma cópia recente, por exemplo).
            var delay: TimeInterval = 0
            if let index = CommandLine.arguments.firstIndex(of: "--delay"),
               CommandLine.arguments.indices.contains(index + 1),
               let seconds = TimeInterval(CommandLine.arguments[index + 1]) {
                delay = seconds
            }

            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                guard let self else { return }
                self.palette.show()

                // `--query termo` pré-preenche a busca. Precisa vir depois do
                // show(), que zera a consulta ao abrir.
                if let index = CommandLine.arguments.firstIndex(of: "--query"),
                   CommandLine.arguments.indices.contains(index + 1) {
                    self.palette.setQuery(CommandLine.arguments[index + 1])
                }

                // `--commit` confirma o item do topo, como um ⏎.
                if CommandLine.arguments.contains("--commit") {
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
                        self.palette.commitSelection(paste: false)
                    }
                }
            }
        }
    }

    private func logHotKey(_ combo: KeyCombo, registered: Bool) {
        guard PaletteController.debugEnabled else { return }
        let line = "[atalho] \(combo.displayString) registrado=\(registered)\n"
        let url = LibraryStorage.defaultDirectory.appendingPathComponent("debug.log")
        if let handle = try? FileHandle(forWritingTo: url) {
            handle.seekToEndOfFile()
            handle.write(Data(line.utf8))
            try? handle.close()
        } else {
            try? Data(line.utf8).write(to: url)
        }
    }

    private func registerHotKeys() {
        // Limpa antes de registrar: sem isso, trocar o atalho deixaria o
        // antigo ativo junto com o novo.
        hotKeyIDs.forEach { HotKeyManager.shared.unregister($0) }
        hotKeyIDs = []

        if let id = HotKeyManager.shared.register(preferences.paletteShortcut, handler: { [weak self] in
            self?.palette.toggle()
        }) {
            hotKeyIDs.append(id)
            logHotKey(preferences.paletteShortcut, registered: true)
        } else {
            logHotKey(preferences.paletteShortcut, registered: false)
            statusItem.reportHotKeyConflict(preferences.paletteShortcut.displayString)
        }

        if let id = HotKeyManager.shared.register(preferences.captureShortcut, handler: { [weak self] in
            self?.captureClipboard()
        }) {
            hotKeyIDs.append(id)
            logHotKey(preferences.captureShortcut, registered: true)
        } else {
            logHotKey(preferences.captureShortcut, registered: false)
            statusItem.reportHotKeyConflict(preferences.captureShortcut.displayString)
        }
    }

    /// Pede a permissão de Acessibilidade uma única vez, na primeira execução.
    ///
    /// O diálogo do sistema já adiciona o app à lista dos Ajustes, poupando o
    /// usuário de encontrá-lo e arrastá-lo à mão. Pedir só uma vez é essencial:
    /// quem prefere apenas copiar não pode ser perguntado a cada abertura.
    private func requestAccessibilityOnFirstRun() {
        let key = "hasRequestedAccessibility"
        guard !UserDefaults.standard.bool(forKey: key), !Paster.hasPermission else { return }

        UserDefaults.standard.set(true, forKey: key)
        Paster.requestPermission()
    }

    /// Salva o conteúdo atual da área de transferência como template.
    private func captureClipboard(text explicit: String? = nil) {
        palette.hide()

        // Prefere o que o observador registrou: se o usuário acabou de colar um
        // template, o clipboard contém o template, não a última cópia dele.
        let text = explicit ?? clipboard.mostRecent?.text ?? Clipboard.readText()

        if !capture.capture(text: text) {
            statusItem.reportEmptyClipboard()
        }
    }

    /// Grava antes de encerrar: o debounce de 500 ms pode não ter disparado.
    func applicationWillTerminate(_ notification: Notification) {
        store.flush()
    }
}
