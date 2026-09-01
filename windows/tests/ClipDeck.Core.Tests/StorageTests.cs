using System.Text.Json;
using ClipDeck.Core.Models;

namespace ClipDeck.Core.Tests;

public class LibraryStorageTests : IDisposable
{
    private readonly string _dir;
    private readonly LibraryStorage _storage;

    public LibraryStorageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "clipdeck-test-" + Guid.NewGuid());
        _storage = new LibraryStorage(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* melhor esforço */ }
    }

    [Fact]
    public void SemeiaQuandoNaoHaArquivo()
    {
        var result = _storage.Load();
        Assert.Equal(LoadOutcomeKind.SeededNewFile, result.Outcome.Kind);
        Assert.NotEmpty(result.Library.Folders);
        Assert.True(File.Exists(_storage.FilePath));
    }

    [Fact]
    public void RoundtripPreservaTudo()
    {
        var folder = new Folder { Name = "Cobrança", Order = 3 };
        var template = new Template
        {
            Title = "Aviso de vencimento",
            Body = "Oi {{nome}}, sua fatura vence em {{data}}.",
            FolderId = folder.Id,
            Tags = { "urgente", "financeiro" },
            UseCount = 7,
        };

        _storage.Save(new Library { Folders = { folder }, Templates = { template } });
        var reloaded = _storage.Load().Library;

        var loaded = Assert.Single(reloaded.Templates);
        Assert.Equal("Aviso de vencimento", loaded.Title);
        Assert.Equal(7, loaded.UseCount);
        Assert.Equal(new[] { "urgente", "financeiro" }, loaded.Tags);
        Assert.Equal(folder.Id, loaded.FolderId);
        Assert.Equal("Cobrança", reloaded.Folders[0].Name);
    }

    [Fact]
    public void RecuperaDeBackupQuandoArquivoCorrompe()
    {
        var good = new Library { Templates = { new Template { Title = "Bom template", Body = "conteúdo" } } };
        _storage.Save(good);
        _storage.Save(good); // a segunda gravação gera o backup da primeira

        File.WriteAllText(_storage.FilePath, "{ isso não é json");

        var result = _storage.Load();
        Assert.Equal(LoadOutcomeKind.RecoveredFromBackup, result.Outcome.Kind);
        Assert.Equal("Bom template", result.Library.Templates[0].Title);
        Assert.NotEmpty(Directory.GetFiles(_dir, "library.corrupt-*.json"));
    }

    [Fact]
    public void SemeiaQuandoCorrompidoESemBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_storage.FilePath, "lixo");
        Assert.Equal(LoadOutcomeKind.SeededAfterCorruption, _storage.Load().Outcome.Kind);
    }

    [Fact]
    public void MantemNoMaximoCincoBackups()
    {
        for (var i = 0; i < 9; i++)
        {
            _storage.Save(new Library { Templates = { new Template { Title = $"v{i}", Body = "x" } } });
        }

        var backups = Directory.GetFiles(_storage.BackupsDirectory, "library-*.json");
        Assert.InRange(backups.Length, 1, 5);
    }

    [Fact]
    public void CarregaJsonEscritoAMaoSemCamposOpcionais()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var library = JsonSerializer.Deserialize<Library>(
            """{ "templates": [ { "title": "Só título" } ] }""", options);

        Assert.NotNull(library);
        Assert.Equal("Só título", library!.Templates[0].Title);
        Assert.Equal(0, library.Templates[0].UseCount);
        Assert.Empty(library.Folders);
    }
}
