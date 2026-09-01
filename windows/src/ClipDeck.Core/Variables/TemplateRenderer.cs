using System.Globalization;
using System.Text;

namespace ClipDeck.Core.Variables;

/// <summary>Uma variável encontrada no corpo de um template.</summary>
public readonly record struct TemplateVariable(string Name, bool IsBuiltIn)
{
    /// <summary>
    /// Rótulo legível. Trocar underscore por espaço e subir a primeira letra
    /// basta — inventar acentuação que o usuário não escreveu seria pior.
    /// </summary>
    public string Label
    {
        get
        {
            var spaced = Name.Replace('_', ' ');
            return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced[1..];
        }
    }
}

/// <summary>Interpreta {{variáveis}} no corpo dos templates.</summary>
public static class TemplateRenderer
{
    /// <summary>Variáveis resolvidas sozinhas, sem perguntar nada ao usuário.</summary>
    public static readonly IReadOnlySet<string> BuiltInNames =
        new HashSet<string> { "data", "hora", "clipboard" };

    private static readonly CultureInfo Culture = new("pt-BR");

    /// <summary>
    /// Extrai as variáveis na ordem em que aparecem, sem repetir. A ordem importa:
    /// é a ordem dos campos no formulário.
    /// </summary>
    public static List<TemplateVariable> Variables(string body)
    {
        var found = new List<TemplateVariable>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in RawNames(body))
        {
            if (!seen.Add(name)) continue;
            found.Add(new TemplateVariable(name, BuiltInNames.Contains(name)));
        }

        return found;
    }

    /// <summary>Só as variáveis que precisam ser preenchidas à mão.</summary>
    public static List<TemplateVariable> FillableVariables(string body) =>
        Variables(body).Where(v => !v.IsBuiltIn).ToList();

    /// <summary>
    /// Substitui as variáveis pelos valores. Variáveis sem valor informado ficam
    /// como estão, em vez de virarem string vazia: um buraco visível no texto é
    /// melhor que uma frase que perdeu uma palavra sem você perceber.
    /// </summary>
    public static string Render(
        string body,
        IReadOnlyDictionary<string, string> values,
        string? clipboard = null,
        DateTimeOffset? now = null)
    {
        var moment = now ?? DateTimeOffset.Now;
        var output = new StringBuilder(body.Length);
        var index = 0;

        while (index < body.Length)
        {
            var open = body.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                output.Append(body, index, body.Length - index);
                break;
            }

            output.Append(body, index, open - index);

            var close = body.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                // Sem fechamento: o resto é texto literal, incluindo o "{{".
                output.Append(body, open, body.Length - open);
                break;
            }

            var name = body[(open + 2)..close].Trim();
            var resolved = Resolve(name, values, clipboard, moment);
            output.Append(resolved ?? $"{{{{{name}}}}}");

            index = close + 2;
        }

        return output.ToString();
    }

    private static string? Resolve(
        string name,
        IReadOnlyDictionary<string, string> values,
        string? clipboard,
        DateTimeOffset now)
    {
        // Valor informado tem prioridade sobre built-in: se o template usa
        // {{data}} e o usuário digitou uma data, vale a dele.
        if (values.TryGetValue(name, out var provided) && provided.Length > 0) return provided;

        return name switch
        {
            "data" => now.ToString("dd/MM/yyyy", Culture),
            "hora" => now.ToString("HH:mm", Culture),
            "clipboard" => clipboard,
            _ => null,
        };
    }

    /// <summary>Nomes na ordem de aparição, com repetições.</summary>
    private static IEnumerable<string> RawNames(string body)
    {
        var index = 0;
        while (index < body.Length)
        {
            var open = body.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0) yield break;

            var close = body.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0) yield break;

            var name = body[(open + 2)..close].Trim();
            if (name.Length > 0) yield return name;

            index = close + 2;
        }
    }
}
