import ClipDeckKit
import Foundation

func runSearchTests() {
    let vendas = Folder(name: "Prospecção", order: 0)
    let cobranca = Folder(name: "Cobrança", order: 1)
    let folders = [vendas, cobranca]

    let templates = [
        Template(title: "Follow-up pós-reunião", body: "Obrigado pelo tempo hoje.",
                 folderID: vendas.id, tags: ["reuniao"]),
        Template(title: "Primeiro contato", body: "Oi, vi que a empresa está crescendo.",
                 folderID: vendas.id, tags: ["frio", "abertura"]),
        Template(title: "Lembrete de pagamento", body: "Sua fatura vence em breve.",
                 folderID: cobranca.id, tags: ["urgente"]),
        Template(title: "Aviso de vencimento", body: "Pagamento pendente.",
                 folderID: cobranca.id, tags: ["urgente", "financeiro"]),
    ]

    func titles(_ query: String, now: Date = Date()) -> [String] {
        SearchEngine.search(query: query, templates: templates, folders: folders, now: now)
            .map(\.template.title)
    }

    Harness.suite("SearchEngine — busca fuzzy") {
        Harness.expectEqual(titles("flwp").first, "Follow-up pós-reunião",
                            "abreviação por iniciais de sílaba acha o template")
        Harness.expectEqual(titles("follow").first, "Follow-up pós-reunião",
                            "prefixo do título vence")
        Harness.expect(titles("zzzqqq").isEmpty,
                       "consulta sem correspondência retorna vazio")
    }

    Harness.suite("SearchEngine — acentuação em português") {
        Harness.expectEqual(titles("prospeccao").count, 2,
                            "'prospeccao' sem acento acha a pasta 'Prospecção'")
        Harness.expectEqual(titles("reuniao").first, "Follow-up pós-reunião",
                            "'reuniao' acha 'reunião'")
        Harness.expectEqual(titles("COBRANCA").count, 2,
                            "maiúscula sem cedilha acha a pasta 'Cobrança'")
    }

    Harness.suite("SearchEngine — peso do título sobre o corpo") {
        // "pagamento" está no título de um e no corpo do outro.
        Harness.expectEqual(titles("pagamento").first, "Lembrete de pagamento",
                            "match no título ganha de match no corpo")
    }

    Harness.suite("SearchEngine — filtros #tag e /pasta") {
        Harness.expectEqual(titles("#urgente").count, 2, "#tag restringe o conjunto")
        Harness.expectEqual(titles("#financeiro").first, "Aviso de vencimento",
                            "#tag específica isola um template")
        Harness.expectEqual(titles("/cobranca").count, 2, "/pasta restringe por pasta")
        Harness.expectEqual(titles("aviso #urgente").first, "Aviso de vencimento",
                            "texto livre combina com filtro de tag")
        Harness.expect(titles("#inexistente").isEmpty, "tag inexistente não retorna nada")
    }

    Harness.suite("SearchEngine — ranking por frequência de uso") {
        var usado = Template(title: "Aviso de vencimento", body: "Pagamento pendente.",
                             folderID: cobranca.id, tags: ["urgente"])
        usado.useCount = 40
        usado.lastUsedAt = Date()

        let novo = Template(title: "Aviso de vencimento antecipado", body: "x",
                            folderID: cobranca.id)

        let ranked = SearchEngine.search(query: "aviso", templates: [novo, usado], folders: folders)
        Harness.expectEqual(ranked.first?.template.useCount, 40,
                            "o mais usado sobe mesmo com título menos exato")
    }

    Harness.suite("SearchEngine — recência decai") {
        let agora = Date()
        var recente = Template(title: "Template A", body: "x")
        recente.useCount = 5
        recente.lastUsedAt = agora

        var antigo = Template(title: "Template A", body: "x")
        antigo.useCount = 5
        antigo.lastUsedAt = agora.addingTimeInterval(-90 * 86_400)

        let recenteScore = SearchEngine.frecencyMultiplier(for: recente, now: agora)
        let antigoScore = SearchEngine.frecencyMultiplier(for: antigo, now: agora)
        Harness.expect(recenteScore > antigoScore,
                       "mesmo uso, mas usado hoje pontua mais que há 90 dias")
    }

    Harness.suite("SearchEngine — consulta vazia") {
        var populares = templates
        populares[2].useCount = 99
        let all = SearchEngine.search(query: "", templates: populares, folders: folders)
        Harness.expectEqual(all.count, templates.count, "consulta vazia lista tudo")
        Harness.expectEqual(all.first?.template.title, "Lembrete de pagamento",
                            "consulta vazia ordena pelos mais usados")
    }

    Harness.suite("SearchEngine — destaque no título") {
        let results = SearchEngine.search(query: "follow", templates: templates, folders: folders)
        let highlights = results.first?.titleHighlights ?? []
        Harness.expectEqual(highlights, [0, 1, 2, 3, 4, 5],
                            "destaca as posições casadas para a UI sublinhar")
    }

    Harness.suite("SearchEngine — parser da consulta") {
        let parsed = SearchEngine.parse("aviso #urgente /cobranca vencimento")
        Harness.expectEqual(parsed.text, "aviso vencimento", "texto livre preservado na ordem")
        Harness.expectEqual(parsed.tags, ["urgente"], "extrai a tag")
        Harness.expectEqual(parsed.folderName, "cobranca", "extrai a pasta")
    }
}

func runBodyMatchTests() {
    let templates = [
        Template(title: "Alfa", body: """
            Bom dia! Estou entrando em contato para reforçar nossa proposta \
            comercial e alinhar os próximos passos do projeto.
            """),
        Template(title: "Beta", body: "Confirmando nossa reunião de amanhã."),
    ]

    func titles(_ query: String) -> [String] {
        SearchEngine.search(query: query, templates: templates, folders: []).map(\.template.title)
    }

    Harness.suite("SearchEngine — corpo casa por substring, não por subsequência") {
        // As letras r-e-u-n-i-a-o aparecem espalhadas e em ordem no corpo de
        // "Alfa" (refoRçar... propostA... cOmercial), então o fuzzy casaria os
        // dois. Só "Beta" contém a palavra de verdade.
        Harness.expectEqual(titles("reuniao"), ["Beta"],
                            "não casa letras espalhadas pelo corpo")
        Harness.expectEqual(titles("proposta"), ["Alfa"],
                            "casa termo literal presente no corpo")
        Harness.expectEqual(titles("proximos passos"), ["Alfa"],
                            "casa expressão com espaço no corpo")
        Harness.expect(titles("xyzw").isEmpty, "termo ausente não retorna nada")
    }

    Harness.suite("SearchEngine — fuzzy continua valendo no título") {
        let curtos = [
            Template(title: "Follow-up pós-reunião", body: "x"),
            Template(title: "Orçamento", body: "y"),
        ]
        let achados = SearchEngine.search(query: "flwp", templates: curtos, folders: [])
        Harness.expectEqual(achados.map(\.template.title), ["Follow-up pós-reunião"],
                            "abreviação por iniciais ainda funciona no título")
    }
}
