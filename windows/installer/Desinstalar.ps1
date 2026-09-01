# Remove o ClipDeck.
#
# Os seus templates NAO sao apagados: eles ficam em %APPDATA%\ClipDeck e
# continuam la caso voce reinstale. Para apagar tambem, remova essa pasta a mao.

$ErrorActionPreference = 'SilentlyContinue'

Get-Process ClipDeck | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\ClipDeck') -Recurse -Force
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ClipDeck.lnk') -Force
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\ClipDeck.lnk') -Force

Write-Host "ClipDeck removido." -ForegroundColor Green
Write-Host "Seus templates continuam em: $env:APPDATA\ClipDeck"
Read-Host "`nEnter para fechar"
