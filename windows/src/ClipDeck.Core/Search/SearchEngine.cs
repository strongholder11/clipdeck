using ClipDeck.Core.Models;

namespace ClipDeck.Core.Search;

public sealed record SearchResult(Template Template, double Score, IReadOnlyList<int> TitleHighlights);

/// <summary>Consulta já separada em filtros e texto livre.</summary>
public sealed record ParsedQuery(string Text, IReadOnlyList<string> Tags, string? FolderName)
{
    public bool IsEmpty => Text.Length == 0 && Tags.Count == 0 && FolderName is null;
}

public static class SearchEngine
{
    // O título é o que você lembra; o corpo é onde a palavra aparece por acaso.
    private const double TitleWeight = 3.0;
    private const double TagWeight = 2.0;
    private const double FolderWeight = 1.5;
    private const double BodyWeight = 1.0;

    /// <summary>
    /// Separa "#tag" e "/pasta" do texto livre. "cobranca #urgente" busca
    /// "cobranca" só entre os templates com a tag urgente.
    /// </summary>
    public static ParsedQuery Parse(string query)
    {
        var text = new List<string>();
        var tags = new List<string>();
        string? folderName = null;

        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 1 && token[0] == '#')
            {
                tags.Add(TextNormalizer.Fold(token[1..]));
            }
            else if (token.Length > 1 && token[0] == '/')
            {
                folderName = TextNormalizer.Fold(token[1..]);
            }
            else
            {
                text.Add(token);
            }
        }

        return new ParsedQuery(TextNormalizer.Fold(string.Join(' ', text)), tags, folderName);
    }

    public static List<SearchResult> Search(
        string query,
        IReadOnlyList<Template> templates,
        IReadOnlyList<Folder> folders,
        DateTimeOffset? now = null)
    {
        var moment = now ?? DateTimeOffset.Now;
        var parsed = Parse(query);
        var foldersById = folders.ToDictionary(f => f.Id);
        var results = new List<SearchResult>();

        foreach (var template in templates)
        {
            // Filtros são eliminatórios: restringem o conjunto antes de pontuar.
            if (parsed.Tags.Count > 0)
            {
                var folded = template.Tags.Select(TextNormalizer.Fold).ToList();
                if (!parsed.Tags.All(needle => folded.Any(t => t.Contains(needle, StringComparison.Ordinal))))
                {
                    continue;
                }
            }

            Folder? folder = null;
            if (template.FolderId is { } fid) foldersById.TryGetValue(fid, out folder);

            if (parsed.FolderName is { } wanted)
            {
                if (folder is null) continue;
                if (!TextNormalizer.Fold(folder.Name).Contains(wanted, StringComparison.Ordinal)) continue;
            }

            double best;
            IReadOnlyList<int> highlights = Array.Empty<int>();

            if (parsed.Text.Length == 0)
            {
                // Sem texto livre, os filtros já selecionaram: ordena só por uso.
                best = 1.0;
            }
            else
            {
                best = 0.0;

                var titleFolded = TextNormalizer.Fold(template.Title);
                var titleMatch = FuzzyMatcher.Match(
                    parsed.Text, titleFolded,
                    TextNormalizer.WordStartOffsets(titleFolded), trackIndices: true);
                if (titleMatch is { } tm)
                {
                    best = tm.Score * TitleWeight;
                    highlights = tm.MatchedIndices;
                }

                foreach (var tag in template.Tags)
                {
                    var folded = TextNormalizer.Fold(tag);
                    var match = FuzzyMatcher.Match(
                        parsed.Text, folded, TextNormalizer.WordStartOffsets(folded));
                    if (match is { } m) best = Math.Max(best, m.Score * TagWeight);
                }

                if (folder is not null)
                {
                    var folded = TextNormalizer.Fold(folder.Name);
                    var match = FuzzyMatcher.Match(
                        parsed.Text, folded, TextNormalizer.WordStartOffsets(folded));
                    if (match is { } m) best = Math.Max(best, m.Score * FolderWeight);
                }

                // O corpo casa por substring contígua, não por subsequência fuzzy.
                //
                // Fuzzy no corpo parece boa ideia e arruína a busca: num texto de
                // várias linhas, as letras de quase qualquer consulta aparecem
                // espalhadas em ordem, então todo template casa e a lista deixa de
                // filtrar. Medido na versão macOS: "reuniao" retornava os 6
                // templates da biblioteca inicial. No título, curto, o fuzzy vale.
                var bodyScore = SubstringScore(parsed.Text, TextNormalizer.Fold(template.Body));
                if (bodyScore is { } bs) best = Math.Max(best, bs * BodyWeight);

                if (best <= 0) continue;
            }

            results.Add(new SearchResult(
                template, best * FrecencyMultiplier(template, moment), highlights));
        }

        // Desempate por título mantém a lista estável entre digitações, em vez de
        // itens empatados trocando de lugar a cada tecla.
        results.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0
                ? byScore
                : string.Compare(a.Template.Title, b.Template.Title, StringComparison.CurrentCulture);
        });

        return results;
    }

    /// <summary>
    /// Pontua ocorrência literal do termo dentro de um texto. Retorna null quando
    /// não há ocorrência. Match no início de palavra vale mais que no meio dela.
    /// </summary>
    public static double? SubstringScore(string needle, string haystack)
    {
        if (needle.Length == 0) return null;

        var offset = haystack.IndexOf(needle, StringComparison.Ordinal);
        if (offset < 0) return null;

        var score = 1.0;
        if (offset == 0)
        {
            score += 0.5;
        }
        else
        {
            var previous = haystack[offset - 1];
            if (!char.IsLetter(previous) && !char.IsDigit(previous)) score += 0.4;
        }

        // Decai suavemente com a distância até o início — nunca a ponto de zerar.
        return score * (1.0 / (1.0 + offset / 400.0));
    }

    /// <summary>
    /// Combina frequência e recência de uso. O log evita que um template usado
    /// 200 vezes domine para sempre, e o decaimento exponencial faz o que você
    /// usou hoje subir sem apagar o histórico.
    /// </summary>
    public static double FrecencyMultiplier(Template template, DateTimeOffset now)
    {
        var frequency = 1.0 + Math.Log(1.0 + template.UseCount) * 0.30;

        if (template.LastUsedAt is not { } lastUsed) return frequency;

        var days = Math.Max(0, (now - lastUsed).TotalDays);
        var recency = 1.0 + 0.5 * Math.Exp(-days / 14.0);

        return frequency * recency;
    }
}
