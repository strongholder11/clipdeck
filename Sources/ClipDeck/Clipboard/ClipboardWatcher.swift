import AppKit
import Combine

/// Guarda as últimas cópias de texto feitas com ⌘C.
///
/// O macOS não notifica mudanças na área de transferência — a única forma é
/// comparar `changeCount` periodicamente. Meio segundo é rápido o bastante para
/// nunca parecer atrasado e leve o bastante para não pesar (é uma leitura de
/// inteiro, não do conteúdo).
@MainActor
final class ClipboardWatcher: ObservableObject {
    struct Entry: Identifiable, Equatable {
        let id = UUID()
        let text: String
        let copiedAt: Date

        /// Primeira linha, para listar sem quebrar o layout.
        var preview: String {
            text.split(separator: "\n", omittingEmptySubsequences: true)
                .first
                .map { $0.trimmingCharacters(in: .whitespaces) } ?? ""
        }
    }

    @Published private(set) var entries: [Entry] = []

    private let capacity: Int
    private var lastChangeCount: Int
    private var timer: Timer?

    init(capacity: Int = 30, pollInterval: TimeInterval = 0.5) {
        self.capacity = capacity
        self.lastChangeCount = NSPasteboard.general.changeCount

        startPolling(every: pollInterval)
    }

    /// Separado do init porque o closure do timer captura self, e dentro do
    /// init o self ainda não está completamente inicializado.
    private func startPolling(every interval: TimeInterval) {
        timer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            // Vincula antes do Task: `self?` dentro do closure concorrente é uma
            // referência a variável capturada, o que o compilador recusa.
            guard let watcher = self else { return }
            Task { @MainActor in watcher.poll() }
        }
    }

    deinit {
        timer?.invalidate()
    }

    var mostRecent: Entry? { entries.first }

    private func poll() {
        let pasteboard = NSPasteboard.general
        let count = pasteboard.changeCount
        guard count != lastChangeCount else { return }
        lastChangeCount = count

        // Ignora o que nós mesmos escrevemos ao colar um template.
        guard !Clipboard.wasWrittenByUs(changeCount: count) else { return }

        guard let text = pasteboard.string(forType: .string) else { return }
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        record(text)
    }

    private func record(_ text: String) {
        // Copiar duas vezes o mesmo texto move o item para o topo em vez de
        // duplicá-lo na lista.
        entries.removeAll { $0.text == text }
        entries.insert(Entry(text: text, copiedAt: Date()), at: 0)

        if entries.count > capacity {
            entries.removeLast(entries.count - capacity)
        }
    }
}
