using System.Globalization;
using System.Text;

namespace ClipDeck.Core.Search;

/// <summary>
/// Normalização de texto para busca em português.
///
/// Digitar "cobranca" precisa achar "Cobrança", e "PROSPECCAO" precisa achar
/// "Prospecção". Decompor em NFD e descartar as marcas diacríticas resolve os
/// dois de uma vez.
/// </summary>
public static class TextNormalizer
{
    /// <summary>Versão comparável: minúscula, sem acento, sem espaço nas pontas.</summary>
    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            // NonSpacingMark é onde vivem os acentos depois da decomposição.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Trim();
    }

    /// <summary>
    /// Índices onde começa cada palavra no texto já normalizado.
    ///
    /// A busca usa isso para dar bônus a casamentos em início de palavra: em
    /// "Follow-up pós-reunião", digitar "fpr" deve pontuar mais que "ollo".
    /// </summary>
    public static HashSet<int> WordStartOffsets(ReadOnlySpan<char> folded)
    {
        var starts = new HashSet<int>();
        var previousWasSeparator = true;

        for (var offset = 0; offset < folded.Length; offset++)
        {
            var ch = folded[offset];
            var isSeparator = !char.IsLetter(ch) && !char.IsDigit(ch);

            if (!isSeparator && previousWasSeparator)
            {
                starts.Add(offset);
            }
            previousWasSeparator = isSeparator;
        }

        return starts;
    }
}
