# ClipDeck

Gerenciador de templates de mensagem para macOS. App de barra de menu: você chama
com um atalho, digita duas ou três letras e o texto vai direto para onde você
estava escrevendo.

## Uso

| Atalho | O que faz |
|---|---|
| `⌥Espaço` | abre a paleta de busca |
| `⌥⌘C` | salva o que você acabou de copiar como template |
| `↑` `↓` (ou `⌃N` `⌃P`) | navega |
| `⏎` | cola no app onde você estava |
| `⇧⏎` | apenas copia |
| `⌘1`–`⌘9` | escolhe direto pela posição |
| `⌘N` | salva a área de transferência como template |
| `Esc` | fecha |

Os atalhos globais podem ser trocados em **Preferências** (menu da barra, ou `⌘,`).

### Busca

Digitar filtra por título, tags, pasta e conteúdo:

- **Sem acento funciona**: `cobranca` acha "Cobrança", `reuniao` acha "reunião".
- **Abreviação funciona no título**: `flwp` acha "Follow-up pós-reunião".
- **`#tag`** restringe por tag, **`/pasta`** restringe por pasta. Podem ser
  combinados com texto livre: `aviso #urgente /cobranca`.
- O que você mais usa sobe sozinho na lista.

### Variáveis

Escreva `{{nome}}` no corpo do template e, ao usá-lo, o ClipDeck pede o valor
antes de colar, com prévia ao vivo. Variáveis repetidas são preenchidas uma vez só.

Três são resolvidas automaticamente e não viram campo:

| Variável | Vira |
|---|---|
| `{{data}}` | data de hoje (dd/MM/aaaa) |
| `{{hora}}` | hora atual (24h) |
| `{{clipboard}}` | o que estava na área de transferência |

## Instalação

### Pré-requisitos

Precisa do compilador do Swift, que não vem instalado por padrão. Sem Xcode
completo, as Ferramentas de Linha de Comando bastam — é tudo que este projeto
usa:

```
xcode-select --install
```

Aparece um instalador do sistema; aceite e espere terminar (alguns minutos,
dependendo da internet). Se você já tem o Xcode completo instalado, pode pular
esse passo.

Confirma que funcionou:

```
swift --version
```

### Compilar e instalar

```
git clone https://github.com/strongholder11/clipdeck.git
cd clipdeck
make cert
make run
```

- `make cert` cria um certificado local (uma vez por máquina) para a permissão
  de Acessibilidade não cair a cada recompilação — veja o porquê mais abaixo.
- `make run` compila, monta `ClipDeck.app`, instala em `~/Applications` e abre.

O ícone aparece na barra de menu (não no Dock).

### Permissão de Acessibilidade

Só o **colar automático** precisa dela. Sem a permissão o app funciona igual, mas
`⏎` copia em vez de colar — nada é perdido, você só aperta `⌘V`.

Na primeira execução o ClipDeck pede a permissão e o diálogo do sistema já
adiciona o app à lista dos Ajustes. Basta marcá-lo em **Ajustes do Sistema ›
Privacidade e Segurança › Acessibilidade**.

#### Por que existe o `make cert`

Com assinatura ad-hoc, o macOS identifica o app pelo **hash do binário**, que muda
a cada compilação. O resultado é que a permissão de Acessibilidade cai toda vez
que você recompila — a caixa continua marcada nos Ajustes, mas não vale mais, o
que é especialmente confuso porque nada indica o problema.

`make cert` cria um certificado autoassinado local e o marca como confiável para
assinatura de código. Com ele, o requisito designado do app passa a ser:

```
identifier "com.felipe.clipdeck" and certificate root = H"..."
```

Bundle ID + certificado, em vez do hash. Isso é estável entre builds, e a
permissão para de cair. Rode uma vez por máquina:

```
make cert
make install
```

O `bundle.sh` detecta o certificado sozinho e passa a usá-lo. Se ele não existir,
o build cai de volta para ad-hoc e avisa no terminal.

## Comandos

| Comando | O que faz |
|---|---|
| `make build` | compila em release |
| `make test` | roda os testes |
| `make install` | monta o `.app` e instala em `~/Applications` |
| `make run` | instala e abre |
| `make stop` | encerra o app |
| `make clean` | apaga artefatos de build |

## Onde ficam seus dados

`~/Library/Application Support/ClipDeck/library.json` — JSON legível, dá para
versionar no git, sincronizar via iCloud Drive ou editar à mão.

Toda gravação é atômica e passa por um backup rotativo (5 cópias em `Backups/`).
Se o arquivo for corrompido, o app recupera do backup mais recente e preserva o
arquivo defeituoso como `library.corrupt-<data>.json` em vez de apagá-lo.

Exportar e importar em JSON estão no menu da barra.

## Estrutura

```
Sources/
├── ClipDeckKit/          lógica pura, testável sem levantar o app
│   ├── Model/            Template, Folder, Library, Store, LibraryStorage
│   ├── Search/           TextNormalizer, FuzzyMatcher, SearchEngine
│   └── Variables/        TemplateRenderer
└── ClipDeck/             o app
    ├── Hotkeys/          atalhos globais (Carbon), gravador de atalhos
    ├── Clipboard/        observador de cópias, colagem via CGEvent
    └── UI/               paleta, captura, preenchimento, biblioteca, preferências
```

### Testes

O XCTest não acompanha as Command Line Tools, então `swift test` não funciona sem
o Xcode completo. Os testes rodam como um executável comum (`Tests/SelfTest`), com
um harness de ~40 linhas — `make test`. São 62 verificações cobrindo busca,
variáveis e persistência.

## Flags de linha de comando

Úteis para automação externa (Atalhos, Raycast) e para inspecionar estados:

```
open -a ClipDeck --args --show-palette
open -a ClipDeck --args --show-palette --query "cobranca"
open -a ClipDeck --args --capture        # salva o clipboard como template
open -a ClipDeck --args --library        # abre a Biblioteca
open -a ClipDeck --args --preferences
```

`--debug` grava diagnóstico em `~/Library/Application Support/ClipDeck/debug.log`
(estado do painel, foco, número de itens) — útil se o atalho ou o foco falharem.

## Fora de escopo por enquanto

Sync entre máquinas, expansão automática por gatilho de digitação (`;fup` →
expande), texto rico e imagens, e histórico completo de área de transferência.
Como o arquivo é JSON puro, sync via iCloud Drive ou git funciona sem refatoração.
