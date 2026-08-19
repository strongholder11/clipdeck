import ClipDeckKit
import SwiftUI

/// Formulário para transformar um texto copiado em template.
struct CaptureView: View {
    let body_: String
    let folders: [Folder]
    let suggestedTags: [String]

    @State private var title: String = ""
    @State private var folderID: UUID?
    @State private var tagsText: String = ""
    @FocusState private var titleFocused: Bool

    var onSave: (String, UUID?, [String]) -> Void
    var onCancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()

            VStack(alignment: .leading, spacing: 14) {
                field("Título") {
                    TextField("Como você vai procurar por ele", text: $title)
                        .textFieldStyle(.roundedBorder)
                        .focused($titleFocused)
                        .onSubmit(save)
                }

                field("Pasta") {
                    Picker("", selection: $folderID) {
                        Text("Sem pasta").tag(UUID?.none)
                        ForEach(folders) { folder in
                            Text(folder.name).tag(UUID?.some(folder.id))
                        }
                    }
                    .labelsHidden()
                    .pickerStyle(.menu)
                }

                field("Tags") {
                    VStack(alignment: .leading, spacing: 6) {
                        TextField("separadas por espaço", text: $tagsText)
                            .textFieldStyle(.roundedBorder)

                        if !suggestedTags.isEmpty {
                            HStack(spacing: 5) {
                                ForEach(suggestedTags.prefix(6), id: \.self) { tag in
                                    Button("#\(tag)") { append(tag: tag) }
                                        .buttonStyle(.plain)
                                        .font(.caption)
                                        .padding(.horizontal, 6)
                                        .padding(.vertical, 2)
                                        .background(
                                            Capsule().fill(Color.secondary.opacity(0.15))
                                        )
                                }
                            }
                        }
                    }
                }

                field("Conteúdo") {
                    ScrollView {
                        Text(body_)
                            .font(.system(size: 12, design: .monospaced))
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .textSelection(.enabled)
                            .padding(8)
                    }
                    .frame(height: 120)
                    .background(Color.secondary.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                }
            }
            .padding(16)

            Divider()
            footer
        }
        .frame(width: 480)
        .background(.regularMaterial)
        .onAppear {
            titleFocused = true
            // Sugere um título a partir da primeira linha: quase sempre é o que
            // você quer, e economiza a digitação no caso comum.
            if title.isEmpty { title = suggestedTitle }
        }
    }

    private var header: some View {
        HStack(spacing: 8) {
            Image(systemName: "square.and.arrow.down.on.square")
                .foregroundStyle(.secondary)
            Text("Salvar como template")
                .font(.headline)
            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    private var footer: some View {
        HStack {
            Text("\(body_.count) caracteres")
                .font(.caption)
                .foregroundStyle(.tertiary)
            Spacer()
            Button("Cancelar", action: onCancel)
                .keyboardShortcut(.cancelAction)
            Button("Salvar", action: save)
                .keyboardShortcut(.defaultAction)
                .disabled(title.trimmingCharacters(in: .whitespaces).isEmpty)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    private func field<Content: View>(
        _ label: String,
        @ViewBuilder content: () -> Content
    ) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.caption.weight(.medium))
                .foregroundStyle(.secondary)
            content()
        }
    }

    private var suggestedTitle: String {
        let firstLine = body_
            .split(separator: "\n", omittingEmptySubsequences: true)
            .first
            .map { $0.trimmingCharacters(in: .whitespaces) } ?? ""
        return String(firstLine.prefix(60))
    }

    private func append(tag: String) {
        var current = tagsText.split(separator: " ").map(String.init)
        guard !current.contains(tag) else { return }
        current.append(tag)
        tagsText = current.joined(separator: " ")
    }

    private func save() {
        let trimmed = title.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }
        let tags = tagsText
            .split(whereSeparator: { $0 == " " || $0 == "," })
            .map { $0.trimmingCharacters(in: CharacterSet(charactersIn: "#")) }
            .filter { !$0.isEmpty }
        onSave(trimmed, folderID, tags)
    }
}
