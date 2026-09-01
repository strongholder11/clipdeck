using System.Runtime.InteropServices;

namespace ClipDeck.App.Platform;

/// <summary>
/// Área de transferência.
///
/// No Windows usa as APIs nativas em vez do clipboard do Avalonia, que é
/// assíncrono: a colagem precisa do texto já gravado antes de sintetizar o
/// Ctrl+V, e um await no meio disso abre espaço para o foco mudar. Fora do
/// Windows guarda em memória, o bastante para desenvolver e testar a interface.
/// </summary>
public static class ClipboardHelper
{
    private static string? _fallback;

    public static string? ReadText()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return _fallback;

        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        if (!OpenClipboard(IntPtr.Zero)) return null;

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;

            try { return Marshal.PtrToStringUni(pointer); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    public static void WriteText(string text)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { _fallback = text; return; }

        if (!OpenClipboard(IntPtr.Zero)) return;

        try
        {
            EmptyClipboard();

            var bytes = (text.Length + 1) * 2;
            var handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (handle == IntPtr.Zero) return;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) { GlobalFree(handle); return; }

            try { Marshal.Copy(System.Text.Encoding.Unicode.GetBytes(text + '\0'), 0, pointer, bytes); }
            finally { GlobalUnlock(handle); }

            // A partir daqui a memória pertence ao sistema; liberar seria erro.
            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero) GlobalFree(handle);
        }
        finally { CloseClipboard(); }
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
}
