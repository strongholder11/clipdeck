namespace ClipDeck.Core.Models;

/// <summary>Raiz do arquivo salvo em disco.</summary>
public sealed class Library
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Versão do formato. Existe para permitir migração sem perder dados quando
    /// o modelo mudar.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<Folder> Folders { get; set; } = new();
    public List<Template> Templates { get; set; } = new();

    public Folder? FolderById(Guid? id) =>
        id is null ? null : Folders.FirstOrDefault(f => f.Id == id);

    /// <summary>
    /// Biblioteca inicial: pastas por objetivo, cada uma com um exemplo real
    /// mostrando o uso de variáveis.
    /// </summary>
    public static Library Seeded()
    {
        var prospeccao = new Folder { Name = "Prospecção", Symbol = "search", Order = 0 };
        var followUp = new Folder { Name = "Follow-up", Symbol = "reply", Order = 1 };
        var agendamento = new Folder { Name = "Agendamento", Symbol = "calendar", Order = 2 };
        var suporte = new Folder { Name = "Suporte", Symbol = "lifebuoy", Order = 3 };
        var cobranca = new Folder { Name = "Cobrança", Symbol = "money", Order = 4 };

        return new Library
        {
            Folders = { prospeccao, followUp, agendamento, suporte, cobranca },
            Templates =
            {
                new Template
                {
                    Title = "Primeiro contato",
                    Body = "Oi {{nome}}, tudo bem?\n\nVi que a {{empresa}} está {{contexto}}. "
                         + "Trabalho ajudando empresas nessa situação e acho que faz sentido "
                         + "trocarmos uma ideia.\n\nTem 15 minutos essa semana?",
                    FolderId = prospeccao.Id,
                    Tags = { "frio", "abertura" },
                },
                new Template
                {
                    Title = "Follow-up pós-reunião",
                    Body = "{{nome}}, obrigado pelo tempo hoje.\n\nResumo do que combinamos:\n"
                         + "{{pontos}}\n\nPróximo passo: {{proximo_passo}}. Te retorno em {{data}}.",
                    FolderId = followUp.Id,
                    Tags = { "reuniao" },
                },
                new Template
                {
                    Title = "Sem resposta — segundo toque",
                    Body = "Oi {{nome}}, subindo esse aqui na sua caixa.\n\nSei como fica corrido. "
                         + "Ainda faz sentido conversarmos sobre {{assunto}}, ou prefere que eu "
                         + "retome mais pra frente?",
                    FolderId = followUp.Id,
                    Tags = { "insistencia" },
                },
                new Template
                {
                    Title = "Confirmar reunião",
                    Body = "Oi {{nome}}, confirmando nosso encontro em {{data}} às "
                         + "{{hora_reuniao}}.\n\nLink: {{link}}\n\nQualquer imprevisto, me avisa "
                         + "por aqui.",
                    FolderId = agendamento.Id,
                    Tags = { "confirmacao" },
                },
                new Template
                {
                    Title = "Acusar recebimento",
                    Body = "Oi {{nome}}, recebi sua mensagem — obrigado por avisar.\n\nJá estou "
                         + "olhando e te dou um retorno até {{prazo}}.",
                    FolderId = suporte.Id,
                    Tags = { "primeira-resposta" },
                },
                new Template
                {
                    Title = "Lembrete de pagamento",
                    Body = "Oi {{nome}}, tudo bem?\n\nPassando para lembrar da fatura de "
                         + "{{valor}}, com vencimento em {{vencimento}}.\n\nSe já tiver pago, "
                         + "ignora essa mensagem. Qualquer dúvida, é só chamar.",
                    FolderId = cobranca.Id,
                    Tags = { "educado" },
                },
            },
        };
    }
}
