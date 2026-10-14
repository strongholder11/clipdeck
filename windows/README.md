# ClipDeck para Windows

Mesma ideia da versão macOS: você chama com um atalho, digita duas ou três
letras e o texto vai direto para onde estava escrevendo. Vive na bandeja, ao
lado do relógio.

## Instalação

Baixe o **ClipDeck-Windows** mais recente na aba
[Actions](../../actions/workflows/windows.yml) do repositório (abra a execução
mais recente e pegue o arquivo em "Artifacts"). Dentro tem duas opções:

- **`ClipDeck-Instalador.msi`** — instalador comum: dois cliques e pronto.
- **`ClipDeck.exe`** — o programa sozinho, sem instalar. Basta abrir.

Se preferir o `.exe` mas quiser atalho no menu Iniciar e abertura automática,
coloque o `Instalar.ps1` (da pasta `installer/`) na mesma pasta do `.exe`, clique
com o botão direito e escolha **Executar com o PowerShell**.

Não precisa de senha de administrador: tudo é instalado dentro do seu usuário.

> **Aviso do Windows na primeira execução.** O programa não tem assinatura
> comercial, então o SmartScreen mostra "O Windows protegeu o computador".
> Clique em **Mais informações → Executar assim mesmo**. Uma assinatura que
> remove esse aviso custa algumas centenas de dólares por ano.

## Uso

| Atalho | O que faz |
|---|---|
| `Alt+Espaço` | abre a paleta de busca |
| `Ctrl+Shift+C` | salva o que você copiou como template |
| `↑` `↓` | navega |
| `Enter` | cola no programa onde você estava |
| `Shift+Enter` | apenas copia |
| `Ctrl+1`–`Ctrl+9` | escolhe direto pela posição |
| `Ctrl+N` | salva a área de transferência como template |
| `Esc` | fecha |

Um clique numa linha também cola, e passar o mouse seleciona. Clique no ícone
da bandeja abre a paleta; o botão direito mostra o menu.

Os dois atalhos globais podem ser trocados em **Preferências** (menu da
bandeja). Se outro programa já usa a combinação, o menu da bandeja avisa.

### Cópias recentes

Sem nada digitado, a paleta lista no fim as três últimas coisas que você copiou.
`Enter` numa delas abre o formulário para salvá-la como template.

### Preferências

- **Atalhos**: clique no campo e pressione a combinação. `Esc` cancela.
- **Colar automaticamente**: desligado, `Enter` só copia e você cola com `Ctrl+V`.
- **Abrir o ClipDeck ao ligar o Windows**: liga e desliga o atalho na pasta
  Inicializar (o mesmo que o instalador cria).

### Importar e exportar

**Exportar templates…** e **Importar templates…** estão no menu da bandeja. Ao
importar, dá para **adicionar aos seus** (pastas de mesmo nome são
reaproveitadas) ou **substituir tudo**.

O formato é o mesmo da versão macOS: um arquivo exportado no Mac importa no
Windows e vice-versa. No Mac, o arquivo da biblioteca fica em
`~/Library/Application Support/ClipDeck/library.json` e pode ser importado
diretamente.

### Busca

- **Sem acento funciona**: `cobranca` acha "Cobrança", `reuniao` acha "reunião".
- **Abreviação funciona no título**: `flwp` acha "Follow-up pós-reunião".
- **`#tag`** restringe por tag, **`/pasta`** restringe por pasta, e ambos se
  combinam com texto livre: `aviso #urgente /cobranca`.
- O que você mais usa sobe sozinho na lista.

### Variáveis

Escreva `{{nome}}` no corpo do template e o ClipDeck pede o valor antes de colar,
com prévia ao vivo. Repetidas são preenchidas uma vez só. Três se resolvem
sozinhas: `{{data}}`, `{{hora}}` e `{{clipboard}}`.

## Onde ficam seus templates

`%APPDATA%\ClipDeck\library.json` — JSON legível, com escrita atômica e cinco
backups rotativos. Se o arquivo corromper, o app recupera do backup mais recente
e guarda o defeituoso como `library.corrupt-<data>.json` em vez de apagá-lo.

Desinstalar não apaga seus templates. As preferências ficam em
`settings.json`, na mesma pasta.

### Linha de comando

```
ClipDeck.exe --show-palette
ClipDeck.exe --show-palette --query "cobranca"
ClipDeck.exe --capture        # salva o clipboard como template
ClipDeck.exe --library        # abre a Biblioteca
ClipDeck.exe --preferences
```

`--debug` grava diagnóstico dos atalhos em `%APPDATA%\ClipDeck\debug.log`.
Cada abertura da paleta é registrada em `clipdeck.log` (só estado de janela,
nunca o conteúdo dos templates).

## Para quem for mexer no código

```
windows/
├── src/
│   ├── ClipDeck.Core/     lógica pura: busca, variáveis, persistência
│   └── ClipDeck.App/      interface Avalonia + código específico do Windows
├── tests/                 50 testes sobre a lógica
├── tools/ClipDeck.Snapshots/   renderiza as telas em PNG, sem monitor
└── installer/             MSI (WiX) e scripts PowerShell
```

`ClipDeck.Core` não conhece Windows nem interface gráfica: é a mesma lógica da
versão macOS, portada de Swift para C#, com os mesmos casos de teste. O código
específico do Windows (atalho global, colagem, área de transferência) fica atrás
de interfaces em `Platform/`, com implementações substitutas para o app subir em
outros sistemas durante o desenvolvimento.

```
dotnet test                                  # 50 testes
dotnet run --project tools/ClipDeck.Snapshots -- pasta-de-saida
```

O segundo comando desenha todas as telas em PNG usando o modo headless do
Avalonia. Foi assim que a interface foi conferida sem uma máquina Windows por
perto — vale a pena rodar depois de mexer em qualquer `.axaml`.

### O build oficial roda no GitHub Actions

O WiX, que monta o MSI, só funciona no Windows: ele próprio avisa que fora dele
"todo comportamento é indefinido". Por isso o instalador é montado pelo workflow
em `.github/workflows/windows.yml`, num runner Windows de verdade, que também
roda os testes e compila o executável.
