import Foundation

/// Uma pasta, agrupando templates por objetivo (Prospecção, Cobrança, ...).
public struct Folder: Codable, Identifiable, Equatable, Hashable {
    public let id: UUID
    public var name: String
    public var symbol: String
    public var order: Int

    public init(id: UUID = UUID(), name: String, symbol: String = "folder", order: Int = 0) {
        self.id = id
        self.name = name
        self.symbol = symbol
        self.order = order
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decodeIfPresent(UUID.self, forKey: .id) ?? UUID()
        name = try c.decodeIfPresent(String.self, forKey: .name) ?? "Sem nome"
        symbol = try c.decodeIfPresent(String.self, forKey: .symbol) ?? "folder"
        order = try c.decodeIfPresent(Int.self, forKey: .order) ?? 0
    }
}
