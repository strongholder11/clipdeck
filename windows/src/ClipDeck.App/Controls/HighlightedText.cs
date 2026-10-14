using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ClipDeck.Core.Search;

namespace ClipDeck.App.Controls;

/// <summary>
/// Título com as letras que casaram com a busca em negrito sublinhado.
/// </summary>
public sealed class HighlightedText : TextBlock
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<HighlightedText, string?>(nameof(Source));

    public static readonly StyledProperty<IReadOnlyList<int>?> HighlightsProperty =
        AvaloniaProperty.Register<HighlightedText, IReadOnlyList<int>?>(nameof(Highlights));

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IReadOnlyList<int>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == HighlightsProperty) Rebuild();
    }

    /// <summary>
    /// Os índices vêm do texto normalizado (sem acento). Normalmente o folding
    /// preserva o tamanho, mas não é garantido em todo idioma — se divergir,
    /// mostra o título sem destaque em vez de destacar a letra errada.
    /// </summary>
    private void Rebuild()
    {
        var title = Source ?? string.Empty;
        var positions = Highlights is { Count: > 0 } h ? new HashSet<int>(h) : null;

        Inlines?.Clear();
        if (positions is null || TextNormalizer.Fold(title).Length != title.Length)
        {
            Text = title;
            return;
        }

        Text = null;
        var inlines = Inlines ??= new InlineCollection();
        for (var i = 0; i < title.Length;)
        {
            var bold = positions.Contains(i);
            var start = i;
            while (i < title.Length && positions.Contains(i) == bold) i++;

            var run = new Run(title[start..i]);
            if (bold)
            {
                run.FontWeight = FontWeight.Bold;
                run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
            }
            inlines.Add(run);
        }
    }
}
