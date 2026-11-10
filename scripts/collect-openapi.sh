#!/usr/bin/env bash
# Collect the OpenAPI document of every running service.
#
# Usage: ./scripts/collect-openapi.sh [output-dir]
#   E2E / deployment defaults: services on 127.0.0.1:5100-5108
set -euo pipefail

out_dir="${1:-artifacts/openapi}"
mkdir -p "${out_dir}"

services=("gateway:5100" "idp:5101" "identity:5102" "session:5103" "authorization:5104" "audit:5105" "directory:5106" "compliance:5107" "notification:5108")

failed=0
for entry in "${services[@]}"; do
  name="${entry%%:*}"
  port="${entry##*:}"
  target="${out_dir}/${name}.json"
  if curl -fsS "http://127.0.0.1:${port}/openapi/v1.json" -o "${target}"; then
    echo "collected ${name} -> ${target}"
  else
    echo "skipped ${name} (port ${port} not serving OpenAPI)" >&2
    rm -f "${target}"
    failed=1
  fi
done

if [[ "${failed}" == "1" ]]; then
  echo "some services were unreachable; collected documents are still in ${out_dir}" >&2
fi
