using System.Text.Json;

namespace ClipDeck.Core.Models;

/// <summary>
/// Exportar e importar a biblioteca, no mesmo formato da versão macOS — um
/// arquivo exportado num sistema abre no outro.
/// </summary>
public static class LibraryTransfer
{
    public static string Serialize(Library library) =>
        JsonSerializer.Serialize(library, LibraryStorage.Options);

    /// <summary>Lança <see cref="JsonException"/> se não for um arquivo do ClipDeck.</summary>
    public static Library Deserialize(string json)
    {
        var library = JsonSerializer.Deserialize<Library>(json, LibraryStorage.Options)
                      ?? throw new JsonException("arquivo vazio");

        // Um JSON qualquer ({} ou outro programa) desserializa sem erro, só que
        // vazio. Melhor recusar do que "importar" zero templates em silêncio.
        if (library.Templates.Count == 0 && library.Folders.Count == 0)
        {
            throw new JsonException("nenhum template ou pasta encontrado");
        }

        return library;
    }

    /// <summary>
    /// "Adicionar aos meus": junta o importado ao que já existe.
    ///
    /// Os ids são regerados para não colidir com templates vindos de uma
    /// exportação da própria máquina, e pastas de mesmo nome são reaproveitadas
    /// em vez de duplicadas.
    /// </summary>
    public static Library Merge(Library current, Library imported)
    {
        var merged = new Library
        {
            SchemaVersion = current.SchemaVersion,
            Folders = current.Folders.ToList(),
            Templates = current.Templates.ToList(),
        };

        var folderMap = new Dictionary<Guid, Guid>();
        foreach (var folder in imported.Folders.OrderBy(f => f.Order))
        {
            var existing = merged.Folders.FirstOrDefault(f => f.Name == folder.Name);
            if (existing is not null)
            {
                folderMap[folder.Id] = existing.Id;
                continue;
            }

            var copy = new Folder
            {
                Name = folder.Name,
                Symbol = folder.Symbol,
                Order = merged.Folders.Count == 0 ? 0 : merged.Folders.Max(f => f.Order) + 1,
            };
            folderMap[folder.Id] = copy.Id;
            merged.Folders.Add(copy);
        }

        foreach (var template in imported.Templates)
        {
            merged.Templates.Add(new Template
            {
                Title = template.Title,
                Body = template.Body,
                FolderId = template.FolderId is { } id && folderMap.TryGetValue(id, out var mapped) ? mapped : null,
                Tags = new List<string>(template.Tags),
            });
        }

        return merged;
    }
}
