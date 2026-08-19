import ClipDeckKit
import Foundation

// Sem buffer: se um teste derrubar o processo, a saída até ali não se perde.
setvbuf(stdout, nil, _IONBF, 0)

Harness.suite("TextNormalizer") {
    Harness.expectEqual(
        TextNormalizer.fold("Cobrança"), "cobranca",
        "remove cedilha e acento, e baixa a caixa"
    )
    Harness.expectEqual(
        TextNormalizer.fold("  PROSPECÇÃO  "), "prospeccao",
        "apara espaços e normaliza maiúsculas acentuadas"
    )
    Harness.expectEqual(
        TextNormalizer.fold("Follow-up pós-reunião"), "follow-up pos-reuniao",
        "preserva pontuação interna"
    )

    let folded = Array(TextNormalizer.fold("Follow-up pós-reunião"))
    let starts = TextNormalizer.wordStartOffsets(in: folded)
    Harness.expectEqual(
        starts, [0, 7, 10, 14],
        "hífen conta como fronteira de palavra"
    )
}

runStorageTests()

runSearchTests()

runBodyMatchTests()

runRendererTests()

Harness.finish()
