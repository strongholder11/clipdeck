import ClipDeckKit
import SwiftUI

/// Formulário para preencher as variáveis antes de colar.
struct FillView: View {
    let templateTitle: String
    let body_: String
    let variables: [TemplateVariable]
    let clipboardText: String?

    @State private var values: [String: String] = [:]
    @FocusState private var focusedField: String?

    var onConfirm: (String) -> Void
    var onCancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()

            VStack(alignment: .leading, spacing: 10) {
                ForEach(variables) { variable in
                    VStack(alignment: .leading, spacing: 3) {
                        Text(variable.label)
                            .font(.caption.weight(.medium))
                            .foregroundStyle(.secondary)

                        TextField(variable.label, text: binding(for: variable.name))
                            .textFieldStyle(.roundedBorder)
                            .focused($focusedField, equals: variable.name)
                            .onSubmit(advanceOrConfirm)
                    }
                }
            }
            .padding(16)

            Divider()
            preview
            Divider()
            footer
        }
        .frame(width: 460)
        .background(.regularMaterial)
        .onAppear { focusedField = variables.first?.name }
    }

    private var header: some View {
        HStack(spacing: 8) {
            Image(systemName: "curlybraces")
                .foregroundStyle(.secondary)
            Text(templateTitle)
                .font(.headline)
                .lineLimit(1)
            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    /// Prévia ao vivo do resultado.
    ///
    /// Sem ela você só descobre que errou um campo depois de colar na conversa
    /// do cliente — que é exatamente onde o erro custa caro.
    private var preview: some View {
        ScrollView {
            Text(rendered)
                .font(.system(size: 12))
                .frame(maxWidth: .infinity, alignment: .leading)
                .textSelection(.enabled)
                .padding(10)
        }
        .frame(height: 130)
    }

    private var footer: some View {
        HStack {
            if remaining > 0 {
                Text("\(remaining) campo\(remaining == 1 ? "" : "s") em branco")
                    .font(.caption)
                    .foregroundStyle(.orange)
            }
            Spacer()
            Button("Cancelar", action: onCancel)
                .keyboardShortcut(.cancelAction)
            Button("Colar") { onConfirm(rendered) }
                .keyboardShortcut(.defaultAction)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    private var rendered: String {
        TemplateRenderer.render(body_, values: values, clipboard: clipboardText)
    }

    private var remaining: Int {
        variables.filter { (values[$0.name] ?? "").isEmpty }.count
    }

    private func binding(for name: String) -> Binding<String> {
        Binding(
            get: { values[name] ?? "" },
            set: { values[name] = $0 }
        )
    }

    /// ⏎ vai para o próximo campo vazio; no último, confirma. Assim dá para
    /// preencher tudo sem tirar a mão do teclado nem contar quantos Tabs faltam.
    private func advanceOrConfirm() {
        guard let current = focusedField,
              let index = variables.firstIndex(where: { $0.name == current })
        else { return onConfirm(rendered) }

        let next = variables.dropFirst(index + 1).first { (values[$0.name] ?? "").isEmpty }
        if let next {
            focusedField = next.name
        } else {
            onConfirm(rendered)
        }
    }
}
