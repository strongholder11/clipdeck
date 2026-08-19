import ClipDeckKit
import SwiftUI

struct PaletteView: View {
    @ObservedObject var model: PaletteViewModel
    @FocusState private var searchFocused: Bool

    /// `paste` distingue ⏎ (colar) de ⇧⏎ (só copiar).
    var onCommit: (PaletteItem, _ paste: Bool) -> Void

    var body: some View {
        VStack(spacing: 0) {
            searchField
            Divider()

            if model.items.isEmpty {
                emptyState
            } else {
                itemsList
            }

            Divider()
            footer
        }
        .background(.regularMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.12), lineWidth: 1)
        )
        .onAppear { searchFocused = true }
    }

    private var searchField: some View {
        HStack(spacing: 10) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 16, weight: .medium))
                .foregroundStyle(.secondary)

            TextField("Buscar template…  (#tag  /pasta)", text: $model.query)
                .textFieldStyle(.plain)
                .font(.system(size: 20, weight: .regular))
                .focused($searchFocused)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }

    private var itemsList: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(Array(model.items.enumerated()), id: \.element.id) { index, item in
                        if index == model.recentSectionStart {
                            sectionHeader("Copiado recentemente — ⏎ salva como template")
                        }

                        row(for: item, at: index)
                            .id(item.id)
                            .contentShape(Rectangle())
                            .onTapGesture { onCommit(item, true) }
                            .onHover { hovering in
                                if hovering { model.selectIndex(index) }
                            }
                    }
                }
                .padding(6)
            }
            // Acompanha a navegação por teclado: sem isso, descer com ↓ passa do
            // fim da área visível e você seleciona algo fora da tela.
            .onChange(of: model.selectedIndex) { _ in
                guard let id = model.selectedItem?.id else { return }
                withAnimation(.easeOut(duration: 0.12)) {
                    proxy.scrollTo(id, anchor: .center)
                }
            }
        }
        .frame(maxHeight: 340)
    }

    @ViewBuilder
    private func row(for item: PaletteItem, at index: Int) -> some View {
        switch item {
        case .template(let result):
            TemplateRow(
                result: result,
                index: index,
                isSelected: index == model.selectedIndex,
                folderName: model.folderName(for: result),
                folderSymbol: model.folderSymbol(for: result)
            )
        case .recentCopy(let entry):
            RecentRow(entry: entry, isSelected: index == model.selectedIndex)
        }
    }

    private func sectionHeader(_ title: String) -> some View {
        HStack {
            Text(title)
                .font(.caption2.weight(.medium))
                .foregroundStyle(.tertiary)
            Spacer()
        }
        .padding(.horizontal, 10)
        .padding(.top, 8)
        .padding(.bottom, 2)
    }

    private var emptyState: some View {
        VStack(spacing: 6) {
            Image(systemName: "tray")
                .font(.system(size: 26))
                .foregroundStyle(.tertiary)
            Text(model.query.isEmpty ? "Nenhum template ainda" : "Nada encontrado")
                .foregroundStyle(.secondary)
            Text("⌘N salva o que está na área de transferência")
                .font(.caption)
                .foregroundStyle(.tertiary)
        }
        .frame(maxWidth: .infinity)
        .frame(height: 160)
    }

    private var footer: some View {
        HStack(spacing: 14) {
            KeyHint(keys: "↩", label: "colar")
            KeyHint(keys: "⇧↩", label: "copiar")
            KeyHint(keys: "⌘N", label: "novo do clipboard")
            Spacer()
            Text("\(model.templateCount)")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.tertiary)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 8)
    }
}

private struct KeyHint: View {
    let keys: String
    let label: String

    var body: some View {
        HStack(spacing: 4) {
            Text(keys)
                .font(.caption.weight(.semibold))
                .padding(.horizontal, 5)
                .padding(.vertical, 1)
                .background(Color.primary.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}

private struct RowBackground: ViewModifier {
    let isSelected: Bool

    func body(content: Content) -> some View {
        content
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(
                RoundedRectangle(cornerRadius: 7, style: .continuous)
                    .fill(isSelected ? Color.accentColor : Color.clear)
            )
            .foregroundStyle(isSelected ? Color.white : Color.primary)
    }
}

private struct TemplateRow: View {
    let result: SearchResult
    let index: Int
    let isSelected: Bool
    let folderName: String?
    let folderSymbol: String?

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: folderSymbol ?? "doc.text")
                .font(.system(size: 13))
                .foregroundStyle(isSelected ? .white : .secondary)
                .frame(width: 18)

            VStack(alignment: .leading, spacing: 2) {
                highlightedTitle
                    .font(.system(size: 14, weight: .medium))
                    .lineLimit(1)

                HStack(spacing: 6) {
                    if let folderName { Text(folderName) }
                    if !result.template.tags.isEmpty {
                        Text("·")
                        Text(result.template.tags.map { "#\($0)" }.joined(separator: " "))
                    }
                    if !result.template.preview.isEmpty {
                        Text("·")
                        Text(result.template.preview).lineLimit(1)
                    }
                }
                .font(.system(size: 11))
                .foregroundStyle(isSelected ? Color.white.opacity(0.8) : Color.secondary)
                .lineLimit(1)
            }

            Spacer(minLength: 8)

            // Atalho direto para os 9 primeiros, para quando você já sabe a posição.
            if index < 9 {
                Text("⌘\(index + 1)")
                    .font(.caption2.monospacedDigit())
                    .foregroundStyle(isSelected ? Color.white.opacity(0.7) : Color.secondary.opacity(0.5))
            }
        }
        .modifier(RowBackground(isSelected: isSelected))
    }

    /// Sublinha em negrito as letras que casaram com a busca.
    ///
    /// Os índices vêm do texto normalizado (sem acento). Normalmente o folding
    /// preserva a contagem de caracteres, mas não é garantido em todo idioma —
    /// então, se os tamanhos divergirem, mostramos o título sem destaque em vez
    /// de destacar a letra errada.
    private var highlightedTitle: Text {
        let title = result.template.title
        let folded = TextNormalizer.fold(title)
        let positions = Set(result.titleHighlights)

        guard !positions.isEmpty, folded.count == title.count else {
            return Text(title)
        }

        return title.enumerated().reduce(Text("")) { accumulated, pair in
            let (offset, character) = pair
            let piece = positions.contains(offset)
                ? Text(String(character)).fontWeight(.bold).underline()
                : Text(String(character))
            return accumulated + piece
        }
    }
}

private struct RecentRow: View {
    let entry: ClipboardWatcher.Entry
    let isSelected: Bool

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "doc.on.clipboard")
                .font(.system(size: 13))
                .foregroundStyle(isSelected ? .white : .secondary)
                .frame(width: 18)

            VStack(alignment: .leading, spacing: 2) {
                Text(entry.preview)
                    .font(.system(size: 13))
                    .lineLimit(1)
                Text(entry.copiedAt.formatted(date: .omitted, time: .shortened))
                    .font(.system(size: 11))
                    .foregroundStyle(isSelected ? Color.white.opacity(0.8) : Color.secondary)
            }

            Spacer(minLength: 8)

            Image(systemName: "plus.circle")
                .font(.system(size: 12))
                .foregroundStyle(isSelected ? Color.white.opacity(0.8) : Color.secondary.opacity(0.6))
        }
        .modifier(RowBackground(isSelected: isSelected))
    }
}
