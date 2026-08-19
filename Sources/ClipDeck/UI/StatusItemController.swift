import AppKit
import SwiftUI

/// Ícone na barra de menu e seu menu suspenso.
@MainActor
final class StatusItemController {
    private let item: NSStatusItem
    private let openPalette: () -> Void
    private let openLibrary: () -> Void
    private let openPreferences: () -> Void
    private let exportLibrary: () -> Void
    private let importLibrary: () -> Void
    private var conflictMessage: String?
    private var accessibilityGranted = true

    init(
        openPalette: @escaping () -> Void,
        openLibrary: @escaping () -> Void,
        openPreferences: @escaping () -> Void,
        exportLibrary: @escaping () -> Void,
        importLibrary: @escaping () -> Void
    ) {
        self.openPalette = openPalette
        self.openLibrary = openLibrary
        self.openPreferences = openPreferences
        self.exportLibrary = exportLibrary
        self.importLibrary = importLibrary
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)

        if let button = item.button {
            button.image = NSImage(
                systemSymbolName: "text.badge.plus",
                accessibilityDescription: "ClipDeck"
            )
            button.image?.isTemplate = true
        }

        rebuildMenu()
    }

    /// A área de transferência está vazia ou só tem espaços em branco.
    func reportEmptyClipboard() {
        let alert = NSAlert()
        alert.messageText = "Nada para salvar"
        alert.informativeText = "Copie um texto com ⌘C antes de usar "
            + KeyCombo.captureClipboard.displayString + "."
        alert.alertStyle = .informational
        alert.addButton(withTitle: "OK")

        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }

    func setAccessibilityGranted(_ granted: Bool) {
        accessibilityGranted = granted
        rebuildMenu()
    }

    /// Chamado quando uma colagem falhou por falta de permissão. O texto já
    /// está na área de transferência nesse ponto, então o aviso é sobre o
    /// automático não ter acontecido — não sobre ter perdido o conteúdo.
    func reportMissingAccessibilityPermission() {
        accessibilityGranted = false
        rebuildMenu()

        let alert = NSAlert()
        alert.messageText = "O texto foi copiado, mas não colado"
        alert.informativeText = "Para o ClipDeck colar direto no app onde você estava, "
            + "ative-o em Ajustes do Sistema > Privacidade e Segurança > Acessibilidade.\n\n"
            + "Enquanto isso, é só apertar ⌘V — o texto já está na área de transferência."
        alert.addButton(withTitle: "Abrir Ajustes")
        alert.addButton(withTitle: "Agora não")
        alert.alertStyle = .informational

        NSApp.activate(ignoringOtherApps: true)
        if alert.runModal() == .alertFirstButtonReturn {
            Paster.openAccessibilitySettings()
        }
    }

    func reportHotKeyConflict(_ shortcut: String) {
        conflictMessage = "\(shortcut) já está em uso por outro app"
        rebuildMenu()
    }

    private func rebuildMenu() {
        let menu = NSMenu()

        if let conflictMessage {
            let warning = NSMenuItem(title: "⚠︎ \(conflictMessage)", action: nil, keyEquivalent: "")
            warning.isEnabled = false
            menu.addItem(warning)
            menu.addItem(.separator())
        }

        if !accessibilityGranted {
            let warning = NSMenuItem(
                title: "⚠︎ Ativar colagem automática…",
                action: #selector(handleOpenAccessibilitySettings),
                keyEquivalent: ""
            )
            warning.target = self
            menu.addItem(warning)

            let explanation = NSMenuItem(
                title: "    Sem isso, ⏎ copia em vez de colar",
                action: nil,
                keyEquivalent: ""
            )
            explanation.isEnabled = false
            menu.addItem(explanation)
            menu.addItem(.separator())
        }

        let open = NSMenuItem(
            title: "Abrir paleta",
            action: #selector(handleOpenPalette),
            keyEquivalent: ""
        )
        open.target = self
        menu.addItem(open)

        let hint = NSMenuItem(title: "\(KeyCombo.openPalette.displayString)", action: nil, keyEquivalent: "")
        hint.isEnabled = false
        menu.addItem(hint)

        let library = NSMenuItem(
            title: "Biblioteca de templates…",
            action: #selector(handleOpenLibrary),
            keyEquivalent: ""
        )
        library.target = self
        menu.addItem(library)

        let captureHint = NSMenuItem(
            title: "Salvar cópia como template: \(KeyCombo.captureClipboard.displayString)",
            action: nil,
            keyEquivalent: ""
        )
        captureHint.isEnabled = false
        menu.addItem(captureHint)

        menu.addItem(.separator())

        let exportItem = NSMenuItem(
            title: "Exportar templates…",
            action: #selector(handleExport),
            keyEquivalent: ""
        )
        exportItem.target = self
        menu.addItem(exportItem)

        let importItem = NSMenuItem(
            title: "Importar templates…",
            action: #selector(handleImport),
            keyEquivalent: ""
        )
        importItem.target = self
        menu.addItem(importItem)

        let prefs = NSMenuItem(
            title: "Preferências…",
            action: #selector(handleOpenPreferences),
            keyEquivalent: ","
        )
        prefs.target = self
        menu.addItem(prefs)

        menu.addItem(.separator())
        menu.addItem(
            withTitle: "Sair do ClipDeck",
            action: #selector(NSApplication.terminate(_:)),
            keyEquivalent: "q"
        )

        item.menu = menu
    }

    @objc private func handleOpenPalette() {
        openPalette()
    }

    @objc private func handleOpenLibrary() { openLibrary() }
    @objc private func handleOpenPreferences() { openPreferences() }
    @objc private func handleExport() { exportLibrary() }
    @objc private func handleImport() { importLibrary() }

    @objc private func handleOpenAccessibilitySettings() {
        Paster.requestPermission()
        Paster.openAccessibilitySettings()
    }
}

extension Bundle {
    var shortVersion: String {
        object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev"
    }
}
