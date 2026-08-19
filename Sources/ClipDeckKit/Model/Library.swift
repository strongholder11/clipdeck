import Foundation

/// Raiz do arquivo salvo em disco.
public struct Library: Codable, Equatable {
    /// Versão do formato. Existe para permitir migração sem perder dados
    /// quando o modelo mudar — o `Store` compara e migra antes de usar.
    public var schemaVersion: Int
    public var folders: [Folder]
    public var templates: [Template]

    public static let currentSchemaVersion = 1

    public init(
        schemaVersion: Int = Library.currentSchemaVersion,
        folders: [Folder] = [],
        templates: [Template] = []
    ) {
        self.schemaVersion = schemaVersion
        self.folders = folders
        self.templates = templates
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        schemaVersion = try c.decodeIfPresent(Int.self, forKey: .schemaVersion) ?? 1
        folders = try c.decodeIfPresent([Folder].self, forKey: .folders) ?? []
        templates = try c.decodeIfPresent([Template].self, forKey: .templates) ?? []
    }

    public func folder(withID id: UUID?) -> Folder? {
        guard let id else { return nil }
        return folders.first { $0.id == id }
    }

    /// Biblioteca inicial: pastas por objetivo, cada uma com um exemplo real
    /// mostrando o uso de variáveis.
    public static func seeded() -> Library {
        let prospeccao = Folder(name: "Prospecção", symbol: "sparkle.magnifyingglass", order: 0)
        let followUp = Folder(name: "Follow-up", symbol: "arrow.uturn.right", order: 1)
        let agendamento = Folder(name: "Agendamento", symbol: "calendar", order: 2)
        let suporte = Folder(name: "Suporte", symbol: "lifepreserver", order: 3)
        let cobranca = Folder(name: "Cobrança", symbol: "dollarsign.circle", order: 4)

        return Library(
            folders: [prospeccao, followUp, agendamento, suporte, cobranca],
            templates: [
                Template(
                    title: "Primeiro contato",
                    body: """
                        Oi {{nome}}, tudo bem?

                        Vi que a {{empresa}} está {{contexto}}. Trabalho ajudando \
                        empresas nessa situação e acho que faz sentido trocarmos uma ideia.

                        Tem 15 minutos essa semana?
                        """,
                    folderID: prospeccao.id,
                    tags: ["frio", "abertura"]
                ),
                Template(
                    title: "Follow-up pós-reunião",
                    body: """
                        {{nome}}, obrigado pelo tempo hoje.

                        Resumo do que combinamos:
                        {{pontos}}

                        Próximo passo: {{proximo_passo}}. Te retorno em {{data}}.
                        """,
                    folderID: followUp.id,
                    tags: ["reuniao"]
                ),
                Template(
                    title: "Sem resposta — segundo toque",
                    body: """
                        Oi {{nome}}, subindo esse aqui na sua caixa.

                        Sei como fica corrido. Ainda faz sentido conversarmos sobre \
                        {{assunto}}, ou prefere que eu retome mais pra frente?
                        """,
                    folderID: followUp.id,
                    tags: ["insistencia"]
                ),
                Template(
                    title: "Confirmar reunião",
                    body: """
                        Oi {{nome}}, confirmando nosso encontro em {{data}} às {{hora_reuniao}}.

                        Link: {{link}}

                        Qualquer imprevisto, me avisa por aqui.
                        """,
                    folderID: agendamento.id,
                    tags: ["confirmacao"]
                ),
                Template(
                    title: "Acusar recebimento",
                    body: """
                        Oi {{nome}}, recebi sua mensagem — obrigado por avisar.

                        Já estou olhando e te dou um retorno até {{prazo}}.
                        """,
                    folderID: suporte.id,
                    tags: ["primeira-resposta"]
                ),
                Template(
                    title: "Lembrete de pagamento",
                    body: """
                        Oi {{nome}}, tudo bem?

                        Passando para lembrar da fatura de {{valor}}, com vencimento \
                        em {{vencimento}}.

                        Se já tiver pago, ignora essa mensagem. Qualquer dúvida, é só chamar.
                        """,
                    folderID: cobranca.id,
                    tags: ["educado"]
                ),
            ]
        )
    }
}
