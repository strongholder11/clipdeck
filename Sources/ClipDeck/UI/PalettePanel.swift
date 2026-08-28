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
        // .canJoinAllSpaces faz a paleta aparecer no Espaço (Mesa) atual em vez
        // de arrastar o usuário para onde ela estava. .stationary foi removido
        // de propósito: ele marca a janela como parte da mesa de trabalho, o que
        // é semântica de papel de parede, não de painel flutuante.
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]

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

    /// Posiciona a paleta na tela em que o usuário está trabalhando.
    ///
    /// A escolha da tela é por foco de teclado, não pelo cursor do mouse. Num
    /// setup de dois monitores é comum digitar num enquanto o mouse está parado
    /// no outro — e seguir o mouse fazia a paleta abrir na tela errada, que era
    /// exatamente o sintoma: "apertei ⌥Espaço e não abriu na tela em que estou".
    ///
    /// `NSScreen.main` é a tela que contém a janela com o foco de teclado. Como
    /// isso é calculado antes de ativarmos o ClipDeck, ainda aponta para o app em
    /// que o usuário estava escrevendo.
    @discardableResult
    func positionOnActiveScreen() -> NSScreen? {
        let screen = NSScreen.main
            ?? NSScreen.screens.first { $0.frame.contains(NSEvent.mouseLocation) }
            ?? NSScreen.screens.first

        guard let screen else { return nil }

        let visible = screen.visibleFrame
        let size = frame.size
        let origin = NSPoint(
            x: visible.midX - size.width / 2,
            y: visible.maxY - size.height - visible.height * 0.18
        )
        setFrameOrigin(origin)
        return screen
    }

    /// Nome curto da tela, para diagnóstico.
    static func describe(_ screen: NSScreen?) -> String {
        guard let screen else { return "nenhuma" }
        let frame = screen.frame
        return "\(Int(frame.width))x\(Int(frame.height))@\(Int(frame.origin.x)),\(Int(frame.origin.y))"
    }
}
