<script setup lang="ts">
import { Plug, RefreshCw, ShieldCheck } from '@lucide/vue'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { IntegrationStatus, PdpStatus } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'

const { t } = useI18n()
const api = getApi()

const integration = ref<IntegrationStatus | null>(null)
const pdp = ref<PdpStatus | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const editing = ref<'ldap' | 'pdp' | null>(null)
const saving = ref(false)
const ldapForm = ref({ enabled: false, host: '', port: '389', baseDn: '', userFilter: '', timeoutSeconds: '5' })
const pdpForm = ref({ enabled: false, url: '', timeoutSeconds: '3', failOpen: false })

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [integrations, pdpStatus] = await Promise.all([
      api.getIntegrationStatus(),
      api.getPdpStatus(),
    ])
    integration.value = integrations
    pdp.value = pdpStatus
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    loading.value = false
  }
}

function editLdap(): void {
  if (!integration.value) return
  editing.value = 'ldap'
  ldapForm.value = {
    enabled: integration.value.ldap.enabled,
    host: integration.value.ldap.host,
    port: String(integration.value.ldap.port),
    baseDn: integration.value.ldap.baseDn,
    userFilter: integration.value.ldap.userFilter,
    timeoutSeconds: String(integration.value.ldap.timeoutSeconds),
  }
}

function editPdp(): void {
  if (!pdp.value) return
  editing.value = 'pdp'
  pdpForm.value = {
    enabled: pdp.value.enabled,
    url: pdp.value.url ?? '',
    timeoutSeconds: String(pdp.value.timeoutSeconds),
    failOpen: pdp.value.failOpen,
  }
}

async function save(): Promise<void> {
  saving.value = true
  try {
    if (editing.value === 'ldap') {
      await api.updateLdapSettings({
        ...ldapForm.value,
        port: Number(ldapForm.value.port),
        timeoutSeconds: Number(ldapForm.value.timeoutSeconds),
      })
    } else if (editing.value === 'pdp') {
      await api.updatePdpSettings({
        ...pdpForm.value,
        timeoutSeconds: Number(pdpForm.value.timeoutSeconds),
        url: pdpForm.value.url || null,
      })
    }
    editing.value = null
    await load()
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader :title="t('integrations.title')" :description="t('integrations.description')">
      <Button variant="ghost" :loading="loading" @click="load">
        <RefreshCw class="mr-1 size-4" />{{ t('common.refresh') }}
      </Button>
    </PageHeader>

    <p v-if="error" class="mb-4 rounded-md bg-red-50 px-3 py-2 text-xs text-red-700">{{ error }}</p>

    <form v-if="editing" class="mb-6 rounded-lg border border-line bg-white p-4" @submit.prevent="save">
      <h3 class="mb-3 text-sm font-semibold text-ink">
        {{ editing === 'ldap' ? t('integrations.ldapTitle') : t('integrations.pdpTitle') }}
      </h3>
      <div v-if="editing === 'ldap'" class="grid gap-3 sm:grid-cols-2">
        <label class="flex items-center gap-2 text-xs text-ink">
          <input v-model="ldapForm.enabled" type="checkbox" />{{ t('integrations.enabled') }}
        </label>
        <Input v-model="ldapForm.host" :label="t('integrations.host')" />
        <Input v-model.number="ldapForm.port" :label="t('integrations.port')" type="number" />
        <Input v-model="ldapForm.baseDn" :label="t('integrations.baseDn')" />
        <Input v-model="ldapForm.userFilter" :label="t('integrations.userFilter')" />
        <Input v-model.number="ldapForm.timeoutSeconds" :label="t('integrations.timeout')" type="number" />
      </div>
      <div v-else class="grid gap-3 sm:grid-cols-2">
        <label class="flex items-center gap-2 text-xs text-ink">
          <input v-model="pdpForm.enabled" type="checkbox" />{{ t('integrations.enabled') }}
        </label>
        <Input v-model="pdpForm.url" :label="t('integrations.endpoint')" />
        <Input v-model.number="pdpForm.timeoutSeconds" :label="t('integrations.timeout')" type="number" />
        <label class="flex items-center gap-2 text-xs text-ink">
          <input v-model="pdpForm.failOpen" type="checkbox" />{{ t('integrations.failOpen') }}
        </label>
      </div>
      <div class="mt-4 flex gap-2">
        <Button :loading="saving" type="submit">{{ t('common.save') }}</Button>
        <Button variant="ghost" type="button" @click="editing = null">{{ t('common.cancel') }}</Button>
      </div>
      <p class="mt-2 text-[11px] text-muted">{{ t('integrations.runtimeNote') }}</p>
    </form>

    <div class="grid gap-6 lg:grid-cols-2">
      <Card :title="t('integrations.ldapTitle')">
        <template v-if="integration">
          <div class="mb-3 flex items-center gap-2">
            <Badge :tone="integration.ldap.enabled ? 'success' : 'neutral'" size="sm">
              {{ integration.ldap.enabled ? t('integrations.enabled') : t('integrations.disabled') }}
            </Badge>
            <span class="flex-1" />
            <Button size="sm" variant="ghost" @click="editLdap">{{ t('common.edit') }}</Button>
          </div>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-xs">
            <dt class="text-muted">{{ t('integrations.host') }}</dt>
            <dd class="mono text-ink">{{ integration.ldap.host }}:{{ integration.ldap.port }}</dd>
            <dt class="text-muted">{{ t('integrations.baseDn') }}</dt>
            <dd class="mono text-ink">{{ integration.ldap.baseDn }}</dd>
            <dt class="text-muted">{{ t('integrations.userFilter') }}</dt>
            <dd class="mono text-ink">{{ integration.ldap.userFilter }}</dd>
            <dt class="text-muted">{{ t('integrations.serviceAccount') }}</dt>
            <dd class="text-ink">{{ integration.ldap.serviceAccount ? t('common.yes') : t('common.no') }}</dd>
          </dl>
        </template>
      </Card>

      <Card :title="t('integrations.federationTitle')">
        <ul v-if="integration" class="divide-y divide-line">
          <li v-for="provider in integration.federation" :key="provider.id" class="py-2 first:pt-0 last:pb-0">
            <div class="flex items-center gap-2">
              <Plug class="size-4 text-muted" />
              <p class="text-xs font-medium text-ink">{{ provider.displayName }}</p>
            </div>
            <p class="mono mt-1 text-[11px] text-muted">{{ provider.authority }}</p>
            <p class="mono text-[11px] text-muted">{{ provider.clientId }}</p>
          </li>
          <li v-if="integration.federation.length === 0" class="py-2 text-xs text-muted">
            {{ t('integrations.noProviders') }}
          </li>
        </ul>
      </Card>

      <Card :title="t('integrations.pdpTitle')">
        <template v-if="pdp">
          <div class="mb-3 flex items-center gap-2">
            <Badge :tone="pdp.enabled ? 'success' : 'neutral'" size="sm">
              {{ pdp.enabled ? t('integrations.enabled') : t('integrations.disabled') }}
            </Badge>
            <Badge :tone="pdp.failOpen ? 'warning' : 'success'" size="sm">
              {{ pdp.failOpen ? t('integrations.failOpen') : t('integrations.failClosed') }}
            </Badge>
            <span class="flex-1" />
            <Button size="sm" variant="ghost" @click="editPdp">{{ t('common.edit') }}</Button>
          </div>
          <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-xs">
            <dt class="text-muted">{{ t('integrations.endpoint') }}</dt>
            <dd class="mono break-all text-ink">{{ pdp.url ?? '—' }}</dd>
            <dt class="text-muted">{{ t('integrations.timeout') }}</dt>
            <dd class="text-ink">{{ pdp.timeoutSeconds }}s</dd>
            <dt class="text-muted">{{ t('integrations.token') }}</dt>
            <dd class="text-ink">{{ pdp.tokenConfigured ? t('common.yes') : t('common.no') }}</dd>
          </dl>
        </template>
      </Card>

      <Card :title="t('integrations.notesTitle')">
        <div class="flex items-start gap-2 text-[11px] leading-relaxed text-muted">
          <ShieldCheck class="mt-0.5 size-4 shrink-0 text-emerald-600" />
          <p>{{ t('integrations.notes') }}</p>
        </div>
      </Card>
    </div>
  </div>
</template>
