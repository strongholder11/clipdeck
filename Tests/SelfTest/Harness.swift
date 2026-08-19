import Foundation

/// Harness de testes mínimo.
///
/// O XCTest não acompanha as Command Line Tools (só vem com o Xcode completo),
/// então `swift test` não funciona nesta máquina. Como a lógica de busca e de
/// variáveis é justamente o que quebra em silêncio, vale mais um harness de 40
/// linhas que roda em qualquer lugar do que abrir mão dos testes.
///
/// Uso: `swift run SelfTest` — sai com código 1 se algo falhar.
enum Harness {
    private(set) static var failures: [String] = []
    private(set) static var checks = 0
    private static var currentSuite = ""

    static func suite(_ name: String, _ body: () -> Void) {
        currentSuite = name
        print("\n\u{1B}[1m\(name)\u{1B}[0m")
        body()
    }

    static func expect(
        _ condition: Bool,
        _ description: String,
        file: StaticString = #file,
        line: UInt = #line
    ) {
        checks += 1
        if condition {
            print("  \u{1B}[32m✓\u{1B}[0m \(description)")
        } else {
            let location = "\(URL(fileURLWithPath: "\(file)").lastPathComponent):\(line)"
            print("  \u{1B}[31m✗\u{1B}[0m \(description)  \u{1B}[2m(\(location))\u{1B}[0m")
            failures.append("[\(currentSuite)] \(description) — \(location)")
        }
    }

    static func expectEqual<T: Equatable>(
        _ actual: T,
        _ expected: T,
        _ description: String,
        file: StaticString = #file,
        line: UInt = #line
    ) {
        expect(actual == expected, "\(description)", file: file, line: line)
        if actual != expected {
            print("      esperado: \(expected)")
            print("      obtido:   \(actual)")
        }
    }

    static func finish() -> Never {
        print("")
        if failures.isEmpty {
            print("\u{1B}[32m\(checks) verificações passaram.\u{1B}[0m")
            exit(0)
        }
        print("\u{1B}[31m\(failures.count) de \(checks) falharam:\u{1B}[0m")
        failures.forEach { print("  • \($0)") }
        exit(1)
    }
}
