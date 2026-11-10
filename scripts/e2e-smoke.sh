#!/usr/bin/env bash
# End-to-end smoke for the whole backend.
#
# Starts every service against a real PostgreSQL and walks the login flow:
#   login API -> OIDC authorize -> token -> gateway management calls.
#
# Environment:
#   E2E_PORT_BASE   first port to use (default 5100); services take base+0..+7
#                   and gRPC listeners base+102..+106
#   E2E_DB_HOST     default 127.0.0.1
#   E2E_DB_PORT     default 55440
#   E2E_DB_USER/PASSWORD/DATABASE  default nerves/nerves/nerves
#   E2E_KEEP=1      leave the services running for debugging
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
log_dir="${E2E_LOG_DIR:-$(mktemp -d)}"
mkdir -p "${log_dir}"

base="${E2E_PORT_BASE:-5100}"
db_host="${E2E_DB_HOST:-127.0.0.1}"
db_port="${E2E_DB_PORT:-55440}"
db_user="${E2E_DB_USER:-nerves}"
db_password="${E2E_DB_PASSWORD:-nerves}"
db_name="${E2E_DB_DATABASE:-nerves}"
connection_string="Host=${db_host};Port=${db_port};Database=${db_name};Username=${db_user};Password=${db_password}"

idp_port=$((base + 1))
identity_port=$((base + 2))
session_port=$((base + 3))
authorization_port=$((base + 4))
audit_port=$((base + 5))
directory_port=$((base + 6))
compliance_port=$((base + 7))
notification_port=$((base + 8))
gateway_port=$((base + 0))

identity_grpc=$((base + 102))
session_grpc=$((base + 103))
authorization_grpc=$((base + 104))
audit_grpc=$((base + 105))
directory_grpc=$((base + 106))

pids=()

cleanup() {
  if [[ "${E2E_KEEP:-0}" == "1" ]]; then
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
  "Authentication__Authority=http://127.0.0.1:${idp_port}"
  "Authentication__Issuer=http://127.0.0.1:${idp_port}/"
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
    )
  fi

  if [[ "${project}" == "Nerves.Gateway" ]]; then
    envs+=(
      "Gateway__Services__identity=http://127.0.0.1:${identity_port}/"
      "Gateway__Services__session=http://127.0.0.1:${session_port}/"
      "Gateway__Services__authorization=http://127.0.0.1:${authorization_port}/"
      "Gateway__Services__audit=http://127.0.0.1:${audit_port}/"
      "Gateway__Services__directory=http://127.0.0.1:${directory_port}/"
      "Gateway__Services__compliance=http://127.0.0.1:${compliance_port}/"
      "Gateway__Services__notification=http://127.0.0.1:${notification_port}/"
      "Gateway__Services__idp=http://127.0.0.1:${idp_port}/"
    )
  fi

  ( cd "${workdir}" && exec env "${envs[@]}" dotnet "${bin}" > "${log_dir}/${project}.log" 2>&1 ) &
  pids+=($!)
}

echo "== building"
dotnet build "${repo_root}/Nerves.slnx" -warnaserror >/dev/null

for port in "${gateway_port}" "${idp_port}" "${identity_port}" "${session_port}" "${authorization_port}"             "${audit_port}" "${directory_port}" "${compliance_port}"; do
  if ss -ltn 2>/dev/null | grep -q ":${port} "; then
    echo "port ${port} is already in use; choose another E2E_PORT_BASE" >&2
    exit 1
  fi
done

echo "== starting services on base port ${base} (logs: ${log_dir})"
start_service Nerves.Identity "${identity_port}" "${identity_grpc}"
start_service Nerves.Session "${session_port}" "${session_grpc}"
start_service Nerves.Authorization "${authorization_port}" "${authorization_grpc}"
start_service Nerves.Audit "${audit_port}" "${audit_grpc}"
start_service Nerves.Directory "${directory_port}" "${directory_grpc}"
start_service Nerves.ComplianceCn "${compliance_port}"
start_service Nerves.Notification "${notification_port}"
start_service Nerves.Gateway "${gateway_port}"
start_service Nerves.Idp "${idp_port}"

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

wait_for "${identity_port}" Identity
wait_for "${session_port}" Session
wait_for "${authorization_port}" Authorization
wait_for "${audit_port}" Audit
wait_for "${directory_port}" Directory
wait_for "${compliance_port}" ComplianceCn
wait_for "${notification_port}" Notification
wait_for "${idp_port}" Idp
wait_for "${gateway_port}" Gateway

echo "== running the login flow"
E2E_IDP="http://127.0.0.1:${idp_port}" \
E2E_GATEWAY="http://127.0.0.1:${gateway_port}" \
E2E_IDENTITY="http://127.0.0.1:${identity_port}" \
E2E_SESSION="http://127.0.0.1:${session_port}" \
E2E_AUTHORIZATION="http://127.0.0.1:${authorization_port}" \
E2E_AUDIT="http://127.0.0.1:${audit_port}" \
E2E_DIRECTORY="http://127.0.0.1:${directory_port}" \
E2E_COMPLIANCE="http://127.0.0.1:${compliance_port}" \
  python3 "${repo_root}/scripts/e2e_smoke.py"

echo "== smoke passed"

# Optional post-smoke probes. Both run while the stack is still up and both are
# report-only: a failing load probe must not turn a green e2e run red.
if [[ -n "${E2E_COLLECT_OPENAPI:-}" ]]; then
  echo "== collecting OpenAPI documents into ${E2E_COLLECT_OPENAPI}"
  "${repo_root}/scripts/collect-openapi.sh" "${E2E_COLLECT_OPENAPI}" || true
fi

if [[ -n "${E2E_LOAD_SMOKE_URL:-}" ]]; then
  echo "== load smoke against ${E2E_LOAD_SMOKE_URL}"
  "${repo_root}/scripts/load-smoke.sh" "${E2E_LOAD_SMOKE_URL}" "${E2E_LOAD_SMOKE_DURATION:-30s}" || true
fi
