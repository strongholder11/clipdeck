using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClipDeck.App;
using ClipDeck.App.Services;
using ClipDeck.App.ViewModels;
using ClipDeck.App.Views;
using ClipDeck.Core.Models;
using ClipDeck.Core.Variables;

// Rasteriza as telas do ClipDeck em PNG, sem precisar de um monitor.
//
// O backend macOS do Avalonia não sobe neste ambiente (falha ao criar o
// RenderTimer), então rodar o app de verdade aqui não é possível. O modo headless
// com Skia desenha exatamente o mesmo layout, com os mesmos dados, e grava o
// resultado — o que permite conferir a interface antes de entregá-la a alguém no
// Windows.

var outputDir = args.Length > 0 ? args[0] : "snapshots";
Directory.CreateDirectory(outputDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// Biblioteca temporária, para os snapshots não dependerem de dados reais.
var tempDir = Path.Combine(Path.GetTempPath(), "clipdeck-snapshots-" + Guid.NewGuid());
var store = new TemplateStore(new LibraryStorage(tempDir));

Capture("01-paleta-vazia", () =>
{
    var window = new PaletteWindow(new PaletteViewModel(store));
    window.Show();
    return window;
});

Capture("02-paleta-busca", () =>
{
    var model = new PaletteViewModel(store);
    var window = new PaletteWindow(model);
    window.Show();
    // Mesma consulta que expôs o bug de busca na versão macOS.
    model.Query = "reuniao";
    return window;
});

Capture("03-preenchimento", () =>
{
    var template = store.Templates.First(t => t.Title == "Confirmar reunião");
    var window = new FillWindow(
        template, TemplateRenderer.FillableVariables(template.Body), "texto copiado");
    window.Show();
    return window;
});

Capture("04-captura", () =>
{
    var window = new CaptureWindow(store,
        "Oi {{nome}}, tudo certo?\n\nSegue a proposta que combinamos na reunião de hoje.");
    window.Show();
    return window;
});

Capture("05-biblioteca", () =>
{
    var window = new LibraryWindow(store);
    window.Show();
    return window;
});

try { Directory.Delete(tempDir, recursive: true); } catch { }
Console.WriteLine($"\nsnapshots em: {Path.GetFullPath(outputDir)}");
return 0;

void Capture(string name, Func<Avalonia.Controls.Window> build)
{
    var window = build();

    // Deixa o layout assentar: bindings e medições acontecem em jobs enfileirados.
    for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs();

    var frame = window.CaptureRenderedFrame();
    if (frame is null)
    {
        Console.WriteLine($"  ✗ {name}: nada renderizado");
        return;
    }

    var path = Path.Combine(outputDir, name + ".png");
    frame.Save(path);
    Console.WriteLine($"  ✓ {name}  ({frame.PixelSize.Width}x{frame.PixelSize.Height})");
    window.Close();
}
