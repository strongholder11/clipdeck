using ClipDeck.Core.Models;

namespace ClipDeck.App.Platform;

/// <summary>
/// Implementações vazias para rodar fora do Windows.
///
/// Existem para o app subir no macOS durante o desenvolvimento: a interface toda
/// pode ser inspecionada e testada, e só o atalho global e a colagem ficam
/// inertes. Sem isso, verificar a UI exigiria uma máquina Windows a cada ajuste.
/// </summary>
public sealed class StubHotKeyService : IHotKeyService
{
    private readonly List<(KeyCombo Combo, Action Handler)> _registered = new();

    public bool Register(KeyCombo combo, Action handler)
    {
        _registered.Add((combo, handler));
        Console.WriteLine($"[stub] atalho registrado: {combo.DisplayString}");
        return true;
    }

    /// <summary>Dispara um atalho manualmente — usado para testar sem Windows.</summary>
    public void Trigger(int index)
    {
        if (index >= 0 && index < _registered.Count) _registered[index].Handler();
    }

    public void UnregisterAll() => _registered.Clear();
    public void Dispose() => UnregisterAll();
}

public sealed class StubPasteService : IPasteService
{
    public bool Paste(string text)
    {
        Console.WriteLine($"[stub] colaria {text.Length} caracteres");
        return false;
    }

    public void RememberForegroundWindow() { }
}
