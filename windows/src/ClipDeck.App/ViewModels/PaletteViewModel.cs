using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;
using ClipDeck.Core.Search;

namespace ClipDeck.App.ViewModels;

/// <summary>
/// Uma linha da paleta, já formatada para exibição.
///
/// Templates e cópias recentes convivem na mesma lista para a navegação por
/// teclado ser uma só — com duas listas, a seta teria que "pular" de uma para a
/// outra e a seleção viraria um caso especial em todo lugar.
/// </summary>
public sealed class PaletteRow
{
    /// <summary>Preenchido nas linhas de template.</summary>
    public Template? Template { get; init; }

    /// <summary>Preenchido nas linhas de cópia recente.</summary>
    public ClipboardEntry? RecentCopy { get; init; }

    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Shortcut { get; init; }
    public IReadOnlyList<int> TitleHighlights { get; init; } = Array.Empty<int>();

    /// <summary>Primeira cópia recente: leva o cabeçalho da seção acima dela.</summary>
    public bool StartsRecentSection { get; init; }

    public bool IsRecent => RecentCopy is not null;
}

public sealed class PaletteViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Quantas cópias recentes mostrar. Poucas de propósito: a lista existe para
    /// capturar o que você acabou de escrever, não para virar um histórico.
    /// </summary>
    private const int RecentLimit = 3;

    private readonly TemplateStore _store;
    private readonly ClipboardWatcher? _clipboard;
    private string _query = string.Empty;
    private int _selectedIndex;

    public PaletteViewModel(TemplateStore store, ClipboardWatcher? clipboard = null)
    {
        _store = store;
        _clipboard = clipboard;
        Refresh();
    }

    public ObservableCollection<PaletteRow> Rows { get; } = new();

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            _query = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            OnPropertyChanged();
        }
    }

    public PaletteRow? SelectedRow =>
        SelectedIndex >= 0 && SelectedIndex < Rows.Count ? Rows[SelectedIndex] : null;

    public bool IsEmpty => Rows.Count == 0;

    public string EmptyMessage =>
        Query.Length == 0 ? "Nenhum template ainda" : "Nada encontrado";

    public void Refresh()
    {
        var results = SearchEngine.Search(Query, _store.Templates, _store.Folders);

        Rows.Clear();
        for (var i = 0; i < results.Count; i++)
        {
            var template = results[i].Template;
            var folder = _store.FolderById(template.FolderId);

            var pieces = new List<string>();
            if (folder is not null) pieces.Add(folder.Name);
            if (template.Tags.Count > 0) pieces.Add(string.Join(' ', template.Tags.Select(t => "#" + t)));
            if (template.Preview.Length > 0) pieces.Add(template.Preview);

            Rows.Add(new PaletteRow
            {
                Template = template,
                Title = template.Title,
                TitleHighlights = results[i].TitleHighlights,
                Subtitle = string.Join("  ·  ", pieces),
                // Atalho direto para os 9 primeiros, para quando já se sabe a posição.
                Shortcut = i < 9 ? $"Ctrl+{i + 1}" : string.Empty,
            });
        }

        TemplateCount = results.Count;

        // As recentes só aparecem sem busca ativa: com uma consulta digitada, a
        // lista precisa ser só o que casa com ela.
        if (Query.Length == 0 && _clipboard is not null)
        {
            var first = true;
            foreach (var entry in _clipboard.Entries.Take(RecentLimit))
            {
                Rows.Add(new PaletteRow
                {
                    RecentCopy = entry,
                    Title = entry.Preview,
                    Subtitle = entry.CopiedAt.ToString("HH:mm"),
                    Shortcut = "+",
                    StartsRecentSection = first,
                });
                first = false;
            }
        }

        // A seleção volta ao topo a cada digitação: o melhor resultado muda, e
        // manter o índice antigo faria colar o template errado.
        SelectedIndex = Rows.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(ResultCount));
    }

    /// <summary>Quantos templates (não cópias recentes) estão na lista.</summary>
    public int TemplateCount { get; private set; }

    public string ResultCount => TemplateCount.ToString();

    public void Reset()
    {
        _query = string.Empty;
        OnPropertyChanged(nameof(Query));
        Refresh();
    }

    public void MoveSelection(int delta)
    {
        if (Rows.Count == 0) return;
        // Circular: descer no último volta ao primeiro.
        SelectedIndex = (SelectedIndex + delta + Rows.Count) % Rows.Count;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
