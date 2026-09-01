using System.Runtime.InteropServices;

namespace ClipDeck.App.Platform.Windows;

/// <summary>
/// Cola no programa que estava em foco antes da paleta abrir.
///
/// Não existe API para "inserir texto no programa X". O caminho real é: escrever
/// na área de transferência, devolver o foco e sintetizar Ctrl+V. No Windows isso
/// não exige permissão nenhuma — diferente do macOS, onde precisa de
/// Acessibilidade.
/// </summary>
public sealed class WindowsPasteService : IPasteService
{
    /// <summary>
    /// Espera o programa anterior reassumir o foco. Colar antes disso manda o
    /// Ctrl+V para a janela errada.
    /// </summary>
    private const int FocusDelayMs = 80;

    private IntPtr _previousWindow;

    public void RememberForegroundWindow() => _previousWindow = GetForegroundWindow();

    public bool Paste(string text)
    {
        if (_previousWindow == IntPtr.Zero) return false;

        // SetForegroundWindow é recusado quando o processo não tem "direito de
        // foco". AttachThreadInput empresta esse direito da thread que hoje é
        // dona do primeiro plano, que é a forma suportada de contornar isso.
        var target = GetWindowThreadProcessId(_previousWindow, out _);
        var current = GetCurrentThreadId();

        var attached = target != current && AttachThreadInput(current, target, true);
        try
        {
            SetForegroundWindow(_previousWindow);
        }
        finally
        {
            if (attached) AttachThreadInput(current, target, false);
        }

        Thread.Sleep(FocusDelayMs);
        SendCtrlV();
        return true;
    }

    private static void SendCtrlV()
    {
        const ushort VK_CONTROL = 0x11;
        const ushort VK_V = 0x56;

        var inputs = new INPUT[4];
        inputs[0] = KeyInput(VK_CONTROL, keyUp: false);
        inputs[1] = KeyInput(VK_V, keyUp: false);
        inputs[2] = KeyInput(VK_V, keyUp: true);
        inputs[3] = KeyInput(VK_CONTROL, keyUp: true);

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(ushort key, bool keyUp) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = key,
                dwFlags = keyUp ? 0x0002u : 0u, // KEYEVENTF_KEYUP
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
