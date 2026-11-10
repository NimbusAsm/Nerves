#!/usr/bin/env bash
# Restore the newest backup into a scratch database and verify it.
#
# Usage: ./scripts/restore-drill.sh /backup/nerves
#
# Environment mirrors backup.sh: PGHOST/PGPORT/PGUSER/PGPASSWORD, plus
#   KEYS_DIR, KEEP=1 to keep the scratch database for inspection,
#   PG_RESTORE_CMD to override how pg_restore is invoked.
set -euo pipefail

backup_dir="${1:?usage: restore-drill.sh <backup-directory>}"
scratch_db="${SCRATCH_DB:-nerves_restore_drill}"
keep="${KEEP:-0}"
restore_cmd="${PG_RESTORE_CMD:-pg_restore --no-owner --clean --if-exists}"
psql_cmd="${PSQL_CMD:-psql}"

dump="$(ls -1t "${backup_dir}"/nerves-*.dump 2>/dev/null | head -1 || true)"
manifest="$(ls -1t "${backup_dir}"/nerves-*.sha256 2>/dev/null | head -1 || true)"

if [[ -z "${dump}" ]]; then
  echo "no dump found in ${backup_dir}" >&2
  exit 1
fi

if [[ -n "${manifest}" ]]; then
  echo "== verifying checksums"
  ( cd "${backup_dir}" && sha256sum -c "$(basename "${manifest}")" )
else
  echo "warning: no sha256 manifest next to ${dump}" >&2
fi

echo "== recreating ${scratch_db}"
${psql_cmd} -v ON_ERROR_STOP=1 -d postgres -c "drop database if exists ${scratch_db};" >/dev/null
${psql_cmd} -v ON_ERROR_STOP=1 -d postgres -c "create database ${scratch_db};" >/dev/null

echo "== restoring ${dump}"
# shellcheck disable=SC2086
${restore_cmd} -d "${scratch_db}" < "${dump}"

echo "== checking schemas"
missing=0
for schema in identity idp session authz audit directory compliance; do
  if ! ${psql_cmd} -v ON_ERROR_STOP=1 -d "${scratch_db}" -tAc \
      "select 1 from information_schema.schemata where schema_name='${schema}'" | grep -q 1; then
    echo "  missing schema: ${schema}" >&2
    missing=1
  else
    echo "  ok ${schema}"
  fi
done

echo "== checking migration histories"
rows=0
history_schemas=$(${psql_cmd} -v ON_ERROR_STOP=1 -d "${scratch_db}" -tAc \
  "select table_schema from information_schema.tables where table_name='__EFMigrationsHistory'")
for schema in ${history_schemas}; do
  count=$(${psql_cmd} -v ON_ERROR_STOP=1 -d "${scratch_db}" -tAc \
    "select count(*) from \"${schema}\".\"__EFMigrationsHistory\"")
  rows=$((rows + count))
done
echo "  migrations: ${rows}"

status=0
if [[ "${rows}" -lt 1 ]]; then
  echo "no migrations restored" >&2
  status=1
fi
if [[ "${missing}" -ne 0 ]]; then
  status=1
fi

if [[ "${keep}" == "1" ]]; then
  echo "scratch database kept: ${scratch_db}"
else
  ${psql_cmd} -v ON_ERROR_STOP=1 -d postgres -c "drop database if exists ${scratch_db};" >/dev/null
fi

if [[ "${status}" -eq 0 ]]; then
  echo "== restore drill passed"
else
  echo "== restore drill FAILED" >&2
fi
exit "${status}"
