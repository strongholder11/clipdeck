import AppKit

// Bootstrap manual do NSApplication em vez de `@main struct App: SwiftUI.App`.
//
// O ciclo de vida do SwiftUI insiste em criar uma janela principal e não dá controle
// suficiente sobre NSPanel — e a paleta precisa ser um NSPanel para flutuar sobre
// apps em tela cheia e devolver o foco ao app anterior. Então subimos o AppKit na mão
// e usamos SwiftUI só para desenhar o conteúdo, hospedado em NSHostingView.

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate

// .accessory = sem ícone no Dock, sem menu bar de app, mas ainda pode
// mostrar janelas e receber foco de teclado (diferente de .prohibited).
app.setActivationPolicy(.accessory)

app.run()
