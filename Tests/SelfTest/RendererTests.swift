import ClipDeckKit
import Foundation

func runRendererTests() {
    Harness.suite("TemplateRenderer — extração de variáveis") {
        let body = "Oi {{nome}}, sobre {{assunto}}. Abraço, {{nome}}."
        let names = TemplateRenderer.variables(in: body).map(\.name)
        Harness.expectEqual(names, ["nome", "assunto"],
                            "variável repetida aparece uma vez só, na ordem de leitura")

        let comBuiltIn = TemplateRenderer.variables(in: "{{nome}} em {{data}}")
        Harness.expectEqual(comBuiltIn.map(\.isBuiltIn), [false, true],
                            "marca quais são resolvidas automaticamente")

        Harness.expectEqual(
            TemplateRenderer.fillableVariables(in: "{{nome}} em {{data}} às {{hora}}").map(\.name),
            ["nome"],
            "só as preenchíveis viram campo do formulário"
        )
        Harness.expect(TemplateRenderer.variables(in: "Texto sem variável").isEmpty,
                       "texto puro não gera variáveis")
    }

    Harness.suite("TemplateRenderer — substituição") {
        let rendered = TemplateRenderer.render(
            "Oi {{nome}}, sobre {{assunto}}. Até logo, {{nome}}.",
            values: ["nome": "Ana", "assunto": "a proposta"]
        )
        Harness.expectEqual(
            rendered, "Oi Ana, sobre a proposta. Até logo, Ana.",
            "substitui todas as ocorrências, inclusive as repetidas"
        )

        Harness.expectEqual(
            TemplateRenderer.render("Oi {{nome}}, tudo bem?", values: [:]),
            "Oi {{nome}}, tudo bem?",
            "variável sem valor fica visível em vez de virar buraco silencioso"
        )

        Harness.expectEqual(
            TemplateRenderer.render("Preço: {{ 100", values: [:]),
            "Preço: {{ 100",
            "abertura sem fechamento fica literal"
        )

        Harness.expectEqual(
            TemplateRenderer.render("{{ nome }} chegou", values: ["nome": "Léo"]),
            "Léo chegou",
            "ignora espaços em volta do nome da variável"
        )

        Harness.expectEqual(
            TemplateRenderer.render("{{a}}{{b}}", values: ["a": "1", "b": "2"]),
            "12",
            "variáveis coladas uma na outra"
        )
    }

    Harness.suite("TemplateRenderer — built-ins") {
        var componentes = DateComponents()
        componentes.year = 2026
        componentes.month = 3
        componentes.day = 7
        componentes.hour = 14
        componentes.minute = 5
        var calendario = Calendar(identifier: .gregorian)
        calendario.timeZone = TimeZone.current
        let data = calendario.date(from: componentes)!

        Harness.expectEqual(
            TemplateRenderer.render("Vence em {{data}}", values: [:], now: data),
            "Vence em 07/03/2026",
            "{{data}} usa formato brasileiro"
        )
        Harness.expectEqual(
            TemplateRenderer.render("Às {{hora}}", values: [:], now: data),
            "Às 14:05",
            "{{hora}} usa 24h"
        )
        Harness.expectEqual(
            TemplateRenderer.render("Copiado: {{clipboard}}", values: [:], clipboard: "abc"),
            "Copiado: abc",
            "{{clipboard}} injeta o conteúdo da área de transferência"
        )
        Harness.expectEqual(
            TemplateRenderer.render("Em {{data}}", values: ["data": "amanhã"], now: data),
            "Em amanhã",
            "valor informado pelo usuário tem prioridade sobre o built-in"
        )
    }

    Harness.suite("TemplateRenderer — rótulos do formulário") {
        let variavel = TemplateVariable(name: "proximo_passo", isBuiltIn: false)
        Harness.expectEqual(variavel.label, "Proximo passo",
                            "underscore vira espaço e a primeira letra sobe")
    }
}
