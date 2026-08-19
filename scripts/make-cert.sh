#!/bin/bash
# Cria o certificado autoassinado usado para assinar o ClipDeck.
#
# Por que isso existe: com assinatura ad-hoc, o macOS identifica o app pelo hash
# do binário, que muda a cada compilação — e a permissão de Acessibilidade cai
# junto. Com um certificado, o requisito designado do app vira
# "bundle id + certificado", que é estável entre builds.
#
# Roda uma vez por máquina. Pode pedir a senha do usuário para confiar no
# certificado.
set -euo pipefail

NAME="ClipDeck Dev"

if security find-identity -v -p codesigning 2>/dev/null | grep -q "$NAME"; then
  echo "✓ '$NAME' já existe e é válido — nada a fazer."
  exit 0
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

cat > "$WORK/cert.cnf" <<CNF
[req]
distinguished_name = dn
x509_extensions = v3
prompt = no

[dn]
CN = $NAME
O = ClipDeck
C = BR

[v3]
basicConstraints = critical,CA:false
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,codeSigning
subjectKeyIdentifier = hash
CNF

echo "→ gerando o par de chaves…"
openssl req -new -x509 -newkey rsa:2048 -nodes -days 3650 \
  -config "$WORK/cert.cnf" -keyout "$WORK/key.pem" -out "$WORK/cert.pem" 2>/dev/null

openssl pkcs12 -export -inkey "$WORK/key.pem" -in "$WORK/cert.pem" \
  -out "$WORK/bundle.p12" -passout pass:clipdeck -name "$NAME" 2>/dev/null

echo "→ importando no chaveiro de login…"
security import "$WORK/bundle.p12" -k ~/Library/Keychains/login.keychain-db \
  -P clipdeck -T /usr/bin/codesign >/dev/null

echo "→ marcando como confiável para assinatura de código (pode pedir sua senha)…"
security add-trusted-cert -r trustRoot -p codeSign \
  -k ~/Library/Keychains/login.keychain-db "$WORK/cert.pem"

if security find-identity -v -p codesigning | grep -q "$NAME"; then
  echo "✓ '$NAME' criado. Agora rode: make install"
else
  echo "✗ o certificado foi criado mas não ficou confiável."
  echo "  Abra o Acesso às Chaves, ache '$NAME' e marque 'Confiar sempre' para Assinatura de Código."
  exit 1
fi
