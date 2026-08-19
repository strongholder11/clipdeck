import ClipDeckKit
import SwiftUI

/// O que está selecionado na barra lateral.
enum LibrarySelection: Hashable {
    case all
    case unfiled
    case folder(UUID)
    case tag(String)
}

/// Janela de gerenciamento: pastas à esquerda, templates no meio, editor à direita.
struct LibraryView: View {
    @ObservedObject var store: Store

    @State private var selection: LibrarySelection = .all
    @State private var selectedTemplateID: UUID?
    @State private var search = ""
    @State private var newFolderName = ""
    @State private var showingNewFolder = false

    var body: some View {
        NavigationSplitView {
            sidebar
                .navigationSplitViewColumnWidth(min: 180, ideal: 210, max: 280)
        } content: {
            templateList
                .navigationSplitViewColumnWidth(min: 240, ideal: 300, max: 420)
        } detail: {
            editor
        }
        .frame(minWidth: 900, minHeight: 540)
        .onAppear {
            // Abre já mostrando algo: um painel de edição vazio na abertura
            // não ajuda ninguém, e o primeiro item é um ponto de partida tão
            // bom quanto qualquer outro.
            if selectedTemplateID == nil {
                selectedTemplateID = visibleTemplates.first?.id
            }
        }
    }

    // MARK: - Barra lateral

    private var sidebar: some View {
        List(selection: $selection) {
            Section {
                Label("Todos os templates", systemImage: "tray.full")
                    .tag(LibrarySelection.all)
                Label("Sem pasta", systemImage: "tray")
                    .tag(LibrarySelection.unfiled)
            }

            Section("Pastas") {
                ForEach(store.folders) { folder in
                    Label(folder.name, systemImage: folder.symbol)
                        .tag(LibrarySelection.folder(folder.id))
                        .contextMenu {
                            Button("Apagar pasta") { store.deleteFolder(id: folder.id) }
                        }
                }
            }

            if !store.allTags.isEmpty {
                Section("Tags") {
                    ForEach(store.allTags, id: \.self) { tag in
                        Label(tag, systemImage: "number")
                            .tag(LibrarySelection.tag(tag))
                    }
                }
            }
        }
        .listStyle(.sidebar)
        .safeAreaInset(edge: .bottom) {
            HStack {
                Button {
                    showingNewFolder = true
                } label: {
                    Label("Nova pasta", systemImage: "plus")
                        .font(.caption)
                }
                .buttonStyle(.plain)
                Spacer()
            }
            .padding(8)
        }
        .popover(isPresented: $showingNewFolder) {
            VStack(alignment: .leading, spacing: 8) {
                Text("Nome da pasta").font(.caption).foregroundStyle(.secondary)
                TextField("Ex: Renovação", text: $newFolderName)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 200)
                    .onSubmit(createFolder)
                HStack {
                    Spacer()
                    Button("Criar", action: createFolder)
                        .disabled(newFolderName.trimmingCharacters(in: .whitespaces).isEmpty)
                }
            }
            .padding(12)
        }
    }

    // MARK: - Lista de templates

    private var templateList: some View {
        List(selection: $selectedTemplateID) {
            ForEach(visibleTemplates) { template in
                VStack(alignment: .leading, spacing: 2) {
                    Text(template.title)
                        .font(.system(size: 13, weight: .medium))
                        .lineLimit(1)
                    Text(template.preview)
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
                .padding(.vertical, 2)
                .tag(template.id)
                .contextMenu {
                    Menu("Mover para") {
                        Button("Sem pasta") { store.move(templateID: template.id, toFolder: nil) }
                        ForEach(store.folders) { folder in
                            Button(folder.name) {
                                store.move(templateID: template.id, toFolder: folder.id)
                            }
                        }
                    }
                    Divider()
                    Button("Duplicar") { duplicate(template) }
                    Button("Apagar", role: .destructive) {
                        store.delete(templateID: template.id)
                    }
                }
            }
        }
        .searchable(text: $search, placement: .toolbar, prompt: "Filtrar")
        .safeAreaInset(edge: .bottom) {
            HStack {
                Button {
                    createTemplate()
                } label: {
                    Label("Novo template", systemImage: "plus")
                        .font(.caption)
                }
                .buttonStyle(.plain)
                Spacer()
                Text("\(visibleTemplates.count)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.tertiary)
            }
            .padding(8)
        }
    }

    // MARK: - Editor

    @ViewBuilder
    private var editor: some View {
        if let template = selectedTemplate {
            TemplateEditor(
                template: template,
                folders: store.folders,
                onChange: { store.update($0) },
                onDelete: {
                    store.delete(templateID: template.id)
                    selectedTemplateID = nil
                }
            )
            // Recria o editor ao trocar de template, para os campos não
            // carregarem o texto do anterior.
            .id(template.id)
        } else {
            VStack(spacing: 8) {
                Image(systemName: "doc.text")
                    .font(.system(size: 32))
                    .foregroundStyle(.tertiary)
                Text("Selecione um template")
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    // MARK: - Dados

    private var selectedTemplate: Template? {
        guard let selectedTemplateID else { return nil }
        return store.templates.first { $0.id == selectedTemplateID }
    }

    private var visibleTemplates: [Template] {
        let scoped: [Template]
        switch selection {
        case .all:
            scoped = store.templates
        case .unfiled:
            scoped = store.templates.filter { $0.folderID == nil }
        case .folder(let id):
            scoped = store.templates.filter { $0.folderID == id }
        case .tag(let tag):
            scoped = store.templates.filter { $0.tags.contains(tag) }
        }

        guard !search.isEmpty else {
            return scoped.sorted { $0.title.localizedStandardCompare($1.title) == .orderedAscending }
        }

        // Reusa o mesmo motor da paleta, para o filtro daqui se comportar como
        // a busca que você já conhece.
        let ranked = SearchEngine.search(query: search, templates: scoped, folders: store.folders)
        return ranked.map(\.template)
    }

    // MARK: - Ações

    private func createFolder() {
        let name = newFolderName.trimmingCharacters(in: .whitespaces)
        guard !name.isEmpty else { return }
        let order = (store.folders.map(\.order).max() ?? -1) + 1
        store.addFolder(Folder(name: name, order: order))
        newFolderName = ""
        showingNewFolder = false
    }

    private func createTemplate() {
        // Já nasce dentro da pasta selecionada: é quase sempre onde você quer.
        let folderID: UUID?
        if case .folder(let id) = selection { folderID = id } else { folderID = nil }

        let template = Template(title: "Novo template", body: "", folderID: folderID)
        store.add(template)
        selectedTemplateID = template.id
    }

    private func duplicate(_ template: Template) {
        let copy = Template(
            title: template.title + " (cópia)",
            body: template.body,
            folderID: template.folderID,
            tags: template.tags
        )
        store.add(copy)
        selectedTemplateID = copy.id
    }
}
