import AppKit
import ClipDeckKit
import QuartzCore
import SwiftUI

/// Orquestra a paleta: exibição, teclado e o que acontece ao confirmar.
@MainActor
final class PaletteController: NSObject, NSWindowDelegate {
    private let store: Store
    private let model: PaletteViewModel
    private var panel: PalettePanel?
    private var keyMonitor: Any?

    /// Instante em que a paleta apareceu. Ver `windowDidResignKey`.
    private var shownAt: Date?

    /// Desliga o fechamento automático ao perder o foco. Só para inspecionar a
    /// UI durante o desenvolvimento (`--no-auto-hide`).
    var autoHideOnResignKey = true

    /// App que estava em foco quando a paleta abriu. Guardado na abertura porque
    /// depois que o painel assume o teclado essa informação já se perdeu — e é
    /// para esse app que o texto precisa voltar.
    private(set) var previousApplication: NSRunningApplication?

    /// Injetado pela etapa do colar automático. Enquanto não existe, a paleta
    /// apenas copia — por isso é opcional em vez de obrigatório.
    var pasteHandler: ((String, NSRunningApplication?) -> Void)?

    /// Abre o formulário de captura com o conteúdo da área de transferência.
    /// Recebe o texto a salvar; nil significa "use a área de transferência".
    var captureHandler: ((String?) -> Void)?

    /// Chamado quando o template tem variáveis a preencher. Recebe o template,
    /// o app de destino e um callback com o texto já renderizado.
    var fillHandler: ((Template, NSRunningApplication?, @escaping (String) -> Void) -> Void)?

    init(store: Store, clipboard: ClipboardWatcher? = nil) {
        self.store = store
        self.model = PaletteViewModel(store: store, clipboard: clipboard)
        super.init()
    }

    var isVisible: Bool { panel?.isVisible ?? false }

    func toggle() {
        isVisible ? hide() : show()
    }

    func show() {
        previousApplication = NSWorkspace.shared.frontmostApplication

        let panel = existingOrNewPanel()
        model.reset()
        model.refresh()

        panel.positionOnActiveScreen()
        activate(panel)
        shownAt = Date()

        installKeyMonitor()

        logState(panel, moment: "abertura")
        if Self.debugEnabled {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { [weak self] in
                self?.logState(panel, moment: "apos-0.8s")
            }
        }
    }

    func setQuery(_ text: String) {
        model.query = text
    }

    /// Confirma o item selecionado, como se você tivesse apertado ⏎.
    func commitSelection(paste: Bool) {
        guard let item = model.selectedItem else { return }
        commit(item, paste: paste)
    }

    func hide() {
        removeKeyMonitor()
        panel?.orderOut(nil)
        model.reset()
    }

    private func existingOrNewPanel() -> PalettePanel {
        if let panel { return panel }

        let hosting = NSHostingView(
            rootView: PaletteView(model: model) { [weak self] result, paste in
                self?.commit(result, paste: paste)
            }
        )
        let panel = PalettePanel(contentView: hosting)
        panel.delegate = self
        panel.onDismiss = { [weak self] in self?.hide() }
        self.panel = panel
        return panel
    }

    /// Traz o painel à frente e garante o foco de teclado.
    ///
    /// `orderFrontRegardless` exibe o painel mesmo com o app inativo, e o
    /// `activate` pede o foco. Do macOS 14 em diante o sistema pode recusar a
    /// ativação de um app em segundo plano, e aí o painel apareceria sem receber
    /// digitação — por isso a segunda tentativa no próximo ciclo do run loop,
    /// quando a transição de foco já terminou.
    private func activate(_ panel: PalettePanel) {
        // Aparece com um fade curto. Surgir instantaneamente sobre outro app
        // é visualmente abrupto; mais que ~0,1s começa a parecer lento num
        // atalho que você usa dezenas de vezes por dia.
        panel.alphaValue = 0
        NSApp.activate(ignoringOtherApps: true)
        panel.orderFrontRegardless()
        panel.makeKey()

        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.10
            context.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel.animator().alphaValue = 1
        }

