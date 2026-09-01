using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;
using ClipDeck.Core.Search;

namespace ClipDeck.App.ViewModels;

/// <summary>Uma linha da paleta, já formatada para exibição.</summary>
public sealed class PaletteRow
{
    public required Template Template { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Shortcut { get; init; }
}

public sealed class PaletteViewModel : INotifyPropertyChanged
{
    private readonly TemplateStore _store;
    private string _query = string.Empty;
    private int _selectedIndex;

    public PaletteViewModel(TemplateStore store)
    {
        _store = store;
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
                Subtitle = string.Join("  ·  ", pieces),
                // Atalho direto para os 9 primeiros, para quando já se sabe a posição.
                Shortcut = i < 9 ? $"Ctrl+{i + 1}" : string.Empty,
            });
        }

        // A seleção volta ao topo a cada digitação: o melhor resultado muda, e
        // manter o índice antigo faria colar o template errado.
        SelectedIndex = Rows.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(ResultCount));
    }

    public string ResultCount => Rows.Count.ToString();

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
