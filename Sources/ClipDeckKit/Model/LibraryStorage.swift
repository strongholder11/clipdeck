import Foundation

/// Leitura e escrita da biblioteca em disco.
///
/// Separado do `Store` de propósito: aqui não há Combine nem estado observável,
/// então dá para testar escrita atômica, backup e recuperação sem levantar o app.
public struct LibraryStorage {
    public enum LoadOutcome: Equatable {
        case loaded
        case seededNewFile
        case recoveredFromBackup(String)
        case seededAfterCorruption(String)
    }

    public let directory: URL
    private let maxBackups = 5

    public init(directory: URL) {
        self.directory = directory
    }

    public static var defaultDirectory: URL {
        FileManager.default
            .urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("ClipDeck", isDirectory: true)
    }

    public var fileURL: URL { directory.appendingPathComponent("library.json") }
    public var backupsDirectory: URL { directory.appendingPathComponent("Backups", isDirectory: true) }

    // JSON legível e estável: dá para versionar no git, sincronizar via iCloud
    // Drive e editar à mão sem o diff explodir a cada gravação.
    private static var encoder: JSONEncoder {
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        e.dateEncodingStrategy = .iso8601
        return e
    }

    private static var decoder: JSONDecoder {
        let d = JSONDecoder()
        d.dateDecodingStrategy = .iso8601
        return d
    }

    /// Carrega a biblioteca, degradando em vez de falhar.
    ///
    /// Sem arquivo, semeia. Com arquivo corrompido, tenta o backup mais recente
    /// antes de desistir — e nunca apaga o arquivo ruim: renomeia para `.corrupt`
    /// para você poder resgatar o conteúdo à mão.
    public func load() throws -> (library: Library, outcome: LoadOutcome) {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            let seeded = Library.seeded()
            try write(seeded)
            return (seeded, .seededNewFile)
        }

        do {
            let data = try Data(contentsOf: fileURL)
            return (try Self.decoder.decode(Library.self, from: data), .loaded)
        } catch {
            let quarantine = directory.appendingPathComponent(
                "library.corrupt-\(Self.timestamp()).json"
            )
            try? FileManager.default.moveItem(at: fileURL, to: quarantine)

            if let (recovered, name) = try? mostRecentBackup() {
                try write(recovered)
                return (recovered, .recoveredFromBackup(name))
            }

            let seeded = Library.seeded()
            try write(seeded)
            return (seeded, .seededAfterCorruption(quarantine.lastPathComponent))
        }
    }

    /// Grava de forma atômica, com backup do estado anterior.
    public func save(_ library: Library) throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try backupCurrentFile()
        try write(library)
    }

    private func write(_ library: Library) throws {
        let data = try Self.encoder.encode(library)
        // .atomic grava num temporário e faz rename — uma queda no meio da
        // escrita deixa o arquivo antigo intacto em vez de meio arquivo.
        try data.write(to: fileURL, options: .atomic)
    }

    private func backupCurrentFile() throws {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return }
        try FileManager.default.createDirectory(at: backupsDirectory, withIntermediateDirectories: true)

        let destination = backupsDirectory
            .appendingPathComponent("library-\(Self.timestamp()).json")
        try? FileManager.default.copyItem(at: fileURL, to: destination)

        try pruneBackups()
    }

    private func pruneBackups() throws {
        let files = (try? FileManager.default.contentsOfDirectory(
            at: backupsDirectory,
            includingPropertiesForKeys: nil
        )) ?? []

        let sorted = files
            .filter { $0.lastPathComponent.hasPrefix("library-") }
            .sorted { $0.lastPathComponent > $1.lastPathComponent }

        for stale in sorted.dropFirst(maxBackups) {
            try? FileManager.default.removeItem(at: stale)
        }
    }

    private func mostRecentBackup() throws -> (Library, String)? {
        let files = (try? FileManager.default.contentsOfDirectory(
            at: backupsDirectory,
            includingPropertiesForKeys: nil
        )) ?? []

        let candidates = files
            .filter { $0.lastPathComponent.hasPrefix("library-") }
            .sorted { $0.lastPathComponent > $1.lastPathComponent }

        for candidate in candidates {
            if let data = try? Data(contentsOf: candidate),
               let library = try? Self.decoder.decode(Library.self, from: data) {
                return (library, candidate.lastPathComponent)
            }
        }
        return nil
    }

    /// Timestamp ordenável lexicograficamente — a ordenação por nome de arquivo
    /// nas rotinas de backup depende disso.
    private static func timestamp() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd-HHmmss-SSS"
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        return formatter.string(from: Date())
    }
}
