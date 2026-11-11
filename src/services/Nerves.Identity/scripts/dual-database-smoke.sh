#!/usr/bin/env bash
# Manual verification for identity sharding (B9 phase 2).
#
# Boots the backend against a global database plus two account shards and a
# real IdP, then walks a real /api/v1/auth/login -> /connect/authorize ->
# /connect/token flow, proving an account that lives on a shard is still
# reachable through the normal login path.
#
# Everything is temporary: the databases are created here and dropped on exit.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
log_dir="${SHARD_LOG_DIR:-$(mktemp -d)}"
base="${SHARD_PORT_BASE:-5510}"
db_host="${SHARD_DB_HOST:-127.0.0.1}"
db_port="${SHARD_DB_PORT:-55440}"
db_user="${SHARD_DB_USER:-nerves}"
db_password="${SHARD_DB_PASSWORD:-nerves}"
global_db="${SHARD_GLOBAL_DB:-nerves_shard_smoke_global}"
shard0_db="${SHARD_SHARD0_DB:-nerves_shard_smoke_s0}"
shard1_db="${SHARD_SHARD1_DB:-nerves_shard_smoke_s1}"

if command -v psql >/dev/null 2>&1; then
  psql_cmd=(psql -h "${db_host}" -p "${db_port}" -U "${db_user}")
else
  psql_cmd=(docker exec "${SHARD_PG_CONTAINER:-nerves-dev-postgres-1}" psql -U "${db_user}")
fi

PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d postgres -c "select 1" >/dev/null

connection() {
  echo "Host=${db_host};Port=${db_port};Database=$1;Username=${db_user};Password=${db_password}"
}

gateway=$((base + 0))
idp=$((base + 1))
identity=$((base + 2))
session=$((base + 3))
authorization=$((base + 4))
audit=$((base + 5))
directory=$((base + 6))
compliance=$((base + 7))
notification=$((base + 8))

identity_grpc=$((base + 102))
session_grpc=$((base + 103))
authorization_grpc=$((base + 104))
audit_grpc=$((base + 105))
directory_grpc=$((base + 106))

pids=()
cleanup() {
  for pid in "${pids[@]:-}"; do
    kill "${pid}" 2>/dev/null || true
  done
  wait 2>/dev/null || true

  if [[ "${SHARD_KEEP:-0}" == "1" ]]; then
    echo "kept databases and logs: global=${global_db} shard0=${shard0_db} shard1=${shard1_db} logs=${log_dir}"
    return
  fi

  for name in "${shard0_db}" "${shard1_db}" "${global_db}"; do
    PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d postgres \
      -c "drop database if exists ${name} with (force)" >/dev/null 2>&1 || true
  done
  echo "dropped databases ${global_db}, ${shard0_db}, ${shard1_db}"
}
trap cleanup EXIT INT TERM

echo "== recreating databases"
for name in "${global_db}" "${shard0_db}" "${shard1_db}"; do
  PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d postgres -c "drop database if exists ${name} with (force)" >/dev/null
  PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d postgres -c "create database ${name}" >/dev/null
done

mkdir -p "${log_dir}"

common_env=(
  "ASPNETCORE_ENVIRONMENT=Development"
  "ConnectionStrings__Default=$(connection "${global_db}")"
  "Database__MigrateOnStart=true"
  "DataProtection__KeysDirectory=${log_dir}/dp-keys"
  "Authentication__Authority=http://127.0.0.1:${idp}"
  "Authentication__Issuer=http://127.0.0.1:${idp}/"
  "Authentication__Audience=nerves.api"
)

