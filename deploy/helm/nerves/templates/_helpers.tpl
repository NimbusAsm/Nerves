{{/*
Common template helpers for the Nerves chart.
*/}}

{{- define "nerves.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "nerves.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "nerves.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Arguments: dict "root" $ "name" <service name>.
*/}}
{{- define "nerves.serviceFullname" -}}
{{- printf "%s-%s" (include "nerves.fullname" .root) .name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "nerves.selectorLabels" -}}
app.kubernetes.io/name: {{ include "nerves.name" .root }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .name }}
{{- end -}}

{{- define "nerves.labels" -}}
helm.sh/chart: {{ include "nerves.chart" .root }}
{{ include "nerves.selectorLabels" . }}
app.kubernetes.io/version: {{ .root.Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .root.Release.Service }}
app.kubernetes.io/part-of: nerves
{{- with .root.Values.commonLabels }}
{{ toYaml . }}
{{- end }}
{{- end -}}

{{- define "nerves.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
{{- default (include "nerves.fullname" .) .Values.serviceAccount.name -}}
{{- else -}}
{{- default "default" .Values.serviceAccount.name -}}
{{- end -}}
{{- end -}}

{{/*
Container image for one service.
Arguments: dict "root" $ "name" <service name> "svc" <service values>.
*/}}
{{- define "nerves.image" -}}
{{- $root := .root -}}
{{- $name := .name -}}
{{- $image := default (dict) .svc.image -}}
{{- $repository := default (printf "%s/%s" $root.Values.image.repository $name) $image.repository -}}
{{- if $image.digest -}}
{{- printf "%s@%s" $repository $image.digest -}}
{{- else -}}
{{- $tag := default (default $root.Chart.AppVersion $root.Values.image.tag) $image.tag -}}
{{- printf "%s:%s" $repository $tag -}}
{{- end -}}
{{- end -}}

{{- define "nerves.pullPolicy" -}}
{{- $image := default (dict) .svc.image -}}
{{- default .root.Values.image.pullPolicy $image.pullPolicy -}}
{{- end -}}

{{- define "nerves.databaseSecretName" -}}
{{- default (printf "%s-database" (include "nerves.fullname" .)) .Values.database.existingSecret -}}
{{- end -}}

{{- define "nerves.natsSecretName" -}}
{{- default (printf "%s-nats" (include "nerves.fullname" .)) .Values.nats.existingSecret -}}
{{- end -}}

{{/*
Random credentials are generated once and then read back from the existing
Secret, so that "helm upgrade" does not rotate them.
*/}}
{{- define "nerves.postgresqlPassword" -}}
{{- $secretName := printf "%s-database" (include "nerves.fullname" .) -}}
{{- $existing := lookup "v1" "Secret" .Release.Namespace $secretName -}}
{{- if .Values.postgresql.auth.password -}}
{{- .Values.postgresql.auth.password -}}
{{- else if and $existing $existing.data (hasKey $existing.data "postgres-password") -}}
{{- index $existing.data "postgres-password" | b64dec -}}
{{- else -}}
{{- randAlphaNum 32 -}}
{{- end -}}
{{- end -}}

{{- define "nerves.natsToken" -}}
{{- $secretName := printf "%s-nats" (include "nerves.fullname" .) -}}
{{- $existing := lookup "v1" "Secret" .Release.Namespace $secretName -}}
{{- if .Values.nats.auth.token -}}
{{- .Values.nats.auth.token -}}
{{- else if and $existing $existing.data (hasKey $existing.data "token") -}}
{{- index $existing.data "token" | b64dec -}}
{{- else -}}
{{- randAlphaNum 32 -}}
{{- end -}}
{{- end -}}

{{/*
Rendered by templates/validations.yaml. Returns an empty string on success and
fails the render with a readable message otherwise.
*/}}
{{- define "nerves.validate" -}}
{{- $state := dict "enabled" 0 -}}
{{- range $name, $svc := .Values.services -}}
{{- if $svc.enabled -}}
{{- $_ := set $state "enabled" (add1 $state.enabled) -}}
{{- $image := default (dict) $svc.image -}}
{{- if not (default $.Values.image.repository $image.repository) -}}
{{- fail (printf "services.%s has no image repository and image.repository is empty" $name) -}}
{{- end -}}
{{- end -}}
{{- end -}}
{{- if eq (int $state.enabled) 0 -}}
{{- fail "at least one service must be enabled" -}}
{{- end -}}

{{- if and .Values.ingress.enabled (not .Values.services.gateway.enabled) -}}
{{- fail "ingress.enabled requires services.gateway.enabled" -}}
{{- end -}}
{{- if .Values.ingress.enabled -}}
{{- if not .Values.ingress.hosts -}}
{{- fail "ingress.enabled requires at least one entry in ingress.hosts" -}}
{{- end -}}
{{- range .Values.ingress.hosts -}}
{{- if not .host -}}
{{- fail "each ingress.hosts entry needs a host" -}}
{{- end -}}
{{- if not .paths -}}
{{- fail (printf "ingress host %q has no paths; set ingress.hosts in a values file instead of --set on the array" .host) -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- if and .Values.database.existingSecret .Values.database.url -}}
{{- fail "set either database.existingSecret or database.url, not both" -}}
{{- end -}}
{{- if and .Values.database.existingSecret .Values.postgresql.enabled -}}
{{- fail "database.existingSecret is for external PostgreSQL: set postgresql.enabled=false" -}}
{{- end -}}
{{- if and .Values.database.url .Values.postgresql.enabled -}}
{{- fail "database.url is for external PostgreSQL: set postgresql.enabled=false" -}}
{{- end -}}
{{- if and (not .Values.database.existingSecret) (not .Values.database.url) (not .Values.postgresql.enabled) -}}
{{- fail "no database configured: enable postgresql, set database.url or database.existingSecret" -}}
{{- end -}}

{{- if and .Values.nats.existingSecret .Values.nats.url -}}
{{- fail "set either nats.existingSecret or nats.url, not both" -}}
{{- end -}}
{{- if and .Values.nats.existingSecret .Values.nats.enabled -}}
{{- fail "nats.existingSecret is for external NATS: set nats.enabled=false" -}}
{{- end -}}
{{- if and .Values.nats.url .Values.nats.enabled -}}
{{- fail "nats.url is for external NATS: set nats.enabled=false" -}}
{{- end -}}

{{- if and .Values.podDisruptionBudget.enabled .Values.podDisruptionBudget.minAvailable (ne (toString .Values.podDisruptionBudget.minAvailable) "") -}}
{{- if ne (toString .Values.podDisruptionBudget.maxUnavailable) "" -}}
{{- fail "set only one of podDisruptionBudget.minAvailable and podDisruptionBudget.maxUnavailable" -}}
{{- end -}}
{{- end -}}
{{- end -}}
