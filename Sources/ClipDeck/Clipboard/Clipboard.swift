import AppKit

enum Clipboard {
    /// changeCounts gerados pelo próprio ClipDeck.
    ///
    /// O observador precisa distinguir "o usuário copiou algo" de "nós escrevemos
    /// no clipboard ao colar um template" — sem isso, todo template colado voltaria
    /// para a lista de cópias recentes como se fosse uma cópia nova.
    private static var ownChangeCounts: Set<Int> = []

    @discardableResult
    static func write(_ text: String) -> Int {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)

        let count = pasteboard.changeCount
        ownChangeCounts.insert(count)
        // Limpa o histórico para não crescer sem limite durante a sessão.
        if ownChangeCounts.count > 50 {
            ownChangeCounts = Set(ownChangeCounts.sorted().suffix(20))
        }
        return count
    }

    static func readText() -> String? {
        NSPasteboard.general.string(forType: .string)
    }

    static func wasWrittenByUs(changeCount: Int) -> Bool {
        ownChangeCounts.contains(changeCount)
    }

    static func markAsOurs(changeCount: Int) {
        ownChangeCounts.insert(changeCount)
    }
}
