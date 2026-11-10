#!/usr/bin/env bash
# Lightweight load smoke: drive a Nerves endpoint for a short burst and report
# latency percentiles and the error rate.
#
# Usage: ./scripts/load-smoke.sh [url] [duration]
#   url       default http://127.0.0.1:5100/healthz
#   duration  default 30s (also LOAD_DURATION)
#
# This is a *report-only* probe: it never fails because a percentile or an error
# rate crossed a threshold. It does fail when the target is unreachable, so a
# broken stack is still visible.
#
# Environment:
#   LOAD_DURATION   k6/wrk duration, default 30s
#   LOAD_VUS        virtual users, default 10
#   LOAD_STRICT=1   exit non-zero on any HTTP error (for ad-hoc checks)
#   K6_CMD, WRK_CMD override the binaries
#
# Preference: k6 (JSON summary, exact p95) -> wrk (latency table) -> skip.
set -euo pipefail

url="${1:-http://127.0.0.1:5100/healthz}"
duration="${2:-${LOAD_DURATION:-30s}}"
vus="${LOAD_VUS:-10}"
k6_cmd="${K6_CMD:-k6}"
wrk_cmd="${WRK_CMD:-wrk}"

# Only the connection is checked here: a 4xx/5xx still proves the stack answers,
# and counting those responses is exactly what LOAD_STRICT is for.
if ! curl -sS -o /dev/null --max-time 10 "${url}"; then
  echo "target ${url} is not reachable; start the stack first (E2E_KEEP=1 bash scripts/e2e-smoke.sh)" >&2
  exit 1
fi

if command -v "${k6_cmd}" >/dev/null 2>&1; then
  echo "== k6 load smoke: ${url} for ${duration} with ${vus} VUs"
  script="$(mktemp)"
  summary="$(mktemp)"
  trap 'rm -f "${script}" "${summary}"' EXIT
  cat > "${script}" <<'EOF'
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: Number(__ENV.LOAD_VUS || 10),
  duration: __ENV.LOAD_DURATION || '30s',
};

export default function () {
  const res = http.get(__ENV.LOAD_URL);
  check(res, { 'status is below 400': (r) => r.status < 400 });
}

// handleSummary is supported by every k6 release we pin in CI, unlike the
// deprecated --summary-export flag.
export function handleSummary(data) {
  return { [__ENV.LOAD_SUMMARY]: JSON.stringify(data) };
}
EOF
  LOAD_URL="${url}" LOAD_VUS="${vus}" LOAD_DURATION="${duration}" LOAD_SUMMARY="${summary}" \
    "${k6_cmd}" run --quiet "${script}" || true
  python3 - "${summary}" "${url}" "${duration}" "${vus}" <<'PYCODE'
import json, os, sys

path, url, duration, vus = sys.argv[1:5]
try:
    data = json.load(open(path))
except (OSError, json.JSONDecodeError):
    print(f"load smoke: no k6 summary produced for {url}")
    raise SystemExit(0)

metrics = data.get("metrics", {})


def metric(name):
    return metrics.get(name, {}).get("values", {})


latency = metric("http_req_duration")
failed = metric("http_req_failed")
requests = metric("http_reqs")

p95 = latency.get("p(95)")
rate = failed.get("rate")
count = requests.get("count")


def fmt(value, unit, digits=2):
    return "n/a" if value is None else f"{value:.{digits}f}{unit}"


print("load smoke report")
print(f"  target        {url}")
print(f"  duration      {duration} @ {vus} VUs")
print(f"  requests      {count if count is not None else 'n/a'}")
print(f"  p95 latency   {fmt(p95, ' ms')}")
print(f"  avg latency   {fmt(latency.get('avg'), ' ms')}")
print(f"  error rate    {'n/a' if rate is None else f'{rate * 100:.2f}%'}")

if rate is not None and rate > 0 and os.environ.get("LOAD_STRICT") == "1":
    print("load smoke: LOAD_STRICT=1 and the run observed HTTP errors")
    raise SystemExit(1)
PYCODE
  exit 0
fi

if command -v "${wrk_cmd}" >/dev/null 2>&1; then
  echo "== wrk load smoke: ${url} for ${duration} (10 connections)"
  threads="$(nproc 2>/dev/null || echo 2)"
  "${wrk_cmd}" -t "${threads}" -c 10 -d "${duration}" --latency "${url}" || true
  echo
  echo "load smoke report (wrk): see the latency distribution above."
  echo "k6 is preferred because it reports an exact p95 and the HTTP error rate."
  exit 0
fi

cat >&2 <<'EOF'
no load tool found; skipping the load smoke.

install one of:
  k6   https://grafana.com/docs/k6/latest/set-up/install-k6/
  wrk  https://github.com/wg/wrk#installation
EOF
exit 0
