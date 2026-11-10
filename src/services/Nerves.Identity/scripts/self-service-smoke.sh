#!/usr/bin/env bash
# Local end-to-end check for self-service password reset (A1).
#
# Boots the backend on a spare port block against its own PostgreSQL database,
# takes a management token, then walks:
#   request (unknown + known) -> queued email -> confirm -> replay rejected -> login
#
# The notification service runs with Smtp:Disabled=true in this stack, so the
# reset link is only visible in the identity log; the script extracts the token
# from there to prove the full chain works without a mail server.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
log_dir="${E2E_LOG_DIR:-$(mktemp -d)}"
base="${RESET_PORT_BASE:-5130}"
db_host="${RESET_DB_HOST:-127.0.0.1}"
db_port="${RESET_DB_PORT:-55440}"
db_name="${RESET_DB_NAME:-nerves_reset_smoke}"
db_user="${RESET_DB_USER:-nerves}"
db_password="${RESET_DB_PASSWORD:-nerves}"

connection_string="Host=${db_host};Port=${db_port};Database=${db_name};Username=${db_user};Password=${db_password}"

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
  if [[ "${RESET_KEEP:-0}" == "1" ]]; then
    echo "services kept running; logs in ${log_dir}"
    return
  fi
  for pid in "${pids[@]:-}"; do
    kill "${pid}" 2>/dev/null || true
  done
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

common_env=(
  "ASPNETCORE_ENVIRONMENT=Development"
  "ConnectionStrings__Default=${connection_string}"
  "Database__MigrateOnStart=true"
  "DataProtection__KeysDirectory=${log_dir}/dp-keys"
  "Authentication__Authority=http://127.0.0.1:${idp}"
  "Authentication__Issuer=http://127.0.0.1:${idp}/"
  "Authentication__Audience=nerves.api"
)

start_service() {
  local project="$1" port="$2" grpc="${3:-}"
  local workdir="${repo_root}/src/services/${project}"
  local bin="${workdir}/bin/Debug/net10.0/${project}.dll"
  local envs=("${common_env[@]}" "Kestrel__Endpoints__Http__Url=http://127.0.0.1:${port}" "Kestrel__Endpoints__Http__Protocols=Http1")

  if [[ -n "${grpc}" ]]; then
    envs+=("Kestrel__Endpoints__Grpc__Url=http://127.0.0.1:${grpc}" "Kestrel__Endpoints__Grpc__Protocols=Http2")
  fi

  if [[ "${project}" == "Nerves.Idp" ]]; then
    envs+=(
      "Identity__GrpcAddress=http://127.0.0.1:${identity_grpc}"
      "Session__GrpcAddress=http://127.0.0.1:${session_grpc}"
      "Authorization__GrpcAddress=http://127.0.0.1:${authorization_grpc}"
      "Audit__GrpcAddress=http://127.0.0.1:${audit_grpc}"
      "PasswordReset__PublicBaseUrl=http://127.0.0.1:5300"
    )
  fi

  if [[ "${project}" == "Nerves.Identity" ]]; then
    envs+=(
      "Session__GrpcAddress=http://127.0.0.1:${session_grpc}"
      "Audit__GrpcAddress=http://127.0.0.1:${audit_grpc}"
      "Notification__BaseAddress=http://127.0.0.1:${notification}"
      "Notification__AdminTokenFile=${log_dir}/admin-token"
      "Verification__Provider=manual"
      "Verification__PublicBaseUrl=http://127.0.0.1:${gateway}"
    )
  fi

  if [[ "${project}" == "Nerves.Gateway" ]]; then
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
  fi

  ( cd "${workdir}" && exec env "${envs[@]}" dotnet "${bin}" > "${log_dir}/${project}.log" 2>&1 ) &
  pids+=($!)
}

mkdir -p "${log_dir}"

# psql access: prefer a local client, fall back to a PostgreSQL container.
if command -v psql >/dev/null 2>&1; then
  psql_cmd=(psql -h "${db_host}" -p "${db_port}" -U "${db_user}")
elif [[ -n "${RESET_PG_CONTAINER:-}" ]]; then
  psql_cmd=(docker exec "${RESET_PG_CONTAINER}" psql -U "${db_user}")
else
  echo "no psql client and RESET_PG_CONTAINER is unset; cannot provision ${db_name}" >&2
  exit 1
fi
export RESET_PSQL_CMD="${psql_cmd[*]}"

echo "== preparing a dedicated database"
# Fresh database every run: the smoke asserts on "exactly one queued email".
PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d nerves -c "drop database if exists ${db_name}" >/dev/null
PGPASSWORD="${db_password}" "${psql_cmd[@]}" -d nerves -c "create database ${db_name}" >/dev/null

echo "== building (once)"
dotnet build "${repo_root}/Nerves.slnx" -warnaserror >/dev/null

for port in "${gateway}" "${idp}" "${identity}" "${session}" "${authorization}" "${audit}" "${directory}" "${compliance}" "${notification}"; do
  if ss -ltn 2>/dev/null | grep -q ":${port} "; then
    echo "port ${port} is already in use; choose another RESET_PORT_BASE" >&2
    exit 1
  fi
done

echo "== starting services on base port ${base} (logs: ${log_dir})"
start_service Nerves.Identity "${identity}" "${identity_grpc}"
start_service Nerves.Session "${session}" "${session_grpc}"
start_service Nerves.Authorization "${authorization}" "${authorization_grpc}"
start_service Nerves.Audit "${audit}" "${audit_grpc}"
start_service Nerves.Directory "${directory}" "${directory_grpc}"
start_service Nerves.ComplianceCn "${compliance}"
start_service Nerves.Notification "${notification}"
start_service Nerves.Gateway "${gateway}"
start_service Nerves.Idp "${idp}"

wait_for() {
  local port="$1" name="$2"
  for _ in $(seq 1 90); do
    if curl -fsS -o /dev/null "http://127.0.0.1:${port}/healthz" 2>/dev/null; then
      echo "   ${name} ready on ${port}"
      return 0
    fi
    sleep 1
  done
  echo "   ${name} did not become ready; see ${log_dir}/${name}.log"
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

echo "== obtaining a management token"
RESET_IDP="http://127.0.0.1:${idp}" \
RESET_TOKEN_FILE="${log_dir}/admin-token" \
  python3 "${repo_root}/src/services/Nerves.Identity/scripts/password-reset-token.py"

export RESET_ADMIN_TOKEN="$(cat "${log_dir}/admin-token")"

echo "== restarting identity so it can read the admin token"
# The token is read per request from the file, so no restart is needed; this
# line documents that and keeps the flow explicit.
echo "   ok  identity reads ${log_dir}/admin-token on demand"

echo "== password reset flow"
RESET_IDP="http://127.0.0.1:${idp}" \
RESET_GATEWAY="http://127.0.0.1:${gateway}" \
RESET_NOTIFICATION="http://127.0.0.1:${notification}" \
RESET_ADMIN_TOKEN="${RESET_ADMIN_TOKEN}" \
RESET_LOG_DIR="${log_dir}" \
  python3 "${repo_root}/src/services/Nerves.Identity/scripts/password-reset-flow.py"

echo "== verification provider flow"
RESET_IDP="http://127.0.0.1:${idp}" \
VERIFY_IDENTITY="http://127.0.0.1:${identity}" \
RESET_ADMIN_TOKEN="${RESET_ADMIN_TOKEN}" \
RESET_DB_NAME="${db_name}" \
  python3 "${repo_root}/src/services/Nerves.Identity/scripts/verification-smoke.py"

echo "== reset smoke passed"
echo "logs: ${log_dir}"