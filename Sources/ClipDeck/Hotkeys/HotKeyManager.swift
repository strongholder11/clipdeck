import AppKit
import Carbon.HIToolbox

/// Atalhos globais, funcionando com qualquer app em foco.
///
/// Usa a API Carbon `RegisterEventHotKey` em vez de um monitor global de eventos
/// do Cocoa por um motivo prático: o monitor do Cocoa exige permissão de
/// Acessibilidade, e o Carbon não. Assim a paleta abre logo na primeira execução,
/// sem depender de nenhuma permissão — só o colar automático precisa dela.
final class HotKeyManager {
    static let shared = HotKeyManager()

    private var callbacks: [UInt32: () -> Void] = [:]
    private var references: [UInt32: EventHotKeyRef] = [:]
    private var nextID: UInt32 = 1
    private var handlerInstalled = false

    private init() {}

    @discardableResult
    func register(_ combo: KeyCombo, handler: @escaping () -> Void) -> UInt32? {
        installHandlerIfNeeded()

        let id = nextID
        nextID += 1

        let hotKeyID = EventHotKeyID(signature: OSType(0x43_44_4B_31), id: id) // 'CDK1'
        var reference: EventHotKeyRef?

        let status = RegisterEventHotKey(
            combo.keyCode,
            combo.carbonModifiers,
            hotKeyID,
            GetEventDispatcherTarget(),
            0,
            &reference
        )

        // Falha típica: outro app já registrou a mesma combinação. Devolvemos nil
        // para a UI poder avisar em vez de o atalho simplesmente não funcionar.
        guard status == noErr, let reference else { return nil }

        references[id] = reference
        callbacks[id] = handler
        return id
    }

    func unregister(_ id: UInt32) {
        if let reference = references[id] { UnregisterEventHotKey(reference) }
        references[id] = nil
        callbacks[id] = nil
    }

    func unregisterAll() {
        references.keys.forEach(unregister)
    }

    fileprivate func handle(id: UInt32) {
        callbacks[id]?()
    }

    private func installHandlerIfNeeded() {
        guard !handlerInstalled else { return }
        var spec = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )
        InstallEventHandler(GetEventDispatcherTarget(), hotKeyEventHandler, 1, &spec, nil, nil)
        handlerInstalled = true
    }
}

/// Callback do Carbon. Precisa ser função de topo: a API espera um ponteiro de
/// função C, e closures que capturam contexto não podem ser convertidas.
private func hotKeyEventHandler(
    _ callRef: EventHandlerCallRef?,
    _ event: EventRef?,
    _ userData: UnsafeMutableRawPointer?
) -> OSStatus {
    var hotKeyID = EventHotKeyID()
    let status = GetEventParameter(
        event,
        EventParamName(kEventParamDirectObject),
        EventParamType(typeEventHotKeyID),
        nil,
        MemoryLayout<EventHotKeyID>.size,
        nil,
        &hotKeyID
    )
    guard status == noErr else { return status }

    DispatchQueue.main.async {
        HotKeyManager.shared.handle(id: hotKeyID.id)
    }
    return noErr
}
