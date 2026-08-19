import ClipDeckKit
import SwiftUI

/// Painel de edição de um template.
struct TemplateEditor: View {
    let template: Template
    let folders: [Folder]
    var onChange: (Template) -> Void
    var onDelete: () -> Void

    @State private var title: String
    @State private var body_: String
    @State private var folderID: UUID?
    @State private var tagsText: String
    @State private var confirmingDelete = false

    init(
        template: Template,
        folders: [Folder],
        onChange: @escaping (Template) -> Void,
        onDelete: @escaping () -> Void
    ) {
        self.template = template
        self.folders = folders
        self.onChange = onChange
        self.onDelete = onDelete
        _title = State(initialValue: template.title)
        _body_ = State(initialValue: template.body)
        _folderID = State(initialValue: template.folderID)
        _tagsText = State(initialValue: template.tags.joined(separator: " "))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    labeled("Título") {
                        TextField("Título", text: $title)
                            .textFieldStyle(.roundedBorder)
                            .font(.system(size: 14, weight: .medium))
                            .onChange(of: title) { _ in commit() }
                    }

                    HStack(alignment: .top, spacing: 12) {
                        labeled("Pasta") {
                            Picker("", selection: $folderID) {
                                Text("Sem pasta").tag(UUID?.none)
                                ForEach(folders) { folder in
                                    Text(folder.name).tag(UUID?.some(folder.id))
                                }
                            }
                            .labelsHidden()
                            .onChange(of: folderID) { _ in commit() }
                        }

                        labeled("Tags") {
                            TextField("separadas por espaço", text: $tagsText)
                                .textFieldStyle(.roundedBorder)
                                .onChange(of: tagsText) { _ in commit() }
                        }
                    }

                    labeled("Conteúdo") {
                        TextEditor(text: $body_)
                            .font(.system(size: 13))
                            .frame(minHeight: 240)
                            .scrollContentBackground(.hidden)
                            .padding(6)
                            .background(
                                RoundedRectangle(cornerRadius: 6)
                                    .fill(Color.secondary.opacity(0.08))
                            )
                            .onChange(of: body_) { _ in commit() }
                    }

                    variablesHint
                }
                .padding(16)
            }

            Divider()
            footer
        }
    }

    /// Mostra quais variáveis o texto contém, para você conferir se escreveu o
    /// nome certo — `{{nome}}` e `{{Nome}}` são variáveis diferentes, e sem essa
    /// lista o erro só aparece na hora de colar.
    @ViewBuilder
    private var variablesHint: some View {
        let variables = TemplateRenderer.variables(in: body_)
        if !variables.isEmpty {
            VStack(alignment: .leading, spacing: 5) {
                Text("Variáveis detectadas")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(.secondary)

                HStack(spacing: 5) {
                    ForEach(variables) { variable in
                        HStack(spacing: 3) {
                            Text(variable.name)
                            if variable.isBuiltIn {
                                Image(systemName: "wand.and.stars")
                                    .font(.system(size: 8))
                            }
                        }
                        .font(.caption)
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(
                            Capsule().fill(
                                variable.isBuiltIn
                                    ? Color.accentColor.opacity(0.18)
                                    : Color.secondary.opacity(0.15)
                            )
                        )
                    }
                }

                if variables.contains(where: \.isBuiltIn) {
                    Text("As marcadas com ✨ são preenchidas automaticamente.")
                        .font(.caption2)
                        .foregroundStyle(.tertiary)
                }
            }
        }
    }

    private var footer: some View {
        HStack {
            Text("usado \(template.useCount)×")
                .font(.caption)
                .foregroundStyle(.tertiary)
            Spacer()
            Button(role: .destructive) {
                confirmingDelete = true
            } label: {
                Label("Apagar", systemImage: "trash")
            }
            .confirmationDialog(
                "Apagar \"\(template.title)\"?",
                isPresented: $confirmingDelete,
                titleVisibility: .visible
            ) {
                Button("Apagar", role: .destructive, action: onDelete)
                Button("Cancelar", role: .cancel) {}
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
    }

    private func labeled<Content: View>(
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

    private func commit() {
        var updated = template
        updated.title = title
        updated.body = body_
        updated.folderID = folderID
        updated.tags = tagsText
            .split(whereSeparator: { $0 == " " || $0 == "," })
            .map { $0.trimmingCharacters(in: CharacterSet(charactersIn: "#")) }
            .filter { !$0.isEmpty }
        onChange(updated)
    }
}
