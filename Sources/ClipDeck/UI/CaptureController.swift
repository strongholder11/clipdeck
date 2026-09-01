import AppKit
import ClipDeckKit
import SwiftUI

/// Janela de captura: pega um texto e vira template.
@MainActor
final class CaptureController: NSObject, NSWindowDelegate {
    private let store: Store
    private var window: NSWindow?

    init(store: Store) {
        self.store = store
        super.init()
    }

    /// Abre o formulário com o texto informado.
    ///
    /// Retorna false quando não há nada para salvar — quem chama avisa o usuário,
    /// em vez de abrir uma janela vazia.
    @discardableResult
    func capture(text: String?) -> Bool {
        guard let text, !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return false
        }

        close()

        let view = CaptureView(
            body_: text,
            folders: store.folders,
            suggestedTags: store.allTags,
            initialFolderChoice: CommandLine.arguments.contains("--new-folder") ? .new : .none,
            onSave: { [weak self] title, folderID, tags in
                guard let self else { return }
                self.store.add(
                    Template(title: title, body: text, folderID: folderID, tags: tags)
                )
                self.close()
            },
            onCreateFolder: { [weak self] name in
                guard let self else { return UUID() }
                let order = (self.store.folders.map(\.order).max() ?? -1) + 1
                let folder = Folder(name: name, order: order)
                self.store.addFolder(folder)
                return folder.id
            },
            onCancel: { [weak self] in self?.close() }
        )

        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = "Salvar como template"
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        // .floating para não ficar atrás do app em que você estava trabalhando.
        window.level = .floating
        // Vem para o Espaço (Mesa) atual em vez de o sistema levar o usuário até
        // ela. Sem isso, ativar o ClipDeck com esta janela aberta em outra Mesa
        // faz o macOS trocar de Mesa, o que é desorientador com dois monitores.
        window.collectionBehavior = [.moveToActiveSpace, .fullScreenAuxiliary]

        self.window = window

        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
        return true
    }

    func close() {
        window?.orderOut(nil)
        window = nil
    }

    func windowWillClose(_ notification: Notification) {
        window = nil
    }
}
