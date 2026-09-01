using ClipDeck.Core.Models;
using ClipDeck.Core.Search;

namespace ClipDeck.Core.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Cobrança", "cobranca")]
    [InlineData("  PROSPECÇÃO  ", "prospeccao")]
    [InlineData("Follow-up pós-reunião", "follow-up pos-reuniao")]
    public void RemoveAcentoECaixa(string entrada, string esperado)
        => Assert.Equal(esperado, TextNormalizer.Fold(entrada));

    [Fact]
    public void HifenContaComoFronteiraDePalavra()
    {
        var folded = TextNormalizer.Fold("Follow-up pós-reunião");
        Assert.Equal(new[] { 0, 7, 10, 14 }, TextNormalizer.WordStartOffsets(folded).Order());
    }
}

public class SearchEngineTests
{
    private readonly Folder _vendas = new() { Name = "Prospecção", Order = 0 };
    private readonly Folder _cobranca = new() { Name = "Cobrança", Order = 1 };
    private readonly List<Template> _templates;
    private readonly List<Folder> _folders;

    public SearchEngineTests()
    {
        _folders = new List<Folder> { _vendas, _cobranca };
        _templates = new List<Template>
        {
            new() { Title = "Follow-up pós-reunião", Body = "Obrigado pelo tempo hoje.",
                    FolderId = _vendas.Id, Tags = { "reuniao" } },
            new() { Title = "Primeiro contato", Body = "Oi, vi que a empresa está crescendo.",
                    FolderId = _vendas.Id, Tags = { "frio", "abertura" } },
            new() { Title = "Lembrete de pagamento", Body = "Sua fatura vence em breve.",
                    FolderId = _cobranca.Id, Tags = { "urgente" } },
            new() { Title = "Aviso de vencimento", Body = "Pagamento pendente.",
                    FolderId = _cobranca.Id, Tags = { "urgente", "financeiro" } },
        };
    }

    private List<string> Titles(string query) =>
        SearchEngine.Search(query, _templates, _folders).Select(r => r.Template.Title).ToList();

    [Fact]
    public void AbreviacaoPorIniciaisAchaOTemplate()
        => Assert.Equal("Follow-up pós-reunião", Titles("flwp").FirstOrDefault());

    [Fact]
    public void PrefixoDoTituloVence()
        => Assert.Equal("Follow-up pós-reunião", Titles("follow").FirstOrDefault());

    [Fact]
    public void ConsultaSemCorrespondenciaRetornaVazio()
        => Assert.Empty(Titles("zzzqqq"));

    [Fact]
    public void SemAcentoAchaPastaAcentuada()
        => Assert.Equal(2, Titles("prospeccao").Count);

    [Fact]
    public void ReuniaoAchaReuniao()
        => Assert.Equal("Follow-up pós-reunião", Titles("reuniao").FirstOrDefault());

    [Fact]
    public void MaiusculaSemCedilhaAchaCobranca()
        => Assert.Equal(2, Titles("COBRANCA").Count);

    [Fact]
    public void MatchNoTituloGanhaDeMatchNoCorpo()
        => Assert.Equal("Lembrete de pagamento", Titles("pagamento").FirstOrDefault());

    [Fact]
    public void FiltroDeTagRestringeOConjunto()
        => Assert.Equal(2, Titles("#urgente").Count);

    [Fact]
    public void TagEspecificaIsolaUmTemplate()
        => Assert.Equal("Aviso de vencimento", Titles("#financeiro").FirstOrDefault());

    [Fact]
    public void FiltroDePastaRestringe()
        => Assert.Equal(2, Titles("/cobranca").Count);

    [Fact]
    public void TextoLivreCombinaComFiltroDeTag()
        => Assert.Equal("Aviso de vencimento", Titles("aviso #urgente").FirstOrDefault());

    [Fact]
    public void TagInexistenteNaoRetornaNada()
        => Assert.Empty(Titles("#inexistente"));

