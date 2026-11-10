#!/usr/bin/env bash
# Scan a container image for known vulnerabilities with Trivy.
#
# Usage: ./scripts/scan-image.sh <image-ref> [severity]
#   severity defaults to "HIGH,CRITICAL", which is the CI gate.
#
# Environment:
#   TRIVY_CMD             override the trivy binary (default: trivy)
#   TRIVY_IGNORE_UNFIXED  set to 1 to skip findings without a fixed version
#   TRIVY_TIMEOUT         scan timeout (default: 10m)
#   TRIVY_EXIT_CODE       exit code on findings (default: 1, i.e. blocking)
#   TRIVY_IGNOREFILE      reviewed accepted-risk list (CI: deploy/security/trivyignore)
#
# When Trivy is not installed the script prints a clear skip notice and exits 0,
# so local runs without the tool (or without network for the vulnerability DB)
# never fail the caller by accident. CI installs Trivy explicitly, so the gate
# there is blocking.
set -euo pipefail

image="${1:?usage: scan-image.sh <image-ref> [severity]}"
severity="${2:-HIGH,CRITICAL}"
trivy_cmd="${TRIVY_CMD:-trivy}"
exit_code="${TRIVY_EXIT_CODE:-1}"

if ! command -v "${trivy_cmd}" >/dev/null 2>&1; then
  echo "trivy not found (${trivy_cmd}); skipping the image scan for ${image}" >&2
  echo "install: https://trivy.dev/latest/docs/getting-started/installation/" >&2
  exit 0
fi

args=(
  image
  "${image}"
  --scanners vuln
  --severity "${severity}"
  --timeout "${TRIVY_TIMEOUT:-10m}"
  --exit-code "${exit_code}"
  --no-progress
)

if [[ "${TRIVY_IGNORE_UNFIXED:-0}" == "1" ]]; then
  args+=(--ignore-unfixed)
fi

# A project-level trivy.yaml is honoured when present; .trivyignore in the
# working directory is picked up automatically.
if [[ -f trivy.yaml ]]; then
  args+=(--config trivy.yaml)
fi

# The reviewed accepted-risk register. CI points this at
# deploy/security/trivyignore so a finding only blocks when it is new.
if [[ -n "${TRIVY_IGNOREFILE:-}" ]]; then
  if [[ ! -f "${TRIVY_IGNOREFILE}" ]]; then
    echo "TRIVY_IGNOREFILE=${TRIVY_IGNOREFILE} does not exist" >&2
    exit 1
  fi
  args+=(--ignorefile "${TRIVY_IGNOREFILE}")
fi

echo "== scanning ${image} (${severity})"
"${trivy_cmd}" "${args[@]}"
echo "== ${image} passed the vulnerability gate"
