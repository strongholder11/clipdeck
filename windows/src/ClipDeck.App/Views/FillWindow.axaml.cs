using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipDeck.Core.Models;
using ClipDeck.Core.Variables;

namespace ClipDeck.App.Views;

/// <summary>Formulário para preencher as variáveis antes de colar.</summary>
public partial class FillWindow : Window
{
    private readonly Template _template;
    private readonly List<TemplateVariable> _variables;
    private readonly string? _clipboard;
    private readonly Dictionary<string, string> _values = new();
    private readonly List<TextBox> _boxes = new();

    public event Action<string>? Confirmed;

    public FillWindow(Template template, List<TemplateVariable> variables, string? clipboard)
    {
        _template = template;
        _variables = variables;
        _clipboard = clipboard;

        InitializeComponent();

        Title = template.Title;
        this.FindControl<TextBlock>("HeaderText")!.Text = template.Title;

        BuildFields();
        UpdatePreview();

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) => Confirm();

        Opened += (_, _) => Dispatcher.UIThread.Post(() => _boxes.FirstOrDefault()?.Focus());
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void BuildFields()
    {
        var host = this.FindControl<ItemsControl>("FieldsHost")!;
        var items = new List<Control>();

        foreach (var variable in _variables)
        {
            _values[variable.Name] = string.Empty;

            var box = new TextBox { PlaceholderText = variable.Label };
            box.TextChanged += (_, _) =>
            {
                _values[variable.Name] = box.Text ?? string.Empty;
                UpdatePreview();
            };
            _boxes.Add(box);

            items.Add(new StackPanel
            {
                Spacing = 3,
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock { Text = variable.Label, FontSize = 11, Opacity = 0.6 },
                    box,
                },
            });
        }

        host.ItemsSource = items;
    }

    /// <summary>
    /// Prévia ao vivo. Sem ela, um campo errado só aparece depois de colado na
    /// conversa do cliente — que é onde o erro custa caro.
    /// </summary>
    private void UpdatePreview()
    {
        this.FindControl<TextBlock>("PreviewText")!.Text = Rendered;

        var remaining = _values.Count(v => v.Value.Length == 0);
        this.FindControl<TextBlock>("RemainingText")!.Text =
            remaining == 0 ? string.Empty
                           : $"{remaining} campo{(remaining == 1 ? "" : "s")} em branco";
    }

    private string Rendered =>
        TemplateRenderer.Render(_template.Body, _values, _clipboard);

    private void Confirm()
    {
        var text = Rendered;
        Close();
        Confirmed?.Invoke(text);
    }
}
