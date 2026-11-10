#!/usr/bin/env bash
# Compare archived OpenAPI documents against a freshly collected set.
#
# Usage: ./scripts/openapi-diff.sh <base-dir> <current-dir> [fail-on]
#   base-dir      archived specs, one <service>.json per file (main branch)
#   current-dir   specs collected from the working tree
#   fail-on       oasdiff breaking level: ERR (default) or WARN
#
# The comparison only fails on *breaking* changes. When the base archive is
# missing or empty the script reports it and exits 0: the first run on a branch
# only produces the archive, it does not block.
#
# Environment:
#   OASDIFF_CMD   override the oasdiff binary (default: oasdiff)
set -euo pipefail

base_dir="${1:?usage: openapi-diff.sh <base-dir> <current-dir> [fail-on]}"
current_dir="${2:?usage: openapi-diff.sh <base-dir> <current-dir> [fail-on]}"
fail_on="${3:-ERR}"
oasdiff_cmd="${OASDIFF_CMD:-oasdiff}"

if ! compgen -G "${base_dir}/*.json" >/dev/null; then
  echo "no archived OpenAPI documents in ${base_dir}; skipping the compatibility gate" >&2
  echo "the documents collected from this revision were written to ${current_dir}" >&2
  exit 0
fi

if ! command -v "${oasdiff_cmd}" >/dev/null 2>&1; then
  echo "oasdiff not found (${oasdiff_cmd}); skipping the compatibility gate" >&2
  echo "install: https://github.com/oasdiff/oasdiff#installation" >&2
  exit 0
fi

status=0
checked=0
for base in "${base_dir}"/*.json; do
  name="$(basename "${base}")"
  current="${current_dir}/${name}"
  if [[ ! -f "${current}" ]]; then
    echo "::warning file=${current}::${name} was archived but is missing from this revision"
    continue
  fi
  checked=$((checked + 1))
  echo "== ${name}"
  if ! "${oasdiff_cmd}" breaking "${base}" "${current}" --fail-on "${fail_on}"; then
    echo "::error file=scripts/openapi-diff.sh::breaking OpenAPI change detected in ${name}"
    status=1
  fi
done

if [[ "${checked}" -eq 0 ]]; then
  echo "no comparable documents; skipping the compatibility gate"
  exit 0
fi

echo "== compared ${checked} document(s)"
exit "${status}"
