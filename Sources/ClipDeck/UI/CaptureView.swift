import ClipDeckKit
import SwiftUI

/// O que está escolhido no seletor de pasta.
///
/// Um `UUID?` não comporta a terceira opção: "criar uma pasta agora". Um tipo
/// próprio deixa as três alternativas explícitas em vez de reservar algum UUID
/// mágico para significar "nova".
enum FolderChoice: Hashable {
    case none
    case existing(UUID)
    case new

    var folderID: UUID? {
        if case .existing(let id) = self { return id }
        return nil
    }
}

/// Formulário para transformar um texto copiado em template.
struct CaptureView: View {
    let body_: String
    let folders: [Folder]
    let suggestedTags: [String]

    @State private var title: String = ""
    /// Estado inicial do seletor. Existe para poder inspecionar o formulário
    /// já com a criação de pasta aberta, sem depender de abrir o menu à mão.
    var initialFolderChoice: FolderChoice = .none

    @State private var folderChoice: FolderChoice = .none
    @State private var newFolderName: String = ""
    @FocusState private var newFolderFocused: Bool
    @State private var tagsText: String = ""
    @FocusState private var titleFocused: Bool

    var onSave: (String, UUID?, [String]) -> Void

    /// Cria a pasta e devolve o id, para já deixá-la selecionada.
    var onCreateFolder: (String) -> UUID
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
                    VStack(alignment: .leading, spacing: 6) {
                        Picker("", selection: $folderChoice) {
                            Text("Sem pasta").tag(FolderChoice.none)
                            ForEach(folders) { folder in
                                Text(folder.name).tag(FolderChoice.existing(folder.id))
                            }
                            Divider()
                            Text("Nova pasta…").tag(FolderChoice.new)
                        }
                        .labelsHidden()
                        .pickerStyle(.menu)
                        .onChange(of: folderChoice) { choice in
                            // Foca o campo assim que "Nova pasta…" é escolhida:
                            // sem isso é preciso clicar nele antes de digitar.
                            if choice == .new { newFolderFocused = true }
                        }

                        if folderChoice == .new {
                            HStack(spacing: 6) {
                                TextField("Nome da pasta", text: $newFolderName)
                                    .textFieldStyle(.roundedBorder)
                                    .focused($newFolderFocused)
                                    .onSubmit(createFolder)

                                Button("Criar", action: createFolder)
                                    .disabled(trimmedFolderName.isEmpty)

                                Button {
                                    folderChoice = .none
                                    newFolderName = ""
                                } label: {
                                    Image(systemName: "xmark.circle.fill")
                                        .foregroundStyle(.secondary)
                                }
                                .buttonStyle(.plain)
                                .help("Cancelar")
                            }
                        }
                    }
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
            folderChoice = initialFolderChoice
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

    private var trimmedFolderName: String {
        newFolderName.trimmingCharacters(in: .whitespaces)
    }

    /// Cria a pasta e a deixa selecionada, sem tirar você do formulário.
    private func createFolder() {
        let name = trimmedFolderName
        guard !name.isEmpty else { return }

        // Reaproveita uma pasta de mesmo nome em vez de criar uma duplicata —
        // digitar "Vendas" quando "Vendas" já existe quase nunca quer dizer
        // "quero duas pastas chamadas Vendas".
        if let existing = folders.first(where: {
            TextNormalizer.fold($0.name) == TextNormalizer.fold(name)
        }) {
            folderChoice = .existing(existing.id)
        } else {
            folderChoice = .existing(onCreateFolder(name))
        }

        newFolderName = ""
        titleFocused = true
    }

    private func save() {
        let trimmed = title.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }
        let tags = tagsText
            .split(whereSeparator: { $0 == " " || $0 == "," })
            .map { $0.trimmingCharacters(in: CharacterSet(charactersIn: "#")) }
            .filter { !$0.isEmpty }
        onSave(trimmed, folderChoice.folderID, tags)
    }
}
