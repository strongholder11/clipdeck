import AppKit
import SwiftUI

@MainActor
final class PreferencesController: NSObject, NSWindowDelegate {
    private let preferences: Preferences
    private var window: NSWindow?

    init(preferences: Preferences) {
        self.preferences = preferences
        super.init()
    }

    func show() {
        if let window {
            NSApp.activate(ignoringOtherApps: true)
            window.makeKeyAndOrderFront(nil)
            return
        }

        let view = PreferencesView(
            preferences: preferences,
            accessibilityGranted: Paster.hasPermission,
            onOpenAccessibilitySettings: {
                Paster.requestPermission()
                Paster.openAccessibilitySettings()
            }
        )

        let window = NSWindow(contentViewController: NSHostingController(rootView: view))
        window.title = "Preferências do ClipDeck"
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        // Vem para o Espaço (Mesa) atual em vez de o sistema levar o usuário até
        // ela. Sem isso, ativar o ClipDeck com esta janela aberta em outra Mesa
        // faz o macOS trocar de Mesa, o que é desorientador com dois monitores.
        window.collectionBehavior = [.moveToActiveSpace, .fullScreenAuxiliary]

        self.window = window
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {
        window = nil
    }
}
