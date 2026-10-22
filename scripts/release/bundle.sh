#!/usr/bin/env bash
# Builds the downloadable install bundle for a release: everything needed to run Korkut from its
# published images, without the source code. Writes dist/korkut.tar.gz, dist/korkut.zip and
# dist/SHA256SUMS. The names carry no version so that .../releases/latest/download/korkut.tar.gz
# always gets the newest release; the VERSION file inside says which one it is. Both unpack into a
# folder named korkut, so unpacking a newer release over an existing install (and
# running ./install.sh again) updates it in place. .env is never shipped, so it is kept.
#
#   scripts/release/bundle.sh 2026.10.0
set -euo pipefail
version="${1:?usage: bundle.sh VERSION}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/dist"
stage="$(mktemp -d)/korkut"

mkdir -p "$stage/docker" "$out"
cp "$root/docker-compose.yml" "$root/install.sh" "$root/install.ps1" "$root/.env.example" \
   "$root/LICENSE" "$root/DISCLAIMER.md" "$root/CHANGELOG.md" "$stage/"
cp "$root/docker/init-db.sh" "$stage/docker/"
echo "$version" > "$stage/VERSION"
cat > "$stage/README.md" <<README
# Korkut $version

Hukuk büroları için açık kaynak yapay zeka asistanı. Gereksinim: Docker (Compose v2).

    ./install.sh        # Windows: .\\install.ps1

Ardından http://localhost:8090 adresini açın. Kurulum ilk çalıştırmada \`korkut-admin\` için bir
parola üretir ve ekrana yazar. Güncellemek için yeni sürümü bu klasörün üzerine açıp kurulumu
tekrar çalıştırın; \`.env\` dosyanız ve verileriniz korunur.

Belgeler: https://korkutai.com/dokumantasyon/ · Kaynak: https://github.com/bircex/korkut
README
chmod +x "$stage/install.sh"

cd "$(dirname "$stage")"
tar -czf "$out/korkut.tar.gz" korkut
rm -f "$out/korkut.zip"
zip -qr "$out/korkut.zip" korkut
cd "$out"
{ sha256sum korkut.tar.gz korkut.zip 2>/dev/null || shasum -a 256 korkut.tar.gz korkut.zip; } > SHA256SUMS
echo "bundle: Korkut $version in $out (korkut.tar.gz, korkut.zip, SHA256SUMS)"
