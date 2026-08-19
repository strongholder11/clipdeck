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

        self.window = window
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {
        window = nil
    }
}
