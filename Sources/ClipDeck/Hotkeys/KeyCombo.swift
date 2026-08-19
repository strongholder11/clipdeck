import AppKit
import Carbon.HIToolbox

/// Uma combinação de teclas global.
public struct KeyCombo: Codable, Equatable {
    public var keyCode: UInt32
    public var carbonModifiers: UInt32

    public init(keyCode: UInt32, carbonModifiers: UInt32) {
        self.keyCode = keyCode
        self.carbonModifiers = carbonModifiers
    }

    public static let openPalette = KeyCombo(
        keyCode: UInt32(kVK_Space),
        carbonModifiers: UInt32(optionKey)
    )

    public static let captureClipboard = KeyCombo(
        keyCode: UInt32(kVK_ANSI_C),
        carbonModifiers: UInt32(optionKey | cmdKey)
    )

    /// Converte modificadores do Cocoa (usados ao gravar um atalho) para Carbon
    /// (usados ao registrá-lo). São bitmasks diferentes para a mesma coisa.
    public static func carbonModifiers(from flags: NSEvent.ModifierFlags) -> UInt32 {
        var carbon: UInt32 = 0
        if flags.contains(.command) { carbon |= UInt32(cmdKey) }
        if flags.contains(.option) { carbon |= UInt32(optionKey) }
        if flags.contains(.control) { carbon |= UInt32(controlKey) }
        if flags.contains(.shift) { carbon |= UInt32(shiftKey) }
        return carbon
    }

    /// Representação legível, tipo "⌥Espaço", para mostrar nas Preferências.
    public var displayString: String {
        var parts = ""
        if carbonModifiers & UInt32(controlKey) != 0 { parts += "⌃" }
        if carbonModifiers & UInt32(optionKey) != 0 { parts += "⌥" }
        if carbonModifiers & UInt32(shiftKey) != 0 { parts += "⇧" }
        if carbonModifiers & UInt32(cmdKey) != 0 { parts += "⌘" }
        return parts + Self.keyName(for: keyCode)
    }

    static func keyName(for keyCode: UInt32) -> String {
        switch Int(keyCode) {
        case kVK_Space: return "Espaço"
        case kVK_Return: return "↩"
        case kVK_Escape: return "esc"
        case kVK_Tab: return "⇥"
        default:
            // Consulta o layout de teclado ativo, para o nome bater com o que
            // está impresso na tecla — ABNT2 e US não coincidem em tudo.
            guard let source = TISCopyCurrentKeyboardLayoutInputSource()?.takeRetainedValue(),
                  let pointer = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData)
            else { return "?" }

            let data = Unmanaged<CFData>.fromOpaque(pointer).takeUnretainedValue() as Data
            var deadKeyState: UInt32 = 0
            var length = 0
            var characters = [UniChar](repeating: 0, count: 4)

            let status = data.withUnsafeBytes { raw -> OSStatus in
                guard let layout = raw.baseAddress?.assumingMemoryBound(to: UCKeyboardLayout.self)
                else { return OSStatus(-1) }
                return UCKeyTranslate(
                    layout,
                    UInt16(keyCode),
                    UInt16(kUCKeyActionDisplay),
                    0,
                    UInt32(LMGetKbdType()),
                    OptionBits(kUCKeyTranslateNoDeadKeysBit),
                    &deadKeyState,
                    characters.count,
                    &length,
                    &characters
                )
            }

            guard status == noErr, length > 0 else { return "?" }
            return String(utf16CodeUnits: characters, count: length).uppercased()
        }
    }
}
