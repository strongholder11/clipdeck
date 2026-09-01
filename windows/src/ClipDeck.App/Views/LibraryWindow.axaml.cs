using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;
using ClipDeck.Core.Search;
using ClipDeck.Core.Variables;

namespace ClipDeck.App.Views;

/// <summary>Janela de gerenciamento: pastas, lista e editor.</summary>
public partial class LibraryWindow : Window
{
    private readonly TemplateStore _store;
    private List<Folder> _folders = new();
    private List<Template> _visible = new();
    private Template? _selected;

    /// <summary>Evita gravar enquanto os campos são preenchidos por código.</summary>
    private bool _loadingEditor;

    private Guid? _folderFilter;
    private string? _tagFilter;

    public LibraryWindow(TemplateStore store)
    {
        _store = store;
        InitializeComponent();

        this.FindControl<Button>("NewFolderButton")!.Click += (_, _) => CreateFolder();
        this.FindControl<Button>("NewTemplateButton")!.Click += (_, _) => CreateTemplate();
        this.FindControl<Button>("DeleteButton")!.Click += (_, _) => DeleteSelected();
        this.FindControl<TextBox>("FilterBox")!.TextChanged += (_, _) => RefreshList();

        var list = this.FindControl<ListBox>("TemplateList")!;
        list.SelectionChanged += (_, _) =>
        {
            _selected = list.SelectedIndex >= 0 && list.SelectedIndex < _visible.Count
                ? _visible[list.SelectedIndex]
                : null;
            LoadEditor();
        };

        foreach (var name in new[] { "EditTitle", "EditTags", "EditBody" })
        {
            this.FindControl<TextBox>(name)!.TextChanged += (_, _) => CommitEditor();
        }
        this.FindControl<ComboBox>("EditFolder")!.SelectionChanged += (_, _) => CommitEditor();

        RefreshSidebar();
        RefreshList();

        // Abre já mostrando algo: um painel de edição vazio na abertura não ajuda.
        if (_visible.Count > 0) list.SelectedIndex = 0;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RefreshSidebar()
    {
        _folders = _store.Folders.ToList();
        var tree = this.FindControl<TreeView>("SidebarTree")!;

        var items = new List<TreeViewItem>
        {
            MakeNode("Todos os templates", () => { _folderFilter = null; _tagFilter = null; RefreshList(); }),
        };

        var foldersNode = new TreeViewItem { Header = "Pastas", IsExpanded = true };
        var folderChildren = new List<TreeViewItem>();
        foreach (var folder in _folders)
        {
            var id = folder.Id;
            folderChildren.Add(MakeNode(folder.Name, () => { _folderFilter = id; _tagFilter = null; RefreshList(); }));
        }
        foldersNode.ItemsSource = folderChildren;
        items.Add(foldersNode);

        var tags = _store.AllTags;
        if (tags.Count > 0)
        {
            var tagsNode = new TreeViewItem { Header = "Tags", IsExpanded = true };
            tagsNode.ItemsSource = tags
                .Select(tag => MakeNode("#" + tag, () => { _tagFilter = tag; _folderFilter = null; RefreshList(); }))
                .ToList();
            items.Add(tagsNode);
        }

        tree.ItemsSource = items;
    }

    private static TreeViewItem MakeNode(string header, Action onSelect)
    {
        var node = new TreeViewItem { Header = header };
        node.PointerPressed += (_, _) => onSelect();
        return node;
    }

    private void RefreshList()
    {
        var scoped = _store.Templates.AsEnumerable();
        if (_folderFilter is { } folderId) scoped = scoped.Where(t => t.FolderId == folderId);
        if (_tagFilter is { } tag) scoped = scoped.Where(t => t.Tags.Contains(tag));

        var filter = this.FindControl<TextBox>("FilterBox")!.Text ?? string.Empty;
        var list = scoped.ToList();

        // Reusa o mesmo motor da paleta, para o filtro daqui se comportar como a
        // busca que o usuário já conhece.
        _visible = filter.Length == 0
            ? list.OrderBy(t => t.Title, StringComparer.CurrentCulture).ToList()
            : SearchEngine.Search(filter, list, _folders).Select(r => r.Template).ToList();

        this.FindControl<ListBox>("TemplateList")!.ItemsSource = _visible;
        this.FindControl<TextBlock>("CountText")!.Text = _visible.Count.ToString();
    }

    private void LoadEditor()
    {
        this.FindControl<Grid>("EditorPanel")!.IsVisible = _selected is not null;
        this.FindControl<StackPanel>("EmptyPanel")!.IsVisible = _selected is null;
        if (_selected is null) return;

        _loadingEditor = true;

        this.FindControl<TextBox>("EditTitle")!.Text = _selected.Title;
        this.FindControl<TextBox>("EditBody")!.Text = _selected.Body;
        this.FindControl<TextBox>("EditTags")!.Text = string.Join(' ', _selected.Tags);
        this.FindControl<TextBlock>("UsageText")!.Text = $"usado {_selected.UseCount}×";

        var folderBox = this.FindControl<ComboBox>("EditFolder")!;
        folderBox.ItemsSource = new[] { "Sem pasta" }.Concat(_folders.Select(f => f.Name)).ToList();
        folderBox.SelectedIndex = _selected.FolderId is { } id
            ? _folders.FindIndex(f => f.Id == id) + 1
            : 0;

        _loadingEditor = false;
        RefreshVariables();
    }

    /// <summary>
    /// Mostra as variáveis presentes no texto, para conferir se o nome está certo:
    /// {{nome}} e {{Nome}} são variáveis diferentes, e sem essa lista o erro só
    /// aparece na hora de colar.
    /// </summary>
    private void RefreshVariables()
    {
        var body = this.FindControl<TextBox>("EditBody")!.Text ?? string.Empty;
        var variables = TemplateRenderer.Variables(body);

        this.FindControl<TextBlock>("VariablesLabel")!.IsVisible = variables.Count > 0;
        this.FindControl<ItemsControl>("VariablesHost")!.ItemsSource = variables
            .Select(v => new Border
            {
                Background = new SolidColorBrush(
                    v.IsBuiltIn ? Color.Parse("#3A5AC8") : Color.Parse("#40808080")),
                CornerRadius = new Avalonia.CornerRadius(9),
                Padding = new Avalonia.Thickness(7, 2),
                Margin = new Avalonia.Thickness(0, 0, 5, 5),
                Child = new TextBlock { Text = v.Name, FontSize = 11 },
            })
            .ToList();
    }

    private void CommitEditor()
    {
        if (_loadingEditor || _selected is null) return;

        var updated = _selected.Clone();
        updated.Title = this.FindControl<TextBox>("EditTitle")!.Text ?? string.Empty;
        updated.Body = this.FindControl<TextBox>("EditBody")!.Text ?? string.Empty;
        updated.Tags = (this.FindControl<TextBox>("EditTags")!.Text ?? string.Empty)
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimStart('#'))
            .ToList();

        var folderIndex = this.FindControl<ComboBox>("EditFolder")!.SelectedIndex;
        updated.FolderId = folderIndex > 0 ? _folders[folderIndex - 1].Id : null;

        _store.Update(updated);
        _selected = updated;
        RefreshVariables();
    }

    private void CreateFolder()
    {
        var order = _folders.Count == 0 ? 0 : _folders.Max(f => f.Order) + 1;
        _store.AddFolder(new Folder { Name = "Nova pasta", Order = order });
        RefreshSidebar();
    }

    private void CreateTemplate()
    {
        // Já nasce na pasta selecionada: é quase sempre onde se quer.
        var template = new Template { Title = "Novo template", Body = "", FolderId = _folderFilter };
        _store.Add(template);
        RefreshList();

        var index = _visible.FindIndex(t => t.Id == template.Id);
        if (index >= 0) this.FindControl<ListBox>("TemplateList")!.SelectedIndex = index;
    }

    private void DeleteSelected()
    {
        if (_selected is null) return;
        _store.Delete(_selected.Id);
        _selected = null;
        RefreshList();
        LoadEditor();
    }
}
