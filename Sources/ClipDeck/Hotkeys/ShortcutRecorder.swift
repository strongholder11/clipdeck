import AppKit
import SwiftUI

/// Campo que grava uma combinação de teclas.
///
/// Precisa ser AppKit: em SwiftUI não há como interceptar um `keyDown` cru com os
/// modificadores antes do sistema interpretá-lo como um atalho.
struct ShortcutRecorder: NSViewRepresentable {
    @Binding var combo: KeyCombo

    func makeNSView(context: Context) -> RecorderView {
        let view = RecorderView()
        view.onCapture = { combo = $0 }
        view.combo = combo
        return view
    }

    func updateNSView(_ view: RecorderView, context: Context) {
        view.combo = combo
        view.needsDisplay = true
    }

    final class RecorderView: NSView {
        var onCapture: ((KeyCombo) -> Void)?
        var combo: KeyCombo?
        private var recording = false

        override var acceptsFirstResponder: Bool { true }
        override var intrinsicContentSize: NSSize { NSSize(width: 150, height: 24) }

        override func mouseDown(with event: NSEvent) {
            recording = true
            window?.makeFirstResponder(self)
            needsDisplay = true
        }

        override func resignFirstResponder() -> Bool {
            recording = false
            needsDisplay = true
            return true
        }

        override func keyDown(with event: NSEvent) {
            guard recording else { return super.keyDown(with: event) }

            // Esc cancela a gravação sem alterar o atalho.
            if event.keyCode == 53 {
                recording = false
                window?.makeFirstResponder(nil)
                needsDisplay = true
                return
            }

            let modifiers = KeyCombo.carbonModifiers(from: event.modifierFlags)

            // Um atalho global sem modificador sequestraria a tecla no sistema
            // inteiro — digitar "a" em qualquer lugar abriria a paleta.
            guard modifiers != 0 else { NSSound.beep(); return }

            let captured = KeyCombo(keyCode: UInt32(event.keyCode), carbonModifiers: modifiers)
            combo = captured
            onCapture?(captured)

            recording = false
            window?.makeFirstResponder(nil)
            needsDisplay = true
        }

        override func draw(_ dirtyRect: NSRect) {
            let path = NSBezierPath(roundedRect: bounds.insetBy(dx: 1, dy: 1), xRadius: 5, yRadius: 5)

            (recording ? NSColor.controlAccentColor.withAlphaComponent(0.15)
                       : NSColor.controlBackgroundColor).setFill()
            path.fill()

            (recording ? NSColor.controlAccentColor : NSColor.separatorColor).setStroke()
            path.lineWidth = recording ? 2 : 1
            path.stroke()

            let text = recording ? "Pressione as teclas…" : (combo?.displayString ?? "—")
            let attributes: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: 12),
                .foregroundColor: recording ? NSColor.controlAccentColor : NSColor.labelColor,
            ]
            let size = text.size(withAttributes: attributes)
            text.draw(
                at: NSPoint(x: bounds.midX - size.width / 2, y: bounds.midY - size.height / 2),
                withAttributes: attributes
            )
        }
    }
}
