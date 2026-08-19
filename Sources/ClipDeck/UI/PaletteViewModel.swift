import ClipDeckKit
import Combine
import Foundation

/// Uma linha da paleta.
///
/// Templates e cópias recentes convivem na mesma lista para a navegação por
/// teclado ser uma só — com duas listas separadas, ↓ teria que "pular" de uma
/// para a outra e a seleção viraria um caso especial em todo lugar.
enum PaletteItem: Identifiable {
    case template(SearchResult)
    case recentCopy(ClipboardWatcher.Entry)

    var id: UUID {
        switch self {
        case .template(let result): return result.id
        case .recentCopy(let entry): return entry.id
        }
    }
}

/// Estado da paleta: consulta, itens e seleção.
@MainActor
final class PaletteViewModel: ObservableObject {
    @Published var query: String = "" {
        didSet { refresh() }
    }
    @Published private(set) var items: [PaletteItem] = []
    @Published var selectedIndex: Int = 0

    private let store: Store
    private let clipboard: ClipboardWatcher?

    /// Quantas cópias recentes mostrar. Poucas de propósito: a lista existe para
    /// capturar o que você acabou de escrever, não para virar um histórico.
    private let recentLimit = 3

    init(store: Store, clipboard: ClipboardWatcher? = nil) {
        self.store = store
        self.clipboard = clipboard
        refresh()
    }

    var selectedItem: PaletteItem? {
        items.indices.contains(selectedIndex) ? items[selectedIndex] : nil
    }

    /// Índice onde começa a seção de cópias recentes, para a view desenhar o
    /// cabeçalho no lugar certo.
    private(set) var recentSectionStart: Int?

    var templateCount: Int {
        items.filter { if case .template = $0 { return true }; return false }.count
    }

    func refresh() {
        let results = SearchEngine.search(
            query: query,
            templates: store.templates,
            folders: store.folders
        )

        var built: [PaletteItem] = results.map { .template($0) }
        recentSectionStart = nil

        // As recentes só aparecem sem busca ativa: com uma consulta digitada, a
        // lista precisa ser só o que casa com ela.
        if query.isEmpty, let entries = clipboard?.entries, !entries.isEmpty {
            recentSectionStart = built.count
            built.append(contentsOf: entries.prefix(recentLimit).map { .recentCopy($0) })
        }

        items = built
        // A seleção volta ao topo a cada digitação: o melhor resultado muda, e
        // manter o índice antigo faria você colar o template errado.
        selectedIndex = 0
    }

    func reset() {
        query = ""
        selectedIndex = 0
    }

    func moveSelection(by delta: Int) {
        guard !items.isEmpty else { return }
        selectedIndex = (selectedIndex + delta + items.count) % items.count
    }

    func selectIndex(_ index: Int) {
        guard items.indices.contains(index) else { return }
        selectedIndex = index
    }

    func folderName(for result: SearchResult) -> String? {
        store.folder(withID: result.template.folderID)?.name
    }

    func folderSymbol(for result: SearchResult) -> String? {
        store.folder(withID: result.template.folderID)?.symbol
    }
}
