#!/bin/bash
# Monta ClipDeck.app a partir do executável do SwiftPM.
#
# O SwiftPM só produz um binário solto; o macOS precisa da estrutura de bundle
# para tratar como app (barra de menu, Info.plist, TCC). Aqui montamos na mão.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIG:-release}"
BINARY="$ROOT/.build/$CONFIG/ClipDeck"
APP="$ROOT/build/ClipDeck.app"

[ -f "$BINARY" ] || { echo "erro: binário não encontrado em $BINARY — rode 'make build'"; exit 1; }

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "$BINARY" "$APP/Contents/MacOS/ClipDeck"
cp "$ROOT/Resources/Info.plist" "$APP/Contents/Info.plist"
[ -f "$ROOT/Resources/AppIcon.icns" ] && cp "$ROOT/Resources/AppIcon.icns" "$APP/Contents/Resources/"

# Assinatura. Sem certificado, o macOS identifica o app pelo hash do binário, e a
# permissão de Acessibilidade cai a cada rebuild. Definir CODESIGN_IDENTITY com um
# certificado autoassinado torna a permissão estável entre builds.
# Prefere o certificado local se existir. Com ele o requisito designado do app
# passa a ser "bundle id + certificado" em vez do hash do binário — e é isso que
# faz a permissão de Acessibilidade sobreviver a uma recompilação.
if [ -z "${CODESIGN_IDENTITY:-}" ] && security find-identity -v -p codesigning 2>/dev/null | grep -q "ClipDeck Dev"; then
  CODESIGN_IDENTITY="ClipDeck Dev"
fi

IDENTITY="${CODESIGN_IDENTITY:--}"
codesign --force --sign "$IDENTITY" --identifier com.felipe.clipdeck "$APP" 2>&1 | sed 's/^/  /'

if [ "$IDENTITY" = "-" ]; then
  echo "  (assinatura ad-hoc — a permissão de Acessibilidade vai cair a cada build)"
else
  echo "  assinado com: $IDENTITY"
fi

echo "✓ $APP"
