@echo off
chcp 65001 >nul
title Instalar o ClipDeck

REM Instalador para quem baixou o repositorio inteiro.
REM
REM O codigo-fonte sozinho nao instala nada: seria preciso compilar. Este arquivo
REM busca o instalador ja pronto da pagina de Releases e o executa, para que
REM baixar o repositorio e dar dois cliques aqui simplesmente funcione.

echo.
echo   ============================================
echo      Instalador do ClipDeck
echo   ============================================
echo.
echo   Baixando o instalador (cerca de 41 MB)...
echo.

set "DESTINO=%TEMP%\ClipDeck-Instalador.msi"
set "URL=https://github.com/strongholder11/clipdeck/releases/latest/download/ClipDeck-Instalador.msi"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ProgressPreference='SilentlyContinue';" ^
  "try { Invoke-WebRequest -Uri '%URL%' -OutFile '%DESTINO%' -UseBasicParsing; exit 0 }" ^
  "catch { Write-Host $_.Exception.Message; exit 1 }"

if errorlevel 1 goto :falhou
if not exist "%DESTINO%" goto :falhou

echo   Download concluido. Abrindo o instalador...
echo.
echo   Se o Windows mostrar um aviso azul dizendo que protegeu o
echo   computador, clique em "Mais informacoes" e depois em
echo   "Executar assim mesmo".
echo.

start "" /wait msiexec /i "%DESTINO%"

del "%DESTINO%" >nul 2>&1

echo.
echo   Pronto! Procure o icone do ClipDeck na bandeja,
echo   ao lado do relogio.
echo.
echo     Alt + Espaco       abre a busca de templates
echo     Ctrl + Shift + C   salva o que voce copiou
echo.
pause
exit /b 0

:falhou
echo.
echo   Nao consegui baixar automaticamente.
echo.
echo   Vou abrir a pagina de download no navegador. Baixe o arquivo
echo   ClipDeck-Instalador.msi e de dois cliques nele.
echo.
pause
start "" "https://github.com/strongholder11/clipdeck/releases/latest"
exit /b 1
