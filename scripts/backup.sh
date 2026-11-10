#!/usr/bin/env bash
# Back up the Nerves database and DataProtection key rings.
#
# Usage:
#   PGHOST=... PGPORT=... PGUSER=... PGPASSWORD=... PGDATABASE=... \
#     KEYS_DIR=/var/lib/nerves \
#     ./scripts/backup.sh /path/to/output
#
# Without PostgreSQL client tools on the host, point PG_DUMP_CMD at a wrapper:
#   PG_DUMP_CMD="docker compose exec -T postgres pg_dump -U nerves -d nerves --format=custom --no-owner" \
#     ./scripts/backup.sh ./backup
#
# Produces:
#   nerves-<timestamp>.dump          pg_dump custom format
#   nerves-<timestamp>.keys.tar.gz   key rings
#   nerves-<timestamp>.sha256        sums of both files
set -euo pipefail

out_dir="${1:?usage: backup.sh <output-directory>}"
mkdir -p "${out_dir}"

timestamp="$(date -u +%Y%m%d-%H%M%S)"
dump="${out_dir}/nerves-${timestamp}.dump"
keys="${out_dir}/nerves-${timestamp}.keys.tar.gz"
dump_cmd="${PG_DUMP_CMD:-pg_dump --format=custom --no-owner}"

echo "== pg_dump -> ${dump}"
# shellcheck disable=SC2086
${dump_cmd} > "${dump}"

echo "== packing key rings -> ${keys}"
keys_dir="${KEYS_DIR:-/var/lib/nerves}"
if [[ -d "${keys_dir}" ]]; then
  tar -czf "${keys}" -C "${keys_dir}" .
else
  echo "   key directory ${keys_dir} not found; skipping key ring archive" >&2
  keys=""
fi

echo "== checksums"
( cd "${out_dir}" && sha256sum "$(basename "${dump}")" $( [[ -n "${keys}" ]] && basename "${keys}" ) > "nerves-${timestamp}.sha256" )

echo "== done"
ls -l "${dump}" ${keys:+"${keys}"} "${out_dir}/nerves-${timestamp}.sha256"
echo
echo "Restore:"
echo "  pg_restore --clean --if-exists --no-owner -d \$PGDATABASE ${dump}"
echo "  stop identity and idp, restore the key ring archive, start them again"
