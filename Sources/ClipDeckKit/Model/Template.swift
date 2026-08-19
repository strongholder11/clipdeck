import Foundation

/// Um template de mensagem.
public struct Template: Codable, Identifiable, Equatable, Hashable {
    public let id: UUID
    public var title: String
    public var body: String
    public var folderID: UUID?
    public var tags: [String]

    /// Quantas vezes foi usado. Alimenta o ranking: o que você mais usa sobe sozinho.
    public var useCount: Int
    public var lastUsedAt: Date?
    public var createdAt: Date
    public var updatedAt: Date

    public init(
        id: UUID = UUID(),
        title: String,
        body: String,
        folderID: UUID? = nil,
        tags: [String] = [],
        useCount: Int = 0,
        lastUsedAt: Date? = nil,
        createdAt: Date = Date(),
        updatedAt: Date = Date()
    ) {
        self.id = id
        self.title = title
        self.body = body
        self.folderID = folderID
        self.tags = tags
        self.useCount = useCount
        self.lastUsedAt = lastUsedAt
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    /// Primeira linha não vazia do corpo, para preview na lista.
    public var preview: String {
        body
            .split(separator: "\n", omittingEmptySubsequences: true)
            .first
            .map { $0.trimmingCharacters(in: .whitespaces) }
            ?? ""
    }

    /// Decodificação tolerante: campos novos adicionados em versões futuras não
    /// invalidam um arquivo antigo, e um arquivo editado à mão sem `tags` ou
    /// `useCount` continua carregando em vez de derrubar a biblioteca inteira.
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decodeIfPresent(UUID.self, forKey: .id) ?? UUID()
        title = try c.decodeIfPresent(String.self, forKey: .title) ?? "Sem título"
        body = try c.decodeIfPresent(String.self, forKey: .body) ?? ""
        folderID = try c.decodeIfPresent(UUID.self, forKey: .folderID)
        tags = try c.decodeIfPresent([String].self, forKey: .tags) ?? []
        useCount = try c.decodeIfPresent(Int.self, forKey: .useCount) ?? 0
        lastUsedAt = try c.decodeIfPresent(Date.self, forKey: .lastUsedAt)
        createdAt = try c.decodeIfPresent(Date.self, forKey: .createdAt) ?? Date()
        updatedAt = try c.decodeIfPresent(Date.self, forKey: .updatedAt) ?? Date()
    }
}