start() {
  local project="$1" port="$2" grpc="${3:-}"
  local workdir="${repo_root}/src/services/${project}"
  local bin="${workdir}/bin/Debug/net10.0/${project}.dll"
  local envs=("${common_env[@]}" "Kestrel__Endpoints__Http__Url=http://127.0.0.1:${port}" "Kestrel__Endpoints__Http__Protocols=Http1")

  if [[ -n "${grpc}" ]]; then
    envs+=("Kestrel__Endpoints__Grpc__Url=http://127.0.0.1:${grpc}" "Kestrel__Endpoints__Grpc__Protocols=Http2")
  fi

  case "${project}" in
    Nerves.Identity)
      envs+=(
        "Sharding__Enabled=true"
        "Sharding__ShardCount=2"
        "Sharding__ConnectionStrings__0=$(connection "${shard0_db}")"
        "Sharding__ConnectionStrings__1=$(connection "${shard1_db}")"
        "Session__GrpcAddress=http://127.0.0.1:${session_grpc}"
        "Audit__GrpcAddress=http://127.0.0.1:${audit_grpc}"
        "Notification__BaseAddress=http://127.0.0.1:${notification}"
        "Notification__AdminTokenFile=${log_dir}/admin-token"
        "Verification__Provider=manual"
      )
      ;;
    Nerves.Idp)
      envs+=(
        "Identity__GrpcAddress=http://127.0.0.1:${identity_grpc}"
        "Session__GrpcAddress=http://127.0.0.1:${session_grpc}"
        "Authorization__GrpcAddress=http://127.0.0.1:${authorization_grpc}"
        "Audit__GrpcAddress=http://127.0.0.1:${audit_grpc}"
        "PasswordReset__PublicBaseUrl=http://127.0.0.1:${gateway}"
      )
      ;;
    Nerves.Authorization)
      # The authorization seeder bootstraps from identity.network_account via
      # SQL against the global database, which no longer holds accounts when
      # sharding is on. The smoke only needs the login path, so skip the seed.
      envs+=("DevSeed__Enabled=false")
      ;;
    Nerves.Gateway)
      envs+=(
        "Gateway__Services__identity=http://127.0.0.1:${identity}/"
        "Gateway__Services__session=http://127.0.0.1:${session}/"
        "Gateway__Services__authorization=http://127.0.0.1:${authorization}/"
        "Gateway__Services__audit=http://127.0.0.1:${audit}/"
        "Gateway__Services__directory=http://127.0.0.1:${directory}/"
        "Gateway__Services__compliance=http://127.0.0.1:${compliance}/"
        "Gateway__Services__notification=http://127.0.0.1:${notification}/"
        "Gateway__Services__idp=http://127.0.0.1:${idp}/"
      )
      ;;
  esac

  ( cd "${workdir}" && exec env "${envs[@]}" dotnet "${bin}" > "${log_dir}/${project}.log" 2>&1 ) &
  pids+=($!)
}

echo "== building"
export PATH="${HOME}/.dotnet:${PATH}"
dotnet build "${repo_root}/Nerves.slnx" -warnaserror >/dev/null

echo "== starting services on base port ${base}"
start Nerves.Identity "${identity}" "${identity_grpc}"
start Nerves.Session "${session}" "${session_grpc}"
start Nerves.Authorization "${authorization}" "${authorization_grpc}"
start Nerves.Audit "${audit}" "${audit_grpc}"
start Nerves.Directory "${directory}" "${directory_grpc}"
start Nerves.ComplianceCn "${compliance}"
start Nerves.Notification "${notification}"
start Nerves.Gateway "${gateway}"
start Nerves.Idp "${idp}"

wait_for() {
  local port="$1" name="$2"
  for _ in $(seq 1 90); do
    if curl -fsS -o /dev/null "http://127.0.0.1:${port}/healthz" 2>/dev/null; then
      echo "   ${name} ready on ${port}"
      return 0
    fi
    sleep 1
  done
  echo "   ${name} did not become ready; see ${log_dir}/${name}.log" >&2
  return 1
}

wait_for "${identity}" Identity
wait_for "${session}" Session
wait_for "${authorization}" Authorization
wait_for "${audit}" Audit
wait_for "${directory}" Directory
wait_for "${compliance}" ComplianceCn
wait_for "${notification}" Notification
wait_for "${idp}" Idp
wait_for "${gateway}" Gateway

echo "== login smoke against the sharded identity"
SHARD_IDP="http://127.0.0.1:${idp}" \
SHARD_GLOBAL_DSN="$(connection "${global_db}")" \
SHARD_ZERO_DSN="$(connection "${shard0_db}")" \
SHARD_ONE_DSN="$(connection "${shard1_db}")" \
SHARD_PSQL_CMD="${psql_cmd[*]}" \
SHARD_GLOBAL_DB="${global_db}" \
  python3 "${repo_root}/src/services/Nerves.Identity/scripts/dual-database-smoke.py"

echo "== sharding smoke passed (logs: ${log_dir})"