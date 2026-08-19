// swift-tools-version:5.10
import PackageDescription

let package = Package(
    name: "ClipDeck",
    platforms: [.macOS(.v13)],
    targets: [
        // Lógica pura: modelo, busca, renderização de variáveis.
        // Sem AppKit/SwiftUI, para poder ser testada sem levantar o app.
        .target(name: "ClipDeckKit", path: "Sources/ClipDeckKit"),

        // O app propriamente dito: UI, atalhos globais, clipboard.
        .executableTarget(
            name: "ClipDeck",
            dependencies: ["ClipDeckKit"],
            path: "Sources/ClipDeck"
        ),

        // Testes como executável comum, não .testTarget: o XCTest não acompanha
        // as Command Line Tools, então `swift test` não roda sem Xcode instalado.
        // Rode com `make test`.
        .executableTarget(
            name: "SelfTest",
            dependencies: ["ClipDeckKit"],
            path: "Tests/SelfTest"
        ),
    ]
)
