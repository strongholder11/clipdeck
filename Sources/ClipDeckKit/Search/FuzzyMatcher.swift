import Foundation

public struct FuzzyMatch {
    public let score: Double
    /// Posições casadas no texto normalizado, para destacar na UI.
    public let matchedIndices: [Int]
}

/// Casamento fuzzy por subsequência com pontuação.
///
/// Digitar "flwp" acha "Follow-up pós-reunião". A escolha de programação dinâmica
/// em vez do laço guloso óbvio existe porque o guloso casa a primeira ocorrência
/// de cada letra e erra o ranking: em "Aviso de pagamento", o guloso casaria "ap"
/// no "A" e no "a" de "pagamento"... ou no "a" de "Aviso" mesmo. A DP encontra o
/// melhor alinhamento, então início de palavra e letras coladas ganham como
/// deveriam.
public enum FuzzyMatcher {
    private static let matchScore = 1.0
    private static let wordStartBonus = 1.5
    private static let consecutiveBonus = 1.2
    private static let gapPenalty = -0.15
    private static let firstCharBonus = 0.8

    /// Limite de texto analisado. O corpo de um template pode ter milhares de
    /// caracteres, e a busca roda a cada tecla digitada.
    public static let maxCandidateLength = 2_000

    /// Retorna nil se nem todos os caracteres da consulta aparecem em ordem.
    ///
    /// - Parameter trackIndices: backtracking custa memória; só vale a pena para
    ///   o campo que será destacado na tela (o título).
    public static func match(
        query: [Character],
        candidate: [Character],
        wordStarts: Set<Int>,
        trackIndices: Bool = false
    ) -> FuzzyMatch? {
        guard !query.isEmpty else { return FuzzyMatch(score: 0, matchedIndices: []) }

        let candidate = candidate.count > maxCandidateLength
            ? Array(candidate.prefix(maxCandidateLength))
            : candidate
        let n = candidate.count
        let m = query.count
        guard m <= n else { return nil }

        let negativeInfinity = -Double.greatestFiniteMagnitude

        // dp[j] = melhor pontuação com query[i] casando exatamente em candidate[j].
        var previousRow = [Double](repeating: negativeInfinity, count: n)
        var currentRow = [Double](repeating: negativeInfinity, count: n)
        var parents: [[Int]] = trackIndices
            ? Array(repeating: [Int](repeating: -1, count: n), count: m)
            : []

        for i in 0..<m {
            // Melhor valor da linha anterior até j-2, para permitir um salto com
            // penalidade. Mantido incrementalmente para não virar O(n²).
            var bestBefore = negativeInfinity
            var bestBeforeIndex = -1

            for j in 0..<n {
                defer {
                    // Só entra no acumulado depois de usado, garantindo que o
                    // salto sempre venha de uma posição estritamente anterior.
                    if i > 0, j >= 1, previousRow[j - 1] > bestBefore {
                        bestBefore = previousRow[j - 1]
                        bestBeforeIndex = j - 1
                    }
                }

                guard candidate[j] == query[i] else {
                    currentRow[j] = negativeInfinity
                    continue
                }

                var bonus = matchScore
                if wordStarts.contains(j) { bonus += wordStartBonus }
                if j == 0 { bonus += firstCharBonus }

                if i == 0 {
                    // Casar mais perto do início do texto é ligeiramente melhor.
                    currentRow[j] = bonus - Double(j) * 0.01
                    if trackIndices { parents[i][j] = -1 }
                    continue
                }

                // j == 0 não tem caractere anterior, então não existe casamento
                // consecutivo possível — e ler previousRow[-1] estouraria o array.
                let consecutive = (j >= 1 && previousRow[j - 1] > negativeInfinity)
                    ? previousRow[j - 1] + bonus + consecutiveBonus
                    : negativeInfinity
                let jumped = bestBefore > negativeInfinity
                    ? bestBefore + bonus + gapPenalty
                    : negativeInfinity

                if consecutive >= jumped, consecutive > negativeInfinity {
                    currentRow[j] = consecutive
                    if trackIndices { parents[i][j] = j - 1 }
                } else if jumped > negativeInfinity {
                    currentRow[j] = jumped
                    if trackIndices { parents[i][j] = bestBeforeIndex }
                } else {
                    currentRow[j] = negativeInfinity
                }
            }
            swap(&previousRow, &currentRow)
        }

        guard let bestEnd = previousRow.indices
            .filter({ previousRow[$0] > negativeInfinity })
            .max(by: { previousRow[$0] < previousRow[$1] })
        else { return nil }

        let raw = previousRow[bestEnd]

        // Normaliza pelo tamanho da consulta, para consultas de tamanhos
        // diferentes serem comparáveis, e favorece candidatos curtos: entre
        // "Follow-up" e "Follow-up depois da reunião de kickoff", o curto vence.
        let normalized = (raw / Double(m)) * (1.0 + 12.0 / (12.0 + Double(n)))

        var indices: [Int] = []
        if trackIndices {
            var j = bestEnd
            for i in stride(from: m - 1, through: 0, by: -1) {
                guard j >= 0 else { break }
                indices.append(j)
                j = parents[i][j]
            }
            indices.reverse()
        }

        return FuzzyMatch(score: normalized, matchedIndices: indices)
    }
}
