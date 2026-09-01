# Instalador do ClipDeck.
#
# Alternativa ao MSI para quem prefere não esperar o build: copia o programa para
# a pasta do usuário, cria o atalho no menu Iniciar e faz o app abrir junto com o
# Windows. Não precisa de administrador — tudo acontece dentro do seu perfil.
#
# Como usar: clique com o botão direito neste arquivo e escolha
# "Executar com o PowerShell".

$ErrorActionPreference = 'Stop'

$origem  = Join-Path $PSScriptRoot 'ClipDeck.exe'
$destino = Join-Path $env:LOCALAPPDATA 'Programs\ClipDeck'
$exe     = Join-Path $destino 'ClipDeck.exe'

if (-not (Test-Path $origem)) {
    Write-Host "ClipDeck.exe nao encontrado nesta pasta." -ForegroundColor Red
    Write-Host "Coloque o Instalar.ps1 na mesma pasta do ClipDeck.exe." -ForegroundColor Red
    Read-Host "`nEnter para fechar"
    exit 1
}

Write-Host "Instalando o ClipDeck..." -ForegroundColor Cyan

# Encerra uma instalacao anterior: copiar por cima de um executavel em uso falha.
Get-Process ClipDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force -Path $destino | Out-Null
Copy-Item $origem $exe -Force
Write-Host "  copiado para $destino"

$shell = New-Object -ComObject WScript.Shell

$menu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ClipDeck.lnk'
$atalho = $shell.CreateShortcut($menu)
$atalho.TargetPath = $exe
$atalho.WorkingDirectory = $destino
$atalho.Description = 'Gerenciador de templates de mensagem'
$atalho.Save()
Write-Host "  atalho criado no menu Iniciar"

# O app vive na bandeja e precisa estar rodando para o atalho global funcionar.
$inicializar = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\ClipDeck.lnk'
$auto = $shell.CreateShortcut($inicializar)
$auto.TargetPath = $exe
$auto.WorkingDirectory = $destino
$auto.Save()
Write-Host "  configurado para abrir junto com o Windows"

Start-Process $exe
Write-Host ""
Write-Host "Pronto! O ClipDeck esta rodando." -ForegroundColor Green
Write-Host "Procure o icone na bandeja, ao lado do relogio."
Write-Host ""
Write-Host "  Alt + Espaco        abre a busca de templates"
Write-Host "  Ctrl + Shift + C    salva o que voce copiou como template"
Write-Host ""
Read-Host "Enter para fechar"
