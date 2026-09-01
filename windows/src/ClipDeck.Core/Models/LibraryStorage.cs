using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipDeck.Core.Models;

public enum LoadOutcomeKind { Loaded, SeededNewFile, RecoveredFromBackup, SeededAfterCorruption }

public readonly record struct LoadOutcome(LoadOutcomeKind Kind, string? Detail = null);

public readonly record struct LoadResult(Library Library, LoadOutcome Outcome);

/// <summary>
/// Leitura e escrita da biblioteca em disco. Sem estado observável, para poder
/// testar escrita atômica, backup e recuperação sem levantar o app.
/// </summary>
public sealed class LibraryStorage
{
    private const int MaxBackups = 5;

    public string Directory { get; }

    public LibraryStorage(string directory) => Directory = directory;

    /// <summary>
    /// %APPDATA%\ClipDeck no Windows; ~/.config/ClipDeck no macOS e Linux.
    /// </summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create),
        "ClipDeck");

    public string FilePath => Path.Combine(Directory, "library.json");
    public string BackupsDirectory => Path.Combine(Directory, "Backups");

    // JSON legível e estável: dá para versionar, sincronizar e editar à mão sem
    // o diff explodir a cada gravação.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Carrega a biblioteca, degradando em vez de falhar. Sem arquivo, semeia.
    /// Com arquivo corrompido, tenta o backup mais recente antes de desistir — e
    /// nunca apaga o arquivo ruim: renomeia para .corrupt para resgate manual.
    /// </summary>
    public LoadResult Load()
    {
        System.IO.Directory.CreateDirectory(Directory);

        if (!File.Exists(FilePath))
        {
            var seeded = Library.Seeded();
            Write(seeded);
            return new LoadResult(seeded, new LoadOutcome(LoadOutcomeKind.SeededNewFile));
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var library = JsonSerializer.Deserialize<Library>(json, Options)
                          ?? throw new JsonException("conteúdo nulo");
            return new LoadResult(library, new LoadOutcome(LoadOutcomeKind.Loaded));
        }
        catch (Exception)
        {
            var quarantine = Path.Combine(Directory, $"library.corrupt-{Timestamp()}.json");
            try { File.Move(FilePath, quarantine); } catch { /* melhor esforço */ }

            var recovered = MostRecentBackup();
            if (recovered is { } found)
            {
                Write(found.Library);
                return new LoadResult(found.Library,
                    new LoadOutcome(LoadOutcomeKind.RecoveredFromBackup, found.Name));
            }

            var seeded = Library.Seeded();
            Write(seeded);
            return new LoadResult(seeded,
                new LoadOutcome(LoadOutcomeKind.SeededAfterCorruption, Path.GetFileName(quarantine)));
        }
    }

    /// <summary>Grava de forma atômica, com backup do estado anterior.</summary>
    public void Save(Library library)
    {
        System.IO.Directory.CreateDirectory(Directory);
        BackupCurrentFile();
        Write(library);
    }

    private void Write(Library library)
    {
        var json = JsonSerializer.Serialize(library, Options);

        // Grava num temporário e substitui: uma queda no meio da escrita deixa o
        // arquivo antigo intacto em vez de meio arquivo.
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);

        if (File.Exists(FilePath))
        {
            File.Replace(temp, FilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temp, FilePath);
        }
    }

    private void BackupCurrentFile()
    {
        if (!File.Exists(FilePath)) return;

        System.IO.Directory.CreateDirectory(BackupsDirectory);
        var destination = Path.Combine(BackupsDirectory, $"library-{Timestamp()}.json");
        try { File.Copy(FilePath, destination, overwrite: true); } catch { return; }

        PruneBackups();
    }

    private void PruneBackups()
    {
        if (!System.IO.Directory.Exists(BackupsDirectory)) return;

        var stale = System.IO.Directory
            .GetFiles(BackupsDirectory, "library-*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(MaxBackups);

        foreach (var file in stale)
        {
            try { File.Delete(file); } catch { /* melhor esforço */ }
        }
    }

    private (Library Library, string Name)? MostRecentBackup()
    {
        if (!System.IO.Directory.Exists(BackupsDirectory)) return null;

        var candidates = System.IO.Directory
            .GetFiles(BackupsDirectory, "library-*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            try
            {
                var library = JsonSerializer.Deserialize<Library>(File.ReadAllText(candidate), Options);
                if (library is not null) return (library, Path.GetFileName(candidate));
            }
            catch { /* tenta o próximo */ }
        }

        return null;
    }

    /// <summary>
    /// Timestamp ordenável lexicograficamente — a rotação de backups ordena por
    /// nome de arquivo e depende disso.
    /// </summary>
    private static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
}