        DispatchQueue.main.async { [weak self] in
            guard let self, panel.isVisible, !panel.isKeyWindow else { return }
            NSApp.activate(ignoringOtherApps: true)
            panel.makeKey()
            self.logState(panel, moment: "retentativa")
        }
    }

    static var debugEnabled: Bool {
        ProcessInfo.processInfo.environment["CLIPDECK_DEBUG"] != nil
            || CommandLine.arguments.contains("--debug")
    }

    /// Escreve em arquivo, e não em stderr, para funcionar também quando o app é
    /// aberto pelo Finder ou pelo `open`, onde não há terminal ligado.
    private func logState(_ panel: PalettePanel, moment: String) {
        guard Self.debugEnabled else { return }
        let line = "[\(moment)] visivel=\(panel.isVisible) key=\(panel.isKeyWindow)"
            + " frame=\(panel.frame) itens=\(model.items.count)"
            + " appAtivo=\(NSApp.isActive)\n"

        let url = LibraryStorage.defaultDirectory.appendingPathComponent("debug.log")
        if let handle = try? FileHandle(forWritingTo: url) {
            handle.seekToEndOfFile()
            handle.write(Data(line.utf8))
            try? handle.close()
        } else {
            try? Data(line.utf8).write(to: url)
        }
    }

    // MARK: - Confirmação

    private func commit(_ item: PaletteItem, paste: Bool) {
        switch item {
        case .template(let result):
            let template = result.template
            let target = previousApplication

            store.recordUse(of: template.id)
            hide()

            let pending = TemplateRenderer.fillableVariables(in: template.body)

            // Sem variáveis livres, cola direto — o caso comum não deve pagar
            // o custo de abrir um formulário vazio. Built-ins como {{data}}
            // ainda precisam passar pelo render.
            guard !pending.isEmpty else {
                let text = TemplateRenderer.render(
                    template.body,
                    values: [:],
                    clipboard: Clipboard.readText()
                )
                deliver(text, to: target, paste: paste)
                return
            }

            guard let fillHandler else {
                deliver(template.body, to: target, paste: paste)
                return
            }

            fillHandler(template, target) { [weak self] rendered in
                self?.deliver(rendered, to: target, paste: paste)
            }

        case .recentCopy(let entry):
            // Numa cópia recente a ação não é colar — é transformá-la em
            // template, que é o motivo de ela estar listada aqui.
            hide()
            captureHandler?(entry.text)
        }
    }

    /// Entrega o texto pronto: cola no app de destino ou apenas copia.
    private func deliver(_ text: String, to target: NSRunningApplication?, paste: Bool) {
        if paste, let pasteHandler {
            pasteHandler(text, target)
        } else {
            Clipboard.write(text)
        }
    }

    // MARK: - Teclado

    /// Intercepta as teclas de navegação antes do campo de busca.
    ///
    /// O campo de texto consome ↑/↓/⏎ por conta própria, então não dá para tratar
    /// isso pela view. O monitor local vê o evento primeiro e devolve nil nas
    /// teclas que nos interessam, engolindo-as; o resto segue para o campo e a
    /// digitação continua normal.
    private func installKeyMonitor() {
        guard keyMonitor == nil else { return }

        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self, self.isVisible else { return event }
            return self.handle(event) ? nil : event
        }
    }

    private func removeKeyMonitor() {
        if let keyMonitor { NSEvent.removeMonitor(keyMonitor) }
        keyMonitor = nil
    }

    private func handle(_ event: NSEvent) -> Bool {
        let modifiers = event.modifierFlags.intersection(.deviceIndependentFlagsMask)

        switch Int(event.keyCode) {
        case 53: // esc
            hide()
            return true

        case 125: // ↓
            model.moveSelection(by: 1)
            return true

        case 126: // ↑
            model.moveSelection(by: -1)
            return true

        case 36, 76: // return, enter do teclado numérico
            guard let item = model.selectedItem else { return true }
            commit(item, paste: !modifiers.contains(.shift))
            return true

        default:
            break
        }

        // ⌃N / ⌃P: navegação estilo Emacs, comum em paletas desse tipo.
        if modifiers == .control, let characters = event.charactersIgnoringModifiers {
            if characters == "n" { model.moveSelection(by: 1); return true }
            if characters == "p" { model.moveSelection(by: -1); return true }
        }

        // ⌘N: salva o que está na área de transferência sem sair da paleta.
        if modifiers == .command, event.charactersIgnoringModifiers == "n" {
            captureHandler?(nil)
            return true
        }

        // ⌘1–⌘9: escolhe direto pela posição.
        if modifiers == .command,
           let characters = event.charactersIgnoringModifiers,
           let digit = Int(characters), (1...9).contains(digit) {
            model.selectIndex(digit - 1)
            if let item = model.selectedItem {
                commit(item, paste: true)
            }
            return true
        }

        return false
    }

    // MARK: - NSWindowDelegate

    /// Clicar fora fecha a paleta, como qualquer painel desse tipo.
    ///
    /// A carência existe porque `activate` + `makeKeyAndOrderFront` não são
    /// atômicos: num Mac ocupado, o app que estava em foco pode reassumi-lo no
    /// meio da transição e disparar um resignKey espúrio, fechando a paleta no
    /// exato instante em que ela deveria abrir.
    func windowDidResignKey(_ notification: Notification) {
        guard autoHideOnResignKey else { return }

        if let shownAt, Date().timeIntervalSince(shownAt) < 0.35 {
            // Reafirma o foco em vez de fechar.
            DispatchQueue.main.async { [weak self] in
                guard let self, self.isVisible else { return }
                NSApp.activate(ignoringOtherApps: true)
                self.panel?.makeKeyAndOrderFront(nil)
            }
            return
        }

        hide()
    }
}
