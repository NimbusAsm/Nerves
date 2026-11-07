#!/usr/bin/env bash
# Validate the Nerves chart without a Kubernetes cluster.
#
#   ./scripts/validate.sh
#
# Requires: helm (3.10+ or 4.x). Optional: kubeconform.
set -euo pipefail

chart_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
helm_bin="${HELM:-helm}"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "${tmp_dir}"' EXIT

echo "== helm lint (${chart_dir})"
"${helm_bin}" lint --strict "${chart_dir}"

render() {
  local name="$1"
  shift
  echo "== render: ${name}"
  "${helm_bin}" template nerves "${chart_dir}" --namespace nerves "$@" > "${tmp_dir}/${name}.yaml"
  if command -v kubeconform >/dev/null 2>&1; then
    kubeconform -strict -summary -ignore-missing-schemas "${tmp_dir}/${name}.yaml"
  fi
}

render default
render external \
  --set postgresql.enabled=false \
  --set 'database.url=Host=postgres.example.internal;Port=5432;Database=nerves;Username=nerves;Password=change-me' \
  --set nats.enabled=false \
  --set 'nats.url=nats://nats.example.internal:4222'
render existing-secrets \
  --set postgresql.enabled=false \
  --set database.existingSecret=nerves-database \
  --set nats.enabled=false \
  --set nats.existingSecret=nerves-nats
render ingress-autoscaling-pdb \
  --set ingress.enabled=true \
  --set 'ingress.hosts[0].host=nerves.example.com' \
  --set 'ingress.hosts[0].paths[0].path=/' \
  --set 'ingress.hosts[0].paths[0].pathType=Prefix' \
  --set autoscaling.enabled=true \
  --set podDisruptionBudget.enabled=true
render no-persistence \
  --set postgresql.persistence.enabled=false \
  --set nats.persistence.enabled=false

echo "== validating that conflicting database/NATS settings fail"
if "${helm_bin}" template nerves "${chart_dir}" \
  --set postgresql.enabled=true \
  --set 'database.url=Host=db' >/dev/null 2>&1; then
  echo "expected the render to fail when database.url and postgresql are both enabled" >&2
  exit 1
fi
if "${helm_bin}" template nerves "${chart_dir}" \
  --set nats.enabled=true \
  --set 'nats.url=nats://nats' >/dev/null 2>&1; then
  echo "expected the render to fail when nats.url and nats.enabled are both set" >&2
  exit 1
fi

echo "OK: chart lint, templates and schema validation passed"
