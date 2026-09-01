using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipDeck.App.Services;
using ClipDeck.Core.Models;

namespace ClipDeck.App.Views;

/// <summary>Transforma um texto copiado em template.</summary>
public partial class CaptureWindow : Window
{
    private readonly TemplateStore _store;
    private readonly string _body;
    private readonly List<Folder> _folders;

    public CaptureWindow(TemplateStore store, string body)
    {
        _store = store;
        _body = body;
        _folders = store.Folders.ToList();

        InitializeComponent();

        this.FindControl<TextBlock>("BodyPreview")!.Text = body;
        this.FindControl<TextBlock>("CountText")!.Text = $"{body.Length} caracteres";

        var folderBox = this.FindControl<ComboBox>("FolderBox")!;
        folderBox.ItemsSource = new[] { "Sem pasta" }.Concat(_folders.Select(f => f.Name)).ToList();
        folderBox.SelectedIndex = 0;

        // Sugere o título a partir da primeira linha: quase sempre é o que se
        // quer, e economiza a digitação no caso comum.
        var titleBox = this.FindControl<TextBox>("TitleBox")!;
        titleBox.Text = SuggestedTitle;

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("SaveButton")!.Click += (_, _) => Save();

        Opened += (_, _) => Dispatcher.UIThread.Post(() => titleBox.Focus());
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

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
