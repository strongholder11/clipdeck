using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;
using ClipDeck.Core.Search;
using ClipDeck.Core.Variables;

namespace ClipDeck.App.Views;

/// <summary>Janela de gerenciamento: pastas, lista e editor.</summary>
public partial class LibraryWindow : Window
{
    /// <summary>O que está escolhido na barra lateral.</summary>
    private enum Scope { All, Unfiled, Folder, Tag }

    private readonly TemplateStore _store;
    private List<Folder> _folders = new();
    private List<Template> _visible = new();
    private Template? _selected;

    /// <summary>Evita gravar enquanto os campos são preenchidos por código.</summary>
    private bool _loadingEditor;

    /// <summary>Evita tratar como escolha do usuário a seleção refeita após atualizar a lista.</summary>
    private bool _refreshingList;

    /// <summary>A mudança na biblioteca veio do próprio editor: não recarregar os campos.</summary>
    private bool _committing;

    private string _sidebarSignature = string.Empty;

    private Scope _scope = Scope.All;
    private Guid? _folderFilter;
    private string? _tagFilter;

    public LibraryWindow(TemplateStore store)
    {
        _store = store;
        InitializeComponent();

        this.FindControl<Button>("NewFolderButton")!.Click += (_, _) => BeginNewFolder();
        this.FindControl<Button>("ConfirmNewFolderButton")!.Click += (_, _) => CreateFolder();
        this.FindControl<Button>("CancelNewFolderButton")!.Click += (_, _) => EndNewFolder();
        this.FindControl<TextBox>("NewFolderName")!.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CreateFolder(); e.Handled = true; }
            if (e.Key == Key.Escape) { EndNewFolder(); e.Handled = true; }
        };

        this.FindControl<Button>("NewTemplateButton")!.Click += (_, _) => CreateTemplate();
        this.FindControl<Button>("DeleteButton")!.Click += async (_, _) =>
        {
            if (_selected is { } template) await ConfirmDelete(template);
        };
        this.FindControl<TextBox>("FilterBox")!.TextChanged += (_, _) => RefreshList();

        var list = this.FindControl<ListBox>("TemplateList")!;
        list.SelectionChanged += (_, _) =>
        {
            if (_refreshingList) return;
            _selected = list.SelectedIndex >= 0 && list.SelectedIndex < _visible.Count
                ? _visible[list.SelectedIndex]
                : null;
            LoadEditor();
        };
        // O menu age sobre a linha clicada com o botão direito, que nem sempre é
        // a selecionada.
        list.ContextRequested += (_, e) =>
        {
            var item = (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
            var index = item is null ? -1 : list.IndexFromContainer(item);
            if (index >= 0 && index < _visible.Count) list.ContextMenu = TemplateMenu(_visible[index]);
            else e.Handled = true;
        };

        foreach (var name in new[] { "EditTitle", "EditTags", "EditBody" })
        {
            this.FindControl<TextBox>(name)!.TextChanged += (_, _) => CommitEditor();
        }
        this.FindControl<ComboBox>("EditFolder")!.SelectionChanged += (_, _) => CommitEditor();

        // A biblioteca pode mudar por fora — uma importação pela bandeja, uma
        // captura com a janela aberta. Sem isso a tela mostraria dados velhos.
        _store.Changed += OnStoreChanged;
        Closed += (_, _) =>
        {
            _store.Changed -= OnStoreChanged;
            // Grava ao fechar em vez de esperar o atraso: é o momento em que se
            // espera que o trabalho esteja salvo.
            _store.Flush();
        };

        RefreshSidebar();
        RefreshList();

        // Abre já mostrando algo: um painel de edição vazio na abertura não ajuda.
        if (_visible.Count > 0) list.SelectedIndex = 0;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnStoreChanged() => Dispatcher.UIThread.Post(() =>
    {
        RefreshSidebar();
        RefreshList();

        // Vindo do próprio editor, recarregar os campos tiraria o cursor do lugar
        // a cada tecla.
        if (!_committing) LoadEditor();
    });

    // MARK: - Barra lateral

    private void RefreshSidebar()
    {
        _folders = _store.Folders.ToList();
        var tags = _store.AllTags;

        // Reconstruir a árvore a cada tecla digitada no editor faria a barra
        // piscar: só refaz quando pastas ou tags realmente mudaram.
        var signature = string.Join("|", _folders.Select(f => f.Id + f.Name)) + "#" + string.Join("|", tags.OrderBy(t => t));
        if (signature == _sidebarSignature) return;
        _sidebarSignature = signature;

        // A pasta filtrada pode ter sido apagada (ou sumido numa importação).
        if (_scope == Scope.Folder && _folders.All(f => f.Id != _folderFilter)) SetScope(Scope.All);
        if (_scope == Scope.Tag && !tags.Contains(_tagFilter!)) SetScope(Scope.All);

        var items = new List<TreeViewItem>
        {
            MakeNode("Todos os templates", () => SetScope(Scope.All)),
            MakeNode("Sem pasta", () => SetScope(Scope.Unfiled)),
        };

        var foldersNode = new TreeViewItem { Header = "Pastas", IsExpanded = true };
        var folderChildren = new List<TreeViewItem>();
        foreach (var folder in _folders)
        {
            var node = MakeNode(folder.Name, () => SetScope(Scope.Folder, folderId: folder.Id));
            var delete = new MenuItem { Header = "Apagar pasta" };
            delete.Click += async (_, _) => await ConfirmDeleteFolder(folder);
            node.ContextMenu = new ContextMenu { ItemsSource = new[] { delete } };
            folderChildren.Add(node);
        }
        foldersNode.ItemsSource = folderChildren;
        items.Add(foldersNode);

        if (tags.Count > 0)
        {
            var tagsNode = new TreeViewItem { Header = "Tags", IsExpanded = true };
            tagsNode.ItemsSource = tags
                .Select(tag => MakeNode("#" + tag, () => SetScope(Scope.Tag, tag: tag)))
                .ToList();
            items.Add(tagsNode);
        }

        this.FindControl<TreeView>("SidebarTree")!.ItemsSource = items;
    }

    private void SetScope(Scope scope, Guid? folderId = null, string? tag = null)
    {
        _scope = scope;
        _folderFilter = folderId;
        _tagFilter = tag;
        RefreshList();
    }

    private static TreeViewItem MakeNode(string header, Action onSelect)
    {
        var node = new TreeViewItem { Header = header };
        node.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(node).Properties.IsLeftButtonPressed) onSelect();
        };
        return node;
    }

    private void BeginNewFolder()
    {
        this.FindControl<StackPanel>("NewFolderRow")!.IsVisible = true;
        this.FindControl<Button>("NewFolderButton")!.IsVisible = false;
        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("NewFolderName")!.Focus());
    }

    private void EndNewFolder()
    {
        this.FindControl<TextBox>("NewFolderName")!.Text = string.Empty;
        this.FindControl<StackPanel>("NewFolderRow")!.IsVisible = false;
        this.FindControl<Button>("NewFolderButton")!.IsVisible = true;
    }

    private void CreateFolder()
    {
        var name = (this.FindControl<TextBox>("NewFolderName")!.Text ?? string.Empty).Trim();
        if (name.Length == 0) return;

        var order = _folders.Count == 0 ? 0 : _folders.Max(f => f.Order) + 1;
        _store.AddFolder(new Folder { Name = name, Order = order });
        EndNewFolder();
    }

    private async Task ConfirmDeleteFolder(Folder folder)
    {
        var count = _store.Templates.Count(t => t.FolderId == folder.Id);
        var detail = count == 0
            ? "A pasta está vazia."
            : $"Os {count} templates dela não são apagados: vão para \"Sem pasta\".";

        if (await MessageDialog.Confirm($"Apagar a pasta \"{folder.Name}\"?", detail, "Apagar", "Cancelar") == 0)
        {
            _store.DeleteFolder(folder.Id);
        }
    }

    // MARK: - Lista

    private void RefreshList()
    {
        var scoped = _scope switch
        {
            Scope.Unfiled => _store.Templates.Where(t => t.FolderId is null),
            Scope.Folder => _store.Templates.Where(t => t.FolderId == _folderFilter),
            Scope.Tag => _store.Templates.Where(t => t.Tags.Contains(_tagFilter!)),
            _ => _store.Templates.AsEnumerable(),
        };

        var filter = this.FindControl<TextBox>("FilterBox")!.Text ?? string.Empty;
        var list = scoped.ToList();

        // Reusa o mesmo motor da paleta, para o filtro daqui se comportar como a
        // busca que o usuário já conhece.
        _visible = filter.Length == 0
            ? list.OrderBy(t => t.Title, StringComparer.CurrentCulture).ToList()
            : SearchEngine.Search(filter, list, _folders).Select(r => r.Template).ToList();

        var listBox = this.FindControl<ListBox>("TemplateList")!;
        var keepId = _selected?.Id;

        _refreshingList = true;
        listBox.ItemsSource = _visible;
        var index = keepId is { } id ? _visible.FindIndex(t => t.Id == id) : -1;
        listBox.SelectedIndex = index;
        _refreshingList = false;

        // O selecionado saiu do filtro: o editor não pode continuar mostrando-o.
        if (index < 0 && _selected is not null)
        {
            _selected = null;
            LoadEditor();
        }
        else if (index >= 0)
        {
            _selected = _visible[index];
        }

        this.FindControl<TextBlock>("CountText")!.Text = _visible.Count.ToString();
    }

    private ContextMenu TemplateMenu(Template template)
    {
        var move = new MenuItem { Header = "Mover para" };
        var targets = new List<MenuItem>();
        var unfiled = new MenuItem { Header = "Sem pasta" };
        unfiled.Click += (_, _) => _store.Move(template.Id, null);
        targets.Add(unfiled);
        foreach (var folder in _folders)
        {
            var item = new MenuItem { Header = folder.Name };
            item.Click += (_, _) => _store.Move(template.Id, folder.Id);
            targets.Add(item);
        }
        move.ItemsSource = targets;

        var duplicate = new MenuItem { Header = "Duplicar" };
        duplicate.Click += (_, _) => Duplicate(template);

        var delete = new MenuItem { Header = "Apagar" };
        delete.Click += async (_, _) => await ConfirmDelete(template);

        return new ContextMenu { ItemsSource = new Control[] { move, new Separator(), duplicate, delete } };
    }

    // MARK: - Editor

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
        this.FindControl<TextBlock>("BuiltInHint")!.IsVisible = variables.Any(v => v.IsBuiltIn);
        this.FindControl<ItemsControl>("VariablesHost")!.ItemsSource = variables
            .Select(v => new Border
            {
                Background = new SolidColorBrush(
                    v.IsBuiltIn ? Color.Parse("#3A5AC8") : Color.Parse("#40808080")),
                CornerRadius = new Avalonia.CornerRadius(9),
                Padding = new Avalonia.Thickness(7, 2),
                Margin = new Avalonia.Thickness(0, 0, 5, 5),
                Child = new TextBlock { Text = v.IsBuiltIn ? v.Name + " ✨" : v.Name, FontSize = 11 },
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
            .Where(t => t.Length > 0)
            .ToList();

        var folderIndex = this.FindControl<ComboBox>("EditFolder")!.SelectedIndex;
        updated.FolderId = folderIndex > 0 ? _folders[folderIndex - 1].Id : null;

        _committing = true;
        try { _store.Update(updated); }
        finally { Dispatcher.UIThread.Post(() => _committing = false); }

        _selected = updated;
        var index = _visible.FindIndex(t => t.Id == updated.Id);
        if (index >= 0) _visible[index] = updated;
        RefreshVariables();
    }

    // MARK: - Ações

    private void CreateTemplate()
    {
        // Já nasce na pasta selecionada: é quase sempre onde se quer.
        var template = new Template
        {
            Title = "Novo template",
            Body = "",
            FolderId = _scope == Scope.Folder ? _folderFilter : null,
        };
        Select(template, add: true);
    }

    private void Duplicate(Template template)
    {
        var copy = new Template
        {
            Title = template.Title + " (cópia)",
            Body = template.Body,
            FolderId = template.FolderId,
            Tags = new List<string>(template.Tags),
        };
        Select(copy, add: true);
    }

    private void Select(Template template, bool add)
    {
        _selected = template;
        if (add) _store.Add(template);

        // A adição notifica de forma assíncrona; atualiza já para poder selecionar.
        RefreshList();
        if (_visible.All(t => t.Id != template.Id))
        {
            // Fora do filtro atual (uma tag, por exemplo): mostra todos.
            SetScope(Scope.All);
            this.FindControl<TextBox>("FilterBox")!.Text = string.Empty;
        }
        _selected = _store.Templates.FirstOrDefault(t => t.Id == template.Id);
        RefreshList();
        LoadEditor();
        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("EditTitle")!.Focus());
    }

    private async Task ConfirmDelete(Template template)
    {
        if (await MessageDialog.Confirm($"Apagar \"{template.Title}\"?", "Essa ação não pode ser desfeita.",
                                        "Apagar", "Cancelar") != 0) return;

        _store.Delete(template.Id);
        if (_selected?.Id == template.Id) _selected = null;
        RefreshList();
        LoadEditor();
    }
}
