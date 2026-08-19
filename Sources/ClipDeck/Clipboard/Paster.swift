import AppKit
import Carbon.HIToolbox

/// Cola texto no app que estava em foco antes da paleta abrir.
///
/// Não existe API para "inserir texto no app X". O caminho real é: escrever na
/// área de transferência, devolver o foco ao app e sintetizar ⌘V. Sintetizar
/// eventos de teclado exige permissão de Acessibilidade — é a única parte do
/// ClipDeck que exige, e quando falta, tudo degrada para simplesmente copiar.
enum Paster {
    /// Espera até o app anterior reassumir o foco. Colar antes disso manda o ⌘V
    /// para o app errado — normalmente o próprio ClipDeck, e nada acontece.
    private static let focusDelay: TimeInterval = 0.12

    /// Espera antes de devolver o conteúdo original ao clipboard. Precisa ser
    /// maior que o tempo do app de destino processar o ⌘V, senão ele cola o
    /// conteúdo antigo.
    private static let restoreDelay: TimeInterval = 0.45

    static var hasPermission: Bool {
        AXIsProcessTrusted()
    }

    /// Pede a permissão mostrando o diálogo do sistema.
    @discardableResult
    static func requestPermission() -> Bool {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true]
        return AXIsProcessTrustedWithOptions(options as CFDictionary)
    }

    static func openAccessibilitySettings() {
        let url = URL(
            string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
        )!
        NSWorkspace.shared.open(url)
    }

    /// Cola o texto no app indicado.
    ///
    /// Sem permissão, apenas copia e devolve `false` — quem chama decide o que
    /// dizer ao usuário. Nunca falha silenciosamente sem deixar o texto acessível.
    @discardableResult
    static func paste(_ text: String, into application: NSRunningApplication?) -> Bool {
        let snapshot = PasteboardSnapshot.capture()

        Clipboard.write(text)

        guard hasPermission else { return false }

        application?.activate()

        DispatchQueue.main.asyncAfter(deadline: .now() + focusDelay) {
            postCommandV()

            DispatchQueue.main.asyncAfter(deadline: .now() + restoreDelay) {
                snapshot.restore()
            }
        }
        return true
    }

    private static func postCommandV() {
        // .combinedSessionState considera o estado real dos modificadores; sem
        // isso, uma tecla ainda fisicamente pressionada (o ⌥ do atalho, por
        // exemplo) se mistura ao evento e vira ⌥⌘V no app de destino.
        guard let source = CGEventSource(stateID: .combinedSessionState) else { return }
        source.setLocalEventsFilterDuringSuppressionState(
            [.permitLocalMouseEvents, .permitSystemDefinedEvents],
            state: .eventSuppressionStateSuppressionInterval
        )

        let key = CGKeyCode(kVK_ANSI_V)
        guard let down = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: true),
              let up = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: false)
        else { return }

        down.flags = .maskCommand
        up.flags = .maskCommand

        down.post(tap: .cgAnnotatedSessionEventTap)
        up.post(tap: .cgAnnotatedSessionEventTap)
    }
}

/// Cópia do conteúdo da área de transferência, para restaurar depois de colar.
///
/// Guarda todos os tipos de cada item, não só texto: se você tinha uma imagem
/// copiada, ela volta como imagem em vez de sumir.
struct PasteboardSnapshot {
    private let items: [[NSPasteboard.PasteboardType: Data]]

    static func capture() -> PasteboardSnapshot {
        let contents = NSPasteboard.general.pasteboardItems ?? []
        let copied = contents.map { item in
            item.types.reduce(into: [NSPasteboard.PasteboardType: Data]()) { result, type in
                if let data = item.data(forType: type) { result[type] = data }
            }
        }
        return PasteboardSnapshot(items: copied)
    }

    func restore() {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()

        guard !items.isEmpty else { return }

        let restored = items.map { stored -> NSPasteboardItem in
            let item = NSPasteboardItem()
            for (type, data) in stored {
                item.setData(data, forType: type)
            }
            return item
        }
        pasteboard.writeObjects(restored)
    }
}
