import Combine
import Foundation

/// Estado da biblioteca em memória, observável pela UI.
///
/// Grava com debounce: editar um template dispara uma alteração por tecla
/// digitada, e sem debounce isso viraria centenas de escritas em disco.
@MainActor
public final class Store: ObservableObject {
    @Published public private(set) var library: Library
    @Published public private(set) var lastLoadOutcome: LibraryStorage.LoadOutcome

    private let storage: LibraryStorage
    private var saveTask: Task<Void, Never>?
    private let saveDebounce: Duration

    public init(storage: LibraryStorage = LibraryStorage(directory: LibraryStorage.defaultDirectory),
                saveDebounce: Duration = .milliseconds(500)) {
        self.storage = storage
        self.saveDebounce = saveDebounce

        if let result = try? storage.load() {
            library = result.library
            lastLoadOutcome = result.outcome
        } else {
            library = Library.seeded()
            lastLoadOutcome = .seededNewFile
        }
    }

    public var templates: [Template] { library.templates }
    public var folders: [Folder] { library.folders.sorted { $0.order < $1.order } }

    public func folder(withID id: UUID?) -> Folder? { library.folder(withID: id) }

    /// Todas as tags em uso, ordenadas por frequência.
    public var allTags: [String] {
        var counts: [String: Int] = [:]
        for template in library.templates {
            for tag in template.tags { counts[tag, default: 0] += 1 }
        }
        return counts.sorted { ($0.value, $1.key) > ($1.value, $0.key) }.map(\.key)
    }

    // MARK: - Mutações

    public func add(_ template: Template) {
        library.templates.append(template)
        scheduleSave()
    }

    public func update(_ template: Template) {
        guard let index = library.templates.firstIndex(where: { $0.id == template.id }) else { return }
        var updated = template
        updated.updatedAt = Date()
        library.templates[index] = updated
        scheduleSave()
    }

    public func delete(templateID: UUID) {
        library.templates.removeAll { $0.id == templateID }
        scheduleSave()
    }

    /// Registra um uso. É o que alimenta o ranking por frequência na busca.
    public func recordUse(of templateID: UUID) {
        guard let index = library.templates.firstIndex(where: { $0.id == templateID }) else { return }
        library.templates[index].useCount += 1
        library.templates[index].lastUsedAt = Date()
        scheduleSave()
    }

    public func addFolder(_ folder: Folder) {
        library.folders.append(folder)
        scheduleSave()
    }

    public func renameFolder(id: UUID, to name: String) {
        guard let index = library.folders.firstIndex(where: { $0.id == id }) else { return }
        library.folders[index].name = name
        scheduleSave()
    }

    /// Remove a pasta e solta os templates dela na raiz, em vez de apagá-los junto.
    public func deleteFolder(id: UUID) {
        library.folders.removeAll { $0.id == id }
        for index in library.templates.indices where library.templates[index].folderID == id {
            library.templates[index].folderID = nil
        }
        scheduleSave()
    }

    public func move(templateID: UUID, toFolder folderID: UUID?) {
        guard let index = library.templates.firstIndex(where: { $0.id == templateID }) else { return }
        library.templates[index].folderID = folderID
        library.templates[index].updatedAt = Date()
        scheduleSave()
    }

    public func replaceLibrary(with newLibrary: Library) {
        library = newLibrary
        scheduleSave()
    }

    // MARK: - Persistência

    private func scheduleSave() {
        saveTask?.cancel()
        let snapshot = library
        saveTask = Task { [storage, saveDebounce] in
            try? await Task.sleep(for: saveDebounce)
            guard !Task.isCancelled else { return }
            try? storage.save(snapshot)
        }
    }

    /// Grava imediatamente, sem esperar o debounce. Usado ao encerrar o app,
    /// onde não há tempo para o timer disparar.
    public func flush() {
        saveTask?.cancel()
        saveTask = nil
        try? storage.save(library)
    }
}
