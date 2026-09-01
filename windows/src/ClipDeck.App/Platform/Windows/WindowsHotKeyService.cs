using System.Runtime.InteropServices;

namespace ClipDeck.App.Platform.Windows;

/// <summary>
/// Atalhos globais no Windows, via RegisterHotKey.
///
/// A API exige uma janela que receba WM_HOTKEY, e o app não tem janela principal.
/// A solução é uma janela "message-only" (HWND_MESSAGE) numa thread própria com
/// seu próprio laço de mensagens — invisível, sem barra de tarefas, e sem
/// depender do ciclo de vida de nenhuma janela da interface.
/// </summary>
public sealed class WindowsHotKeyService : IHotKeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_CLOSE = 0x0010;

    private readonly Dictionary<int, Action> _handlers = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private IntPtr _hwnd;
    private int _nextId = 1;
    private volatile bool _disposed;

    public WindowsHotKeyService()
    {
        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "ClipDeck HotKeys",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        // Espera a janela existir: registrar antes disso falharia em silêncio.
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public bool Register(KeyCombo combo, Action handler)
    {
        if (_hwnd == IntPtr.Zero) return false;

        var id = _nextId++;
        _handlers[id] = handler;

        // MOD_NOREPEAT evita a ação disparar repetidamente enquanto a tecla fica
        // pressionada — sem ele, segurar Alt+Espaço abriria a paleta em looping.
        var modifiers = (uint)combo.Modifiers | 0x4000u;

        var ok = false;
        Invoke(() => ok = RegisterHotKey(_hwnd, id, modifiers, combo.KeyCode));

        // Falha típica: outro app já registrou a mesma combinação.
        if (!ok) _handlers.Remove(id);
        return ok;
    }

    public void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;

        foreach (var id in _handlers.Keys.ToList())
        {
            Invoke(() => UnregisterHotKey(_hwnd, id));
        }
        _handlers.Clear();
    }

    /// <summary>
    /// Executa na thread dona da janela. RegisterHotKey associa o atalho à thread
    /// que o registrou, então chamá-lo de outra thread não funcionaria.
    /// </summary>
    private void Invoke(Action action)
    {
        if (Thread.CurrentThread == _thread) { action(); return; }

        using var done = new ManualResetEventSlim(false);
        _pending.Enqueue(() => { action(); done.Set(); });
        PostMessage(_hwnd, WM_APP_RUN, IntPtr.Zero, IntPtr.Zero);
        done.Wait(TimeSpan.FromSeconds(2));
    }

    private const int WM_APP_RUN = 0x8000 + 1;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pending = new();

    private void MessageLoop()
    {
        var className = "ClipDeckHotKeyWindow";
        var wndProc = new WndProcDelegate(WndProc);
        _wndProcKeepAlive = wndProc;

        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            lpszClassName = className,
            hInstance = GetModuleHandle(null),
        };
        RegisterClass(ref wc);

        // HWND_MESSAGE cria uma janela que só recebe mensagens: nunca é desenhada
        // nem aparece na barra de tarefas ou no Alt+Tab.
        _hwnd = CreateWindowEx(0, className, className, 0, 0, 0, 0, 0,
                               new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ready.Set();

        while (!_disposed && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private WndProcDelegate? _wndProcKeepAlive;

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY:
                if (_handlers.TryGetValue(wParam.ToInt32(), out var handler))
                {
                    // A ação toca a interface, então precisa voltar para a thread
                    // de UI — estamos na thread do laço de mensagens.
                    Avalonia.Threading.Dispatcher.UIThread.Post(handler);
                }
                return IntPtr.Zero;

            case WM_APP_RUN:
                while (_pending.TryDequeue(out var action)) action();
                return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        if (_hwnd != IntPtr.Zero) PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _ready.Dispose();
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
