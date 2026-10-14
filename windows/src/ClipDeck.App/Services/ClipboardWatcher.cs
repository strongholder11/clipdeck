using Avalonia.Threading;
using ClipDeck.App.Platform;

namespace ClipDeck.App.Services;

/// <summary>Uma cópia de texto feita com Ctrl+C.</summary>
public sealed record ClipboardEntry(Guid Id, string Text, DateTimeOffset CopiedAt)
{
    /// <summary>Primeira linha, para listar sem quebrar o layout.</summary>
    public string Preview =>
        Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
}

/// <summary>
/// Guarda as últimas cópias de texto, para a paleta oferecer transformá-las em
/// template.
///
/// Compara o número de sequência da área de transferência a cada meio segundo:
/// rápido o bastante para nunca parecer atrasado, e leve porque lê um inteiro,
/// não o conteúdo. Mesma estratégia da versão macOS.
/// </summary>
public sealed class ClipboardWatcher
{
    private readonly int _capacity;
    private readonly List<ClipboardEntry> _entries = new();
    private readonly DispatcherTimer _timer;
    private uint _lastSequence;

    public ClipboardWatcher(int capacity = 30, TimeSpan? pollInterval = null)
    {
        _capacity = capacity;
        _lastSequence = ClipboardHelper.SequenceNumber;

        _timer = new DispatcherTimer { Interval = pollInterval ?? TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    public IReadOnlyList<ClipboardEntry> Entries => _entries;

    public ClipboardEntry? MostRecent => _entries.FirstOrDefault();

    /// <summary>Verifica agora, sem esperar o próximo ciclo.</summary>
    public void Poll()
    {
        var sequence = ClipboardHelper.SequenceNumber;
        if (sequence == _lastSequence) return;
        _lastSequence = sequence;

        // Ignora o que nós mesmos escrevemos ao colar um template.
        if (ClipboardHelper.WasWrittenByUs(sequence)) return;

        var text = ClipboardHelper.ReadText();
        if (string.IsNullOrWhiteSpace(text)) return;

        Record(text);
    }

    private void Record(string text)
    {
        // Copiar duas vezes o mesmo texto sobe o item em vez de duplicá-lo.
        _entries.RemoveAll(e => e.Text == text);
        _entries.Insert(0, new ClipboardEntry(Guid.NewGuid(), text, DateTimeOffset.Now));

        if (_entries.Count > _capacity) _entries.RemoveRange(_capacity, _entries.Count - _capacity);
    }
}
