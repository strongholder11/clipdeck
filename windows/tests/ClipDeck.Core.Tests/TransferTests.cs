using System.Text.Json;
using ClipDeck.Core.Models;

namespace ClipDeck.Core.Tests;

public class LibraryTransferTests
{
    // Trecho real de um library.json gravado pela versão macOS.
    private const string MacExport = """
        {
          "folders" : [
            { "id" : "BA517207-0948-46C5-8971-D2809A2E6FFB", "name" : "Prospecção", "order" : 0, "symbol" : "sparkle.magnifyingglass" }
          ],
          "schemaVersion" : 1,
          "templates" : [
            {
              "body" : "Oi {{nome}}, tudo bem?",
              "createdAt" : "2026-08-19T12:07:49Z",
              "folderID" : "BA517207-0948-46C5-8971-D2809A2E6FFB",
              "id" : "AF5BF6C8-4C1E-4F4A-A6AA-D9A5AC4A651F",
              "lastUsedAt" : "2026-08-19T12:52:12Z",
              "tags" : [ "frio" ],
              "title" : "Primeiro contato",
              "updatedAt" : "2026-08-19T12:07:49Z",
              "useCount" : 2
            }
          ]
        }
        """;

    [Fact]
    public void LeArquivoExportadoNoMac()
    {
        var library = LibraryTransfer.Deserialize(MacExport);

        var template = Assert.Single(library.Templates);
        Assert.Equal(library.Folders[0].Id, template.FolderId);
        Assert.Equal(2, template.UseCount);
        Assert.Equal(new DateTimeOffset(2026, 8, 19, 12, 52, 12, TimeSpan.Zero), template.LastUsedAt);
    }

    [Fact]
    public void ExportaNoFormatoQueOMacLe()
    {
        var folder = new Folder { Name = "Vendas" };
        var library = new Library
        {
            Folders = { folder },
            Templates =
            {
                new Template
                {
                    Title = "Oi", FolderId = folder.Id,
                    CreatedAt = new DateTimeOffset(2026, 1, 2, 9, 30, 15, 123, TimeSpan.FromHours(-3)),
                },
            },
        };

        var json = LibraryTransfer.Serialize(library);
        using var doc = JsonDocument.Parse(json);
        var template = doc.RootElement.GetProperty("templates")[0];

        // O Swift procura exatamente "folderID".
        Assert.Equal(folder.Id, template.GetProperty("folderID").GetGuid());
        // E recusa fração de segundo e fuso diferente de Z.
        Assert.Equal("2026-01-02T12:30:15Z", template.GetProperty("createdAt").GetString());
    }

    [Fact]
    public void RecusaJsonQueNaoEDoClipDeck()
    {
        Assert.Throws<JsonException>(() => LibraryTransfer.Deserialize("{}"));
        Assert.ThrowsAny<JsonException>(() => LibraryTransfer.Deserialize("não é json"));
    }

    [Fact]
    public void MesclarReaproveitaPastaDeMesmoNomeERegeraIds()
    {
        var current = LibraryTransfer.Deserialize(MacExport);
        var imported = LibraryTransfer.Deserialize(MacExport);
        imported.Folders.Add(new Folder { Name = "Nova", Order = 5 });
        imported.Templates.Add(new Template { Title = "Na nova", FolderId = imported.Folders[1].Id });

        var merged = LibraryTransfer.Merge(current, imported);

        Assert.Equal(2, merged.Folders.Count);
        Assert.Equal(3, merged.Templates.Count);
        Assert.Equal(3, merged.Templates.Select(t => t.Id).Distinct().Count());

        var prospeccao = merged.Folders.Single(f => f.Name == "Prospecção");
        Assert.Equal(2, merged.Templates.Count(t => t.FolderId == prospeccao.Id));

        var nova = merged.Folders.Single(f => f.Name == "Nova");
        Assert.Equal(1, nova.Order);
        Assert.Equal(nova.Id, merged.Templates.Single(t => t.Title == "Na nova").FolderId);
    }

    [Fact]
    public void MesclarNaoAlteraABibliotecaAtual()
    {
        var current = LibraryTransfer.Deserialize(MacExport);
        LibraryTransfer.Merge(current, LibraryTransfer.Deserialize(MacExport));
        Assert.Single(current.Templates);
    }
}
