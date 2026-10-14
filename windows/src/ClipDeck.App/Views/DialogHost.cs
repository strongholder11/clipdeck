using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ClipDeck.App.Views;

/// <summary>
/// Janela invisível que serve de dona para as caixas de abrir e salvar arquivo.
///
/// O seletor de arquivos do Avalonia pertence a uma janela, e o ClipDeck vive na
/// bandeja sem nenhuma aberta. Esta existe só durante a escolha do arquivo.
/// </summary>
public static class DialogHost
{
    public static async Task<T> Run<T>(Func<IStorageProvider, Task<T>> action)
    {
        var host = new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
        };

        host.Show();
        host.Activate();
        try
        {
            return await action(host.StorageProvider);
        }
        finally
        {
            host.Close();
        }
    }

    public static readonly FilePickerFileType Json = new("Templates do ClipDeck (JSON)")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" },
    };
}
