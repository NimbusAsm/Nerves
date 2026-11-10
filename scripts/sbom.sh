#!/usr/bin/env bash
# Generate a CycloneDX SBOM for a container image with Syft.
#
# Usage: ./scripts/sbom.sh <image-ref> [output-dir]
#   output-dir defaults to artifacts/sbom
#
# Environment:
#   SYFT_CMD        override the syft binary (default: syft)
#   SBOM_FORMAT     syft output format (default: cyclonedx-json)
#
# When Syft is not installed the script prints a skip notice and exits 0.
set -euo pipefail

image="${1:?usage: sbom.sh <image-ref> [output-dir]}"
out_dir="${2:-artifacts/sbom}"
syft_cmd="${SYFT_CMD:-syft}"
format="${SBOM_FORMAT:-cyclonedx-json}"

if ! command -v "${syft_cmd}" >/dev/null 2>&1; then
  echo "syft not found (${syft_cmd}); skipping the SBOM for ${image}" >&2
  echo "install: https://github.com/anchore/syft#installation" >&2
  exit 0
fi

mkdir -p "${out_dir}"
# Image references contain '/', ':' and sometimes '@'; keep the file name flat.
slug="$(printf '%s' "${image}" | tr '/:@' '---')"
target="${out_dir}/${slug}.${format}"

echo "== generating SBOM for ${image} -> ${target}"
"${syft_cmd}" "${image}" -o "${format}=${target}"
echo "== ${target}"
