using ClipDeck.Core.Models;

namespace ClipDeck.App.Services;

/// <summary>
/// Estado da biblioteca em memória.
///
/// Grava com atraso: editar um template dispara uma alteração por tecla
/// digitada, e sem isso viraria centenas de escritas em disco.
/// </summary>
public sealed class TemplateStore : IDisposable
{
    private readonly LibraryStorage _storage;
    private readonly TimeSpan _saveDelay;
    private readonly Lock _gate = new();
    private Timer? _saveTimer;

    public Library Library { get; private set; }
    public LoadOutcome LastLoadOutcome { get; }

    /// <summary>Disparado quando a biblioteca muda, para as telas se atualizarem.</summary>
    public event Action? Changed;

    public TemplateStore(LibraryStorage? storage = null, TimeSpan? saveDelay = null)
    {
        _storage = storage ?? new LibraryStorage(LibraryStorage.DefaultDirectory);
        _saveDelay = saveDelay ?? TimeSpan.FromMilliseconds(500);

        var result = _storage.Load();
        Library = result.Library;
        LastLoadOutcome = result.Outcome;
    }

    public IReadOnlyList<Template> Templates => Library.Templates;

    public IReadOnlyList<Folder> Folders =>
        Library.Folders.OrderBy(f => f.Order).ToList();

    public Folder? FolderById(Guid? id) => Library.FolderById(id);

    /// <summary>Todas as tags em uso, ordenadas por frequência.</summary>
    public IReadOnlyList<string> AllTags =>
        Library.Templates
            .SelectMany(t => t.Tags)
            .GroupBy(t => t, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => g.Key)
            .ToList();

    public void Add(Template template)
    {
        Library.Templates.Add(template);
        Touch();
    }

    public void Update(Template template)
    {
        var index = Library.Templates.FindIndex(t => t.Id == template.Id);
        if (index < 0) return;

        template.UpdatedAt = DateTimeOffset.Now;
        Library.Templates[index] = template;
        Touch();
    }

    public void Delete(Guid templateId)
    {
        Library.Templates.RemoveAll(t => t.Id == templateId);
        Touch();
    }

    /// <summary>Registra um uso. É o que alimenta o ranking por frequência.</summary>
    public void RecordUse(Guid templateId)
    {
        var template = Library.Templates.FirstOrDefault(t => t.Id == templateId);
        if (template is null) return;

        template.UseCount++;
        template.LastUsedAt = DateTimeOffset.Now;
        Touch();
    }

    public void AddFolder(Folder folder)
    {
        Library.Folders.Add(folder);
        Touch();
    }

    /// <summary>Remove a pasta e solta os templates dela na raiz, sem apagá-los.</summary>
    public void DeleteFolder(Guid folderId)
    {
        Library.Folders.RemoveAll(f => f.Id == folderId);
        foreach (var template in Library.Templates.Where(t => t.FolderId == folderId))
        {
            template.FolderId = null;
        }
        Touch();
    }

    public void Move(Guid templateId, Guid? folderId)
    {
        var template = Library.Templates.FirstOrDefault(t => t.Id == templateId);
        if (template is null) return;

        template.FolderId = folderId;
        template.UpdatedAt = DateTimeOffset.Now;
        Touch();
    }

    public void ReplaceLibrary(Library library)
    {
        Library = library;
        Touch();
    }

    private void Touch()
    {
        Changed?.Invoke();
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        lock (_gate)
        {
            _saveTimer?.Dispose();
            _saveTimer = new Timer(_ => SaveNow(), null, _saveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Grava imediatamente. Usado ao encerrar, onde não há tempo de espera.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
        SaveNow();
    }

    private void SaveNow()
    {
        try { _storage.Save(Library); }
        catch (Exception ex) { Console.Error.WriteLine($"[clipdeck] falha ao gravar: {ex.Message}"); }
    }

    public void Dispose() => Flush();
}
