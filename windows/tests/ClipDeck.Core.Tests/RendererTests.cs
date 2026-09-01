using ClipDeck.Core.Variables;

namespace ClipDeck.Core.Tests;

public class TemplateRendererTests
{
    private static readonly Dictionary<string, string> Vazio = new();

    [Fact]
    public void VariavelRepetidaApareceUmaVezNaOrdemDeLeitura()
    {
        var names = TemplateRenderer.Variables("Oi {{nome}}, sobre {{assunto}}. Abraço, {{nome}}.")
                                    .Select(v => v.Name);
        Assert.Equal(new[] { "nome", "assunto" }, names);
    }

    [Fact]
    public void MarcaQuaisSaoResolvidasAutomaticamente()
    {
        var vars = TemplateRenderer.Variables("{{nome}} em {{data}}");
        Assert.Equal(new[] { false, true }, vars.Select(v => v.IsBuiltIn));
    }

    [Fact]
    public void SoAsPreenchiveisViramCampo()
    {
        var names = TemplateRenderer.FillableVariables("{{nome}} em {{data}} às {{hora}}")
                                    .Select(v => v.Name);
        Assert.Equal(new[] { "nome" }, names);
    }

    [Fact]
    public void TextoPuroNaoGeraVariaveis()
        => Assert.Empty(TemplateRenderer.Variables("Texto sem variável"));

    [Fact]
    public void SubstituiTodasAsOcorrencias()
    {
        var values = new Dictionary<string, string> { ["nome"] = "Ana", ["assunto"] = "a proposta" };
        Assert.Equal(
            "Oi Ana, sobre a proposta. Até logo, Ana.",
            TemplateRenderer.Render("Oi {{nome}}, sobre {{assunto}}. Até logo, {{nome}}.", values));
    }

    [Fact]
    public void VariavelSemValorFicaVisivel()
        => Assert.Equal("Oi {{nome}}, tudo bem?",
                        TemplateRenderer.Render("Oi {{nome}}, tudo bem?", Vazio));

    [Fact]
    public void AberturaSemFechamentoFicaLiteral()
        => Assert.Equal("Preço: {{ 100", TemplateRenderer.Render("Preço: {{ 100", Vazio));

    [Fact]
    public void IgnoraEspacosEmVoltaDoNome()
    {
        var values = new Dictionary<string, string> { ["nome"] = "Léo" };
        Assert.Equal("Léo chegou", TemplateRenderer.Render("{{ nome }} chegou", values));
    }

    [Fact]
    public void VariaveisColadasUmaNaOutra()
    {
        var values = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        Assert.Equal("12", TemplateRenderer.Render("{{a}}{{b}}", values));
    }

    [Fact]
    public void BuiltInsUsamFormatoBrasileiro()
    {
        var data = new DateTimeOffset(2026, 3, 7, 14, 5, 0, TimeSpan.Zero);
        Assert.Equal("Vence em 07/03/2026", TemplateRenderer.Render("Vence em {{data}}", Vazio, now: data));
        Assert.Equal("Às 14:05", TemplateRenderer.Render("Às {{hora}}", Vazio, now: data));
    }

    [Fact]
    public void ClipboardInjetaConteudo()
        => Assert.Equal("Copiado: abc",
                        TemplateRenderer.Render("Copiado: {{clipboard}}", Vazio, clipboard: "abc"));

    [Fact]
    public void ValorInformadoTemPrioridadeSobreBuiltIn()
    {
        var values = new Dictionary<string, string> { ["data"] = "amanhã" };
        Assert.Equal("Em amanhã", TemplateRenderer.Render("Em {{data}}", values));
    }

    [Fact]
    public void RotuloTrocaUnderscorePorEspaco()
        => Assert.Equal("Proximo passo", new TemplateVariable("proximo_passo", false).Label);
}
