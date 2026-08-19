import Combine
import Foundation
import ServiceManagement

/// Preferências do usuário, persistidas no UserDefaults.
@MainActor
final class Preferences: ObservableObject {
    private enum Key {
        static let paletteShortcut = "paletteShortcut"
        static let captureShortcut = "captureShortcut"
        static let pasteAutomatically = "pasteAutomatically"
    }

    @Published var paletteShortcut: KeyCombo {
        didSet { save(paletteShortcut, forKey: Key.paletteShortcut) }
    }

    @Published var captureShortcut: KeyCombo {
        didSet { save(captureShortcut, forKey: Key.captureShortcut) }
    }

    /// Quando falso, ⏎ na paleta copia em vez de colar.
    @Published var pasteAutomatically: Bool {
        didSet { defaults.set(pasteAutomatically, forKey: Key.pasteAutomatically) }
    }

    /// Não é persistido aqui: quem guarda esse estado é o próprio sistema, via
    /// SMAppService. Ler dele evita a preferência dizer "ligado" enquanto o
    /// macOS acha que está desligado.
    @Published var launchAtLogin: Bool {
        didSet {
            guard launchAtLogin != (SMAppService.mainApp.status == .enabled) else { return }
            applyLaunchAtLogin()
        }
    }

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults

        paletteShortcut = Self.load(Key.paletteShortcut, from: defaults) ?? .openPalette
        captureShortcut = Self.load(Key.captureShortcut, from: defaults) ?? .captureClipboard

        pasteAutomatically = defaults.object(forKey: Key.pasteAutomatically) as? Bool ?? true
        launchAtLogin = SMAppService.mainApp.status == .enabled
    }

    /// Erros aqui são comuns e não fatais: o app precisa estar em /Applications
    /// ou ~/Applications, e uma assinatura ad-hoc pode ser recusada. Reverte o
    /// estado para o switch não mentir sobre o que aconteceu.
    private func applyLaunchAtLogin() {
        do {
            if launchAtLogin {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
        } catch {
            launchAtLoginError = error.localizedDescription
            launchAtLogin = SMAppService.mainApp.status == .enabled
        }
    }

    @Published var launchAtLoginError: String?

    private func save(_ combo: KeyCombo, forKey key: String) {
        guard let data = try? JSONEncoder().encode(combo) else { return }
        defaults.set(data, forKey: key)
    }

    private static func load(_ key: String, from defaults: UserDefaults) -> KeyCombo? {
        guard let data = defaults.data(forKey: key) else { return nil }
        return try? JSONDecoder().decode(KeyCombo.self, from: data)
    }
}
