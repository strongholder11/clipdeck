using System.Text.Json.Serialization;

namespace ClipDeck.Core.Models;

/// <summary>Um template de mensagem.</summary>
public sealed class Template
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Sem título";
    public string Body { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>Quantas vezes foi usado. Alimenta o ranking da busca.</summary>
    public int UseCount { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Primeira linha não vazia do corpo, para preview na lista.</summary>
    [JsonIgnore]
    public string Preview
    {
        get
        {
            foreach (var line in Body.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0) return trimmed;
            }
            return string.Empty;
        }
    }

    public Template Clone() => new()
    {
        Id = Id,
        Title = Title,
        Body = Body,
        FolderId = FolderId,
        Tags = new List<string>(Tags),
        UseCount = UseCount,
        LastUsedAt = LastUsedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}
