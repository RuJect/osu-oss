#!/usr/bin/env bash
set -euo pipefail

identity="${1:-osu!oss Debug}"
out_dir="${2:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/debug-signing}"
keychain="$out_dir/osuoss-debug.keychain-db"
keychain_password="osuoss-debug"
p12_password="osuoss-debug"

mkdir -p "$out_dir"
rm -f "$keychain"

openssl_config="$out_dir/openssl.cnf"

cat > "$openssl_config" <<EOF
[req]
distinguished_name = dn
x509_extensions = code_signing
prompt = no

[dn]
CN = $identity

[code_signing]
basicConstraints = critical, CA:FALSE
keyUsage = critical, digitalSignature
extendedKeyUsage = codeSigning
EOF

openssl req -x509 -newkey rsa:2048 -nodes \
    -keyout "$out_dir/key.pem" \
    -out "$out_dir/cert.pem" \
    -days 730 \
    -config "$openssl_config"

openssl pkcs12 -export \
    -inkey "$out_dir/key.pem" \
    -in "$out_dir/cert.pem" \
    -out "$out_dir/osuoss-debug.p12" \
    -passout "pass:$p12_password"

security create-keychain -p "$keychain_password" "$keychain"
security set-keychain-settings -lut 21600 "$keychain"
security unlock-keychain -p "$keychain_password" "$keychain"
security import "$out_dir/osuoss-debug.p12" -k "$keychain" -P "$p12_password" -A -T /usr/bin/codesign
security set-key-partition-list -S apple-tool:,apple: -s -k "$keychain_password" "$keychain"

echo "Created $keychain"
echo "Set OSU_DEPLOY_AppleCodeSignCertName=$identity"
echo "Set OSU_DEPLOY_AppleKeyChainPath=$keychain"
