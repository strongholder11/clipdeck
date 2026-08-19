import AppKit
import ClipDeckKit
import SwiftUI

/// Janela de preenchimento de variáveis.
@MainActor
final class FillController: NSObject, NSWindowDelegate {
    private var window: NSWindow?

    /// Abre o formulário. `onConfirm` recebe o texto já renderizado.
    func present(
        templateTitle: String,
        body: String,
        variables: [TemplateVariable],
        clipboardText: String?,
        onConfirm: @escaping (String) -> Void
    ) {
        close()

        let view = FillView(
            templateTitle: templateTitle,
            body_: body,
            variables: variables,
            clipboardText: clipboardText,
            onConfirm: { [weak self] rendered in
                self?.close()
                // Fecha antes de colar: a janela precisa sair do caminho para o
                // app de destino reassumir o foco e receber o ⌘V.
                onConfirm(rendered)
            },
            onCancel: { [weak self] in self?.close() }
        )

        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = templateTitle
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.level = .floating
        window.center()

        self.window = window
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func close() {
        window?.orderOut(nil)
        window = nil
    }

    func windowWillClose(_ notification: Notification) {
        window = nil
    }
}
