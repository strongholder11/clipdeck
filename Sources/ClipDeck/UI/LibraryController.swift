import AppKit
import ClipDeckKit
import SwiftUI
import UniformTypeIdentifiers

/// Janela de gerenciamento de templates.
@MainActor
final class LibraryController: NSObject, NSWindowDelegate {
    private let store: Store
    private var window: NSWindow?

    init(store: Store) {
        self.store = store
        super.init()
    }

    func show() {
        if let window {
            NSApp.activate(ignoringOtherApps: true)
            window.makeKeyAndOrderFront(nil)
            return
        }

        let hosting = NSHostingController(rootView: LibraryView(store: store))
        let window = NSWindow(contentViewController: hosting)
        window.title = "Biblioteca do ClipDeck"
        window.styleMask = [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView]
        window.setContentSize(NSSize(width: 980, height: 600))
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()

        self.window = window
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {
        // Grava na hora de fechar em vez de esperar o debounce: fechar a janela é
        // o momento em que se espera que o trabalho esteja salvo.
        store.flush()
        window = nil
    }

    // MARK: - Importar e exportar

    func exportLibrary() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.json]
        panel.nameFieldStringValue = "clipdeck-templates.json"
        panel.message = "Escolha onde salvar a cópia dos seus templates"

        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK, let url = panel.url else { return }

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601

        do {
            try encoder.encode(store.library).write(to: url, options: .atomic)
        } catch {
            presentError("Não foi possível exportar", error.localizedDescription)
        }
    }

    func importLibrary() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.json]
        panel.allowsMultipleSelection = false
        panel.message = "Escolha um arquivo de templates do ClipDeck"

        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK, let url = panel.url else { return }

        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601

        do {
            let data = try Data(contentsOf: url)
            let imported = try decoder.decode(Library.self, from: data)
            confirmImport(imported)
        } catch {
            presentError(
                "Arquivo inválido",
                "Não parece um arquivo de templates do ClipDeck.\n\n\(error.localizedDescription)"
            )
        }
    }

    /// Importar substitui tudo, então pergunta antes — e oferece mesclar, que é o
    /// que a maioria quer ao trazer templates de outra máquina.
    private func confirmImport(_ imported: Library) {
        let alert = NSAlert()
        alert.messageText = "Importar \(imported.templates.count) templates?"
        alert.informativeText = "Você tem \(store.templates.count) templates agora."
        alert.addButton(withTitle: "Adicionar aos meus")
        alert.addButton(withTitle: "Substituir tudo")
        alert.addButton(withTitle: "Cancelar")

        switch alert.runModal() {
        case .alertFirstButtonReturn:
            var merged = store.library
            // Regera os ids para não colidir com templates existentes vindos de
            // uma exportação da própria máquina.
            let existingFolderNames = Set(merged.folders.map(\.name))
            var folderMap: [UUID: UUID] = [:]

            for folder in imported.folders {
                if let existing = merged.folders.first(where: { $0.name == folder.name }) {
                    folderMap[folder.id] = existing.id
                } else {
                    let copy = Folder(
                        name: folder.name,
                        symbol: folder.symbol,
                        order: (merged.folders.map(\.order).max() ?? -1) + 1
                    )
                    folderMap[folder.id] = copy.id
                    merged.folders.append(copy)
                }
            }
            _ = existingFolderNames

            for template in imported.templates {
                merged.templates.append(
                    Template(
                        title: template.title,
                        body: template.body,
                        folderID: template.folderID.flatMap { folderMap[$0] },
                        tags: template.tags
                    )
                )
            }
            store.replaceLibrary(with: merged)

        case .alertSecondButtonReturn:
            store.replaceLibrary(with: imported)

        default:
            break
        }
    }

    private func presentError(_ title: String, _ detail: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = detail
        alert.alertStyle = .warning
        alert.runModal()
    }
}
