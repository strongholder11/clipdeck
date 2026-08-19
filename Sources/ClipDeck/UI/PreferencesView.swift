import SwiftUI

struct PreferencesView: View {
    @ObservedObject var preferences: Preferences
    let accessibilityGranted: Bool
    var onOpenAccessibilitySettings: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            section("Atalhos") {
                row("Abrir a paleta") {
                    ShortcutRecorder(combo: $preferences.paletteShortcut)
                        .frame(width: 150, height: 24)
                }
                row("Salvar a cópia atual") {
                    ShortcutRecorder(combo: $preferences.captureShortcut)
                        .frame(width: 150, height: 24)
                }
                Text("Clique no campo e pressione a combinação desejada. Esc cancela.")
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            }

            Divider()

            section("Comportamento") {
                Toggle("Colar automaticamente ao pressionar ⏎", isOn: $preferences.pasteAutomatically)
                Text(preferences.pasteAutomatically
                     ? "⇧⏎ continua apenas copiando."
                     : "⏎ apenas copia; você cola com ⌘V.")
                    .font(.caption)
                    .foregroundStyle(.tertiary)

                if !accessibilityGranted {
                    HStack(spacing: 6) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundStyle(.orange)
                        Text("A colagem automática precisa da permissão de Acessibilidade.")
                            .font(.caption)
                        Button("Abrir Ajustes", action: onOpenAccessibilitySettings)
                            .font(.caption)
                    }
                    .padding(8)
                    .background(Color.orange.opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
                }
            }

            Divider()

            section("Sistema") {
                Toggle("Abrir o ClipDeck ao ligar o Mac", isOn: $preferences.launchAtLogin)
                if let error = preferences.launchAtLoginError {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.orange)
                }
            }

            Spacer()
        }
        .padding(20)
        .frame(width: 460, height: 420)
    }

    private func section<Content: View>(
        _ title: String,
        @ViewBuilder content: () -> Content
    ) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title)
                .font(.headline)
            content()
        }
    }

    private func row<Content: View>(
        _ label: String,
        @ViewBuilder content: () -> Content
    ) -> some View {
        HStack {
            Text(label)
            Spacer()
            content()
        }
    }
}
