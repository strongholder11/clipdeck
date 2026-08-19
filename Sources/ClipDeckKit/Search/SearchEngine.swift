import Foundation

public struct SearchResult: Identifiable, Equatable {
    public let template: Template
    public let score: Double
    /// Posições casadas no título (texto normalizado), para destaque.
    public let titleHighlights: [Int]

    public var id: UUID { template.id }
}

/// Consulta já separada em filtros e texto livre.
public struct ParsedQuery: Equatable {
    public var text: String
    public var tags: [String]
    public var folderName: String?

    public var isEmpty: Bool { text.isEmpty && tags.isEmpty && folderName == nil }
}

public enum SearchEngine {
    // O título é o que você lembra; o corpo é onde a palavra aparece por acaso.
    private static let titleWeight = 3.0
    private static let tagWeight = 2.0
    private static let folderWeight = 1.5
    private static let bodyWeight = 1.0

    /// Separa `#tag` e `/pasta` do texto livre.
    ///
    /// "cobranca #urgente" busca "cobranca" só entre os templates com a tag urgente.
    public static func parse(_ query: String) -> ParsedQuery {
        var text: [String] = []
        var tags: [String] = []
        var folderName: String?

        for token in query.split(separator: " ", omittingEmptySubsequences: true) {
            if token.hasPrefix("#"), token.count > 1 {
                tags.append(TextNormalizer.fold(String(token.dropFirst())))
            } else if token.hasPrefix("/"), token.count > 1 {
                folderName = TextNormalizer.fold(String(token.dropFirst()))
            } else {
                text.append(String(token))
            }
        }

        return ParsedQuery(
            text: TextNormalizer.fold(text.joined(separator: " ")),
            tags: tags,
            folderName: folderName
        )
    }

    public static func search(
        query: String,
        templates: [Template],
        folders: [Folder],
        now: Date = Date()
    ) -> [SearchResult] {
        let parsed = parse(query)
        let foldersByID = Dictionary(uniqueKeysWithValues: folders.map { ($0.id, $0) })
        let queryChars = Array(parsed.text)

        var results: [SearchResult] = []

        for template in templates {
            // Filtros são eliminatórios: restringem o conjunto antes de pontuar.
            if !parsed.tags.isEmpty {
                let folded = template.tags.map(TextNormalizer.fold)
                let matchesAll = parsed.tags.allSatisfy { needle in
                    folded.contains { $0.contains(needle) }
                }
                guard matchesAll else { continue }
            }

            if let wanted = parsed.folderName {
                let name = foldersByID[template.folderID ?? UUID()].map { TextNormalizer.fold($0.name) }
                guard let name, name.contains(wanted) else { continue }
            }

            var best = 0.0
            var highlights: [Int] = []

            if queryChars.isEmpty {
                // Sem texto livre, os filtros já selecionaram: ordena só por uso.
                best = 1.0
            } else {
                let titleFolded = Array(TextNormalizer.fold(template.title))
                if let match = FuzzyMatcher.match(
                    query: queryChars,
                    candidate: titleFolded,
                    wordStarts: TextNormalizer.wordStartOffsets(in: titleFolded),
                    trackIndices: true
                ) {
                    best = match.score * titleWeight
                    highlights = match.matchedIndices
                }

                for tag in template.tags {
                    let folded = Array(TextNormalizer.fold(tag))
                    if let match = FuzzyMatcher.match(
                        query: queryChars,
                        candidate: folded,
                        wordStarts: TextNormalizer.wordStartOffsets(in: folded)
                    ) {
                        best = max(best, match.score * tagWeight)
                    }
                }

                if let folder = foldersByID[template.folderID ?? UUID()] {
                    let folded = Array(TextNormalizer.fold(folder.name))
                    if let match = FuzzyMatcher.match(
                        query: queryChars,
                        candidate: folded,
                        wordStarts: TextNormalizer.wordStartOffsets(in: folded)
                    ) {
                        best = max(best, match.score * folderWeight)
                    }
                }

                // O corpo casa por substring contígua, não por subsequência fuzzy.
                //
                // Fuzzy no corpo parece uma boa ideia e arruína a busca: num texto
                // de várias linhas, as letras de quase qualquer consulta aparecem
                // espalhadas em ordem, então todo template casa e a lista deixa de
                // filtrar. Medido: "reuniao" retornava os 6 templates da biblioteca
                // inicial. No título, curto, o fuzzy continua valendo a pena.
                if let bodyScore = substringScore(
                    needle: parsed.text,
                    haystack: TextNormalizer.fold(template.body)
                ) {
                    best = max(best, bodyScore * bodyWeight)
                }

                guard best > 0 else { continue }
            }

            results.append(
                SearchResult(
                    template: template,
                    score: best * frecencyMultiplier(for: template, now: now),
                    titleHighlights: highlights
                )
            )
        }

        return results.sorted {
            // Desempate por título mantém a lista estável entre digitações, em vez
            // de itens empatados trocando de lugar a cada tecla.
            ($0.score, $1.template.title) > ($1.score, $0.template.title)
        }
    }

    /// Pontua ocorrência literal do termo dentro de um texto.
    ///
    /// Retorna nil quando não há ocorrência. Match no início de uma palavra vale
    /// mais que no meio dela, e ocorrência mais ao início do texto vale um pouco
    /// mais que lá no fim.
    static func substringScore(needle: String, haystack: String) -> Double? {
        guard !needle.isEmpty, let range = haystack.range(of: needle) else { return nil }

        let offset = haystack.distance(from: haystack.startIndex, to: range.lowerBound)

        var score = 1.0
        if offset == 0 {
            score += 0.5
        } else {
            let previous = haystack[haystack.index(before: range.lowerBound)]
            if !previous.isLetter && !previous.isNumber { score += 0.4 }
        }

        // Decai suavemente com a distância até o início — nunca a ponto de zerar.
        score *= 1.0 / (1.0 + Double(offset) / 400.0)
        return score
    }

    /// Combina frequência e recência de uso.
    ///
    /// O log evita que um template usado 200 vezes domine para sempre a lista, e o
    /// decaimento exponencial faz o que você usou hoje subir sem apagar o histórico.
    public static func frecencyMultiplier(for template: Template, now: Date) -> Double {
        let frequency = 1.0 + log(1.0 + Double(template.useCount)) * 0.30

        guard let lastUsed = template.lastUsedAt else { return frequency }
        let days = max(0, now.timeIntervalSince(lastUsed) / 86_400)
        let recency = 1.0 + 0.5 * exp(-days / 14.0)

        return frequency * recency
    }
}
