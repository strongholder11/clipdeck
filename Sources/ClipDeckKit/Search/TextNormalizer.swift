import Foundation

/// Normalização de texto para busca em português.
///
/// Digitar "cobranca" precisa achar "Cobrança", e "PROSPECCAO" precisa achar
/// "Prospecção". O `folding` do Foundation resolve os dois de uma vez, removendo
/// diacríticos e caixa numa passada só.
public enum TextNormalizer {
    private static let locale = Locale(identifier: "pt_BR")

    /// Versão comparável de uma string: minúscula, sem acento, sem espaço nas pontas.
    public static func fold(_ text: String) -> String {
        text
            .folding(options: [.diacriticInsensitive, .caseInsensitive], locale: locale)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Índices (em `Array(fold(text))`) onde começa cada palavra.
    ///
    /// A busca usa isso para dar bônus a matches em início de palavra: em
    /// "Follow-up pós-reunião", digitar "fpr" deve pontuar mais alto que "ollo".
    public static func wordStartOffsets(in folded: [Character]) -> Set<Int> {
        var starts: Set<Int> = []
        var previousWasSeparator = true

        for (offset, character) in folded.enumerated() {
            let isSeparator = !character.isLetter && !character.isNumber
            if !isSeparator && previousWasSeparator {
                starts.insert(offset)
            }
            previousWasSeparator = isSeparator
        }
        return starts
    }
}
