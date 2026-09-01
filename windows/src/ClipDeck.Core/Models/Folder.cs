namespace ClipDeck.Core.Models;

/// <summary>Uma pasta, agrupando templates por objetivo.</summary>
public sealed class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Sem nome";

    /// <summary>Nome do glifo usado na interface.</summary>
    public string Symbol { get; set; } = "folder";

    public int Order { get; set; }
}
