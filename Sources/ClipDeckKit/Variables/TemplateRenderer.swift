import Foundation

/// Uma variável encontrada no corpo de um template.
public struct TemplateVariable: Equatable, Identifiable {
    public let name: String
    /// Built-ins são resolvidas sozinhas e não viram campo no formulário.
    public let isBuiltIn: Bool

    public init(name: String, isBuiltIn: Bool) {
        self.name = name
        self.isBuiltIn = isBuiltIn
    }

    public var id: String { name }

    /// Rótulo legível: "proximo_passo" vira "Próximo passo"? Não — trocar
    /// underscore por espaço e capitalizar a primeira letra basta e não inventa
    /// acentuação que o usuário não escreveu.
    public var label: String {
        let spaced = name.replacingOccurrences(of: "_", with: " ")
        return spaced.prefix(1).uppercased() + spaced.dropFirst()
    }
}

/// Interpreta `{{variáveis}}` no corpo dos templates.
public enum TemplateRenderer {
    /// Variáveis resolvidas automaticamente, sem perguntar nada ao usuário.
    public static let builtInNames: Set<String> = ["data", "hora", "clipboard"]

    /// Extrai as variáveis na ordem em que aparecem, sem repetir.
    ///
    /// A ordem importa: é a ordem dos campos no formulário, e ela precisa bater
    /// com a leitura do texto para o preenchimento fazer sentido.
    public static func variables(in body: String) -> [TemplateVariable] {
        var found: [TemplateVariable] = []
        var seen: Set<String> = []

        for name in rawNames(in: body) {
            guard !seen.contains(name) else { continue }
            seen.insert(name)
            found.append(
                TemplateVariable(name: name, isBuiltIn: builtInNames.contains(name))
            )
        }
        return found
    }

    /// Só as variáveis que precisam ser preenchidas à mão.
    public static func fillableVariables(in body: String) -> [TemplateVariable] {
        variables(in: body).filter { !$0.isBuiltIn }
    }

    /// Substitui as variáveis pelos valores.
    ///
    /// Variáveis sem valor informado ficam como estão, em vez de virarem string
    /// vazia: um buraco visível no texto é melhor que uma frase que perdeu uma
    /// palavra sem você perceber.
    public static func render(
        _ body: String,
        values: [String: String],
        clipboard: String? = nil,
        now: Date = Date()
    ) -> String {
        var output = ""
        var remainder = Substring(body)

        while let open = remainder.range(of: "{{") {
            output += remainder[remainder.startIndex..<open.lowerBound]
            let afterOpen = remainder[open.upperBound...]

            guard let close = afterOpen.range(of: "}}") else {
                // Sem fechamento: o resto é texto literal, incluindo o "{{".
                output += remainder[open.lowerBound...]
                return output
            }

            let name = afterOpen[afterOpen.startIndex..<close.lowerBound]
                .trimmingCharacters(in: .whitespaces)

            if let resolved = resolve(name: name, values: values, clipboard: clipboard, now: now) {
                output += resolved
            } else {
                output += "{{\(name)}}"
            }

            remainder = afterOpen[close.upperBound...]
        }

        output += remainder
        return output
    }

    private static func resolve(
        name: String,
        values: [String: String],
        clipboard: String?,
        now: Date
    ) -> String? {
        // Valor informado tem prioridade sobre built-in: se o template usa
        // {{data}} e o usuário digitou uma data, vale a dele.
        if let provided = values[name], !provided.isEmpty { return provided }

        switch name {
        case "data": return formatted(now, style: "dd/MM/yyyy")
        case "hora": return formatted(now, style: "HH:mm")
        case "clipboard": return clipboard
        default: return nil
        }
    }

    private static func formatted(_ date: Date, style: String) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "pt_BR")
        formatter.dateFormat = style
        return formatter.string(from: date)
    }

    /// Nomes na ordem de aparição, com repetições.
    private static func rawNames(in body: String) -> [String] {
        var names: [String] = []
        var remainder = Substring(body)

        while let open = remainder.range(of: "{{") {
            let afterOpen = remainder[open.upperBound...]
            guard let close = afterOpen.range(of: "}}") else { break }

            let name = afterOpen[afterOpen.startIndex..<close.lowerBound]
                .trimmingCharacters(in: .whitespaces)
            if !name.isEmpty { names.append(name) }

            remainder = afterOpen[close.upperBound...]
        }
        return names
    }
}