    [Fact]
    public void MaisUsadoSobeMesmoComTituloMenosExato()
    {
        var usado = new Template { Title = "Aviso de vencimento", Body = "Pagamento pendente.",
                                   UseCount = 40, LastUsedAt = DateTimeOffset.Now };
        var novo = new Template { Title = "Aviso de vencimento antecipado", Body = "x" };

        var ranked = SearchEngine.Search("aviso", new[] { novo, usado }, _folders);
        Assert.Equal(40, ranked.First().Template.UseCount);
    }

    [Fact]
    public void UsadoHojePontuaMaisQueHa90Dias()
    {
        var agora = DateTimeOffset.Now;
        var recente = new Template { Title = "A", UseCount = 5, LastUsedAt = agora };
        var antigo = new Template { Title = "A", UseCount = 5, LastUsedAt = agora.AddDays(-90) };

        Assert.True(SearchEngine.FrecencyMultiplier(recente, agora)
                  > SearchEngine.FrecencyMultiplier(antigo, agora));
    }

    [Fact]
    public void ConsultaVaziaListaTudoOrdenadoPorUso()
    {
        _templates[2].UseCount = 99;
        var all = SearchEngine.Search("", _templates, _folders);
        Assert.Equal(_templates.Count, all.Count);
        Assert.Equal("Lembrete de pagamento", all.First().Template.Title);
    }

    [Fact]
    public void DestacaPosicoesCasadasNoTitulo()
    {
        var results = SearchEngine.Search("follow", _templates, _folders);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, results.First().TitleHighlights);
    }

    [Fact]
    public void ParserSeparaTextoTagEPasta()
    {
        var parsed = SearchEngine.Parse("aviso #urgente /cobranca vencimento");
        Assert.Equal("aviso vencimento", parsed.Text);
        Assert.Equal(new[] { "urgente" }, parsed.Tags);
        Assert.Equal("cobranca", parsed.FolderName);
    }
}

public class BodyMatchTests
{
    private readonly List<Template> _templates = new()
    {
        new() { Title = "Alfa", Body = "Bom dia! Estou entrando em contato para reforçar nossa "
                                     + "proposta comercial e alinhar os próximos passos do projeto." },
        new() { Title = "Beta", Body = "Confirmando nossa reunião de amanhã." },
    };

    private List<string> Titles(string query) =>
        SearchEngine.Search(query, _templates, Array.Empty<Folder>())
                    .Select(r => r.Template.Title).ToList();

    /// <summary>
    /// As letras r-e-u-n-i-a-o aparecem espalhadas e em ordem no corpo de "Alfa"
    /// (refoRçar... propostA... cOmercial), então o fuzzy casaria os dois. Só
    /// "Beta" contém a palavra de verdade. Esse foi um bug real na versão macOS.
    /// </summary>
    [Fact]
    public void NaoCasaLetrasEspalhadasPeloCorpo()
        => Assert.Equal(new[] { "Beta" }, Titles("reuniao"));

    [Fact]
    public void CasaTermoLiteralNoCorpo()
        => Assert.Equal(new[] { "Alfa" }, Titles("proposta"));

    [Fact]
    public void CasaExpressaoComEspacoNoCorpo()
        => Assert.Equal(new[] { "Alfa" }, Titles("proximos passos"));

    [Fact]
    public void TermoAusenteNaoRetornaNada()
        => Assert.Empty(Titles("xyzw"));

    [Fact]
    public void FuzzyContinuaValendoNoTitulo()
    {
        var curtos = new List<Template>
        {
            new() { Title = "Follow-up pós-reunião", Body = "x" },
            new() { Title = "Orçamento", Body = "y" },
        };
        var achados = SearchEngine.Search("flwp", curtos, Array.Empty<Folder>());
        Assert.Equal(new[] { "Follow-up pós-reunião" }, achados.Select(r => r.Template.Title));
    }
}
