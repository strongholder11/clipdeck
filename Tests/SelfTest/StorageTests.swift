import ClipDeckKit
import Foundation

func runStorageTests() {
    func makeTempDirectory() -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("clipdeck-test-\(UUID().uuidString)")
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    Harness.suite("LibraryStorage — primeira execução") {
        let dir = makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let storage = LibraryStorage(directory: dir)

        guard let result = try? storage.load() else {
            return Harness.expect(false, "load() não deveria lançar")
        }
        Harness.expectEqual(result.outcome, .seededNewFile, "semeia quando não há arquivo")
        Harness.expect(!result.library.folders.isEmpty, "vem com pastas de exemplo")
        Harness.expect(
            FileManager.default.fileExists(atPath: storage.fileURL.path),
            "grava o arquivo no primeiro load"
        )
    }

    Harness.suite("LibraryStorage — roundtrip") {
        let dir = makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let storage = LibraryStorage(directory: dir)

        let folder = Folder(name: "Cobrança", order: 3)
        let template = Template(
            title: "Aviso de vencimento",
            body: "Oi {{nome}}, sua fatura vence em {{data}}.",
            folderID: folder.id,
            tags: ["urgente", "financeiro"],
            useCount: 7
        )
        let original = Library(folders: [folder], templates: [template])

        try? storage.save(original)
        guard let reloaded = try? storage.load().library else {
            return Harness.expect(false, "load() após save não deveria lançar")
        }

        Harness.expectEqual(reloaded.templates.count, 1, "preserva a contagem de templates")
        Harness.expectEqual(reloaded.templates[0].title, "Aviso de vencimento", "preserva o título")
        Harness.expectEqual(reloaded.templates[0].useCount, 7, "preserva o contador de uso")
        Harness.expectEqual(reloaded.templates[0].tags, ["urgente", "financeiro"], "preserva as tags")
        Harness.expectEqual(reloaded.templates[0].folderID, folder.id, "preserva o vínculo com a pasta")
        Harness.expectEqual(reloaded.folders[0].name, "Cobrança", "preserva acentuação no nome da pasta")
    }

    Harness.suite("LibraryStorage — recuperação de arquivo corrompido") {
        let dir = makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let storage = LibraryStorage(directory: dir)

        // Grava duas vezes: a segunda gera o backup da primeira.
        let good = Library(templates: [Template(title: "Bom template", body: "conteúdo")])
        try? storage.save(good)
        try? storage.save(good)

        // Simula corrupção (queda de energia, edição manual errada, sync ruim).
        try? Data("{ isso não é json".utf8).write(to: storage.fileURL)

        guard let result = try? storage.load() else {
            return Harness.expect(false, "load() não deveria lançar com arquivo corrompido")
        }

        if case .recoveredFromBackup = result.outcome {
            Harness.expect(true, "recupera a partir do backup mais recente")
        } else {
            Harness.expect(false, "deveria recuperar do backup, obteve \(result.outcome)")
        }
        Harness.expectEqual(result.library.templates.first?.title, "Bom template", "conteúdo volta intacto")

        let quarantined = (try? FileManager.default.contentsOfDirectory(atPath: dir.path))?
            .filter { $0.hasPrefix("library.corrupt-") } ?? []
        Harness.expect(!quarantined.isEmpty, "preserva o arquivo corrompido em quarentena")
    }

    Harness.suite("LibraryStorage — corrompido sem backup") {
        let dir = makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let storage = LibraryStorage(directory: dir)

        try? Data("lixo".utf8).write(to: storage.fileURL)
        guard let result = try? storage.load() else {
            return Harness.expect(false, "load() não deveria lançar")
        }

        if case .seededAfterCorruption = result.outcome {
            Harness.expect(true, "semeia de novo quando não há backup aproveitável")
        } else {
            Harness.expect(false, "esperava seededAfterCorruption, obteve \(result.outcome)")
        }
    }

    Harness.suite("LibraryStorage — rotação de backups") {
        let dir = makeTempDirectory()
        defer { try? FileManager.default.removeItem(at: dir) }
        let storage = LibraryStorage(directory: dir)

        for index in 0..<9 {
            try? storage.save(Library(templates: [Template(title: "v\(index)", body: "x")]))
        }

        let backups = (try? FileManager.default.contentsOfDirectory(atPath: storage.backupsDirectory.path))?
            .filter { $0.hasPrefix("library-") } ?? []
        Harness.expect(backups.count <= 5, "mantém no máximo 5 backups (obteve \(backups.count))")
        Harness.expect(backups.count >= 1, "mantém pelo menos 1 backup")
    }

    Harness.suite("Decodificação tolerante") {
        let partial = """
        { "templates": [ { "title": "Só título" } ] }
        """
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601

        let library = try? decoder.decode(Library.self, from: Data(partial.utf8))
        Harness.expect(library != nil, "carrega JSON escrito à mão sem campos opcionais")
        Harness.expectEqual(library?.templates.first?.title, "Só título", "usa o título informado")
        Harness.expectEqual(library?.templates.first?.useCount, 0, "usa 0 como padrão de useCount")
        Harness.expectEqual(library?.folders.count, 0, "aceita ausência de pastas")
    }
}
