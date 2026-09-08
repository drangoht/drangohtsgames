#!/bin/sh
# Télécharge les builds Web déclarés dans web-builds.json (ADR 0007).
#
# Usage : fetch-web-builds.sh <manifeste> <destination>
#
# Chaque build atterrit dans <destination>/<slug>/, index.html à la racine.
# Toute défaillance interrompt le script : une image amputée de ses jeux passerait
# toutes les vérifications au vert et serait déployée sans que rien ne le signale.

set -eu

manifest="${1:?chemin du manifeste attendu}"
destination="${2:?répertoire de destination attendu}"

command -v jq > /dev/null || { echo "jq est requis." >&2; exit 1; }
command -v unzip > /dev/null || { echo "unzip est requis." >&2; exit 1; }

[ -f "$manifest" ] || { echo "Manifeste introuvable : $manifest" >&2; exit 1; }

# Une ligne par jeu : "slug depot etiquette". La lecture passe par une variable puis un
# fichier, jamais par un tube : dans `jq … | while …`, l'échec de jq est masqué par le
# code de sortie du while, et le `exit` du corps ne quitte qu'un sous-shell. Le script
# rendait alors la main en succès sans avoir rien téléchargé.
builds="$(jq -r '.builds | to_entries[] | "\(.key) \(.value.repository) \(.value.tag)"' "$manifest")"

[ -n "$builds" ] || { echo "Aucun build déclaré dans $manifest." >&2; exit 1; }

mkdir -p "$destination"

liste="$(mktemp)"
printf '%s\n' "$builds" > "$liste"

while read -r slug repository tag; do
    [ -n "$slug" ] || continue

    archive="$(mktemp)"
    url="https://github.com/${repository}/releases/download/${tag}/web.zip"

    echo "→ ${slug} : ${repository}@${tag}"

    # --fail est indispensable : sans lui, la page d'erreur HTML de GitHub serait
    # téléchargée avec succès, et l'échec n'apparaîtrait qu'au dézippage.
    curl --fail --location --silent --show-error --output "$archive" "$url"

    rm -rf "${destination:?}/${slug}"
    unzip -q "$archive" -d "${destination}/${slug}"
    rm -f "$archive"

    if [ ! -f "${destination}/${slug}/index.html" ]; then
        echo "L'archive de ${slug} ne porte pas index.html à sa racine." >&2
        exit 1
    fi
done < "$liste"

rm -f "$liste"

echo "Builds Web récupérés dans ${destination}."
