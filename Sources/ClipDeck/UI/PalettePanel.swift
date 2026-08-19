import AppKit

/// A janela da paleta.
///
/// É um NSPanel, e não uma janela do SwiftUI, porque precisa de três coisas que
/// só o AppKit entrega: flutuar acima de apps em tela cheia, receber teclado sem
/// roubar o foco permanentemente do app anterior, e sumir ao perder o foco.
final class PalettePanel: NSPanel {
    var onDismiss: (() -> Void)?

    init(contentView: NSView) {
        super.init(
            contentRect: NSRect(x: 0, y: 0, width: 720, height: 460),
            styleMask: [.borderless, .nonactivatingPanel, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )

        self.contentView = contentView

        isOpaque = false
        backgroundColor = .clear
        hasShadow = true

        // .floating fica acima de janelas comuns; combinado com .fullScreenAuxiliary
        // aparece também sobre apps em tela cheia, que é onde esse tipo de painel
        // costuma falhar.
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]

        // Some junto com o app em vez de continuar visível numa troca de Espaço.
        hidesOnDeactivate = false
        isMovableByWindowBackground = false
        isReleasedWhenClosed = false
        animationBehavior = .utilityWindow
    }

    // Sem isso, uma janela .borderless nunca recebe teclado e o campo de busca
    // fica inerte.
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }

    /// Esc fecha, inclusive quando o campo de busca está com o foco.
    override func cancelOperation(_ sender: Any?) {
        onDismiss?()
    }

    /// Centraliza horizontalmente e posiciona no terço superior da tela ativa —
    /// mais perto da linha de visão do que o centro geométrico.
    func positionOnActiveScreen() {
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(NSEvent.mouseLocation) })
            ?? NSScreen.main else { return }

        let visible = screen.visibleFrame
        let size = frame.size
        let origin = NSPoint(
            x: visible.midX - size.width / 2,
            y: visible.maxY - size.height - visible.height * 0.18
        )
        setFrameOrigin(origin)
    }
}
