using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;
using ClipDeck.Core.Search;

namespace ClipDeck.App.Views;

/// <summary>Transforma um texto copiado em template.</summary>
public partial class CaptureWindow : Window
{
    private readonly TemplateStore _store;
    private readonly string _body;
    private readonly List<Folder> _folders;

    /// <param name="startWithNewFolder">
    /// Abre já com a criação de pasta visível. Serve para inspecionar esse
    /// estado sem depender de abrir o seletor à mão.
    /// </param>
    public CaptureWindow(TemplateStore store, string body, bool startWithNewFolder = false)
    {
        _store = store;
        _body = body;
        _folders = store.Folders.ToList();

        InitializeComponent();

        this.FindControl<TextBlock>("BodyPreview")!.Text = body;
        this.FindControl<TextBlock>("CountText")!.Text = $"{body.Length} caracteres";

        RefreshFolderBox(selectIndex: 0);

        var folderBox = this.FindControl<ComboBox>("FolderBox")!;
        folderBox.SelectionChanged += (_, _) =>
        {
            // A última opção é "Nova pasta…": revela o campo em vez de selecionar.
            var isNew = folderBox.SelectedIndex == _folders.Count + 1;
            this.FindControl<Grid>("NewFolderRow")!.IsVisible = isNew;
            if (isNew) Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("NewFolderBox")!.Focus());
        };

        this.FindControl<Button>("CreateFolderButton")!.Click += (_, _) => CreateFolder();
        this.FindControl<TextBox>("NewFolderBox")!.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) { CreateFolder(); e.Handled = true; }
        };
        this.FindControl<Button>("CancelFolderButton")!.Click += (_, _) =>
        {
            this.FindControl<Grid>("NewFolderRow")!.IsVisible = false;
            this.FindControl<TextBox>("NewFolderBox")!.Text = string.Empty;
            this.FindControl<ComboBox>("FolderBox")!.SelectedIndex = 0;
        };

        // Sugere o título a partir da primeira linha: quase sempre é o que se
        // quer, e economiza a digitação no caso comum.
        var titleBox = this.FindControl<TextBox>("TitleBox")!;
        titleBox.Text = SuggestedTitle;

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("SaveButton")!.Click += (_, _) => Save();

        if (startWithNewFolder)
        {
            folderBox.SelectedIndex = _folders.Count + 1;
        }

        Opened += (_, _) => Dispatcher.UIThread.Post(() => titleBox.Focus());
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Repopula o seletor, sempre com "Nova pasta…" no fim.</summary>
    private void RefreshFolderBox(int selectIndex)
    {
        var box = this.FindControl<ComboBox>("FolderBox")!;
        box.ItemsSource = new[] { "Sem pasta" }
            .Concat(_folders.Select(f => f.Name))
            .Append("Nova pasta…")
            .ToList();
        box.SelectedIndex = selectIndex;
    }

    /// <summary>Cria a pasta e a deixa selecionada, sem sair do formulário.</summary>
    private void CreateFolder()
    {
        var name = (this.FindControl<TextBox>("NewFolderBox")!.Text ?? string.Empty).Trim();
        if (name.Length == 0) return;

        // Reaproveita uma pasta de mesmo nome em vez de duplicar: digitar
        // "Vendas" quando "Vendas" já existe quase nunca quer dizer "quero duas".
        var existing = _folders.FirstOrDefault(f =>
            TextNormalizer.Fold(f.Name) == TextNormalizer.Fold(name));

        if (existing is null)
        {
            var order = _folders.Count == 0 ? 0 : _folders.Max(f => f.Order) + 1;
            existing = new Folder { Name = name, Order = order };
            _store.AddFolder(existing);
            _folders.Add(existing);
        }

        RefreshFolderBox(selectIndex: _folders.IndexOf(existing) + 1);
        this.FindControl<Grid>("NewFolderRow")!.IsVisible = false;
        this.FindControl<TextBox>("NewFolderBox")!.Text = string.Empty;
    }

    private string SuggestedTitle
    {
        get
        {
            foreach (var line in _body.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0) return trimmed[..Math.Min(60, trimmed.Length)];
            }
            return string.Empty;
        }
    }

    private void Save()
    {
        var title = (this.FindControl<TextBox>("TitleBox")!.Text ?? string.Empty).Trim();
        if (title.Length == 0) return;

        var folderIndex = this.FindControl<ComboBox>("FolderBox")!.SelectedIndex;
        Guid? folderId = folderIndex > 0 ? _folders[folderIndex - 1].Id : null;

        var tags = (this.FindControl<TextBox>("TagsBox")!.Text ?? string.Empty)
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimStart('#'))
            .Where(t => t.Length > 0)
            .ToList();

        _store.Add(new Template { Title = title, Body = _body, FolderId = folderId, Tags = tags });
        Close();
    }
}
