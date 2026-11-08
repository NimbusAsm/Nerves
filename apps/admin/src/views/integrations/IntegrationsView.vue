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

const { t } = useI18n()
const api = getApi()

const integration = ref<IntegrationStatus | null>(null)
const pdp = ref<PdpStatus | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

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

    <div class="grid gap-6 lg:grid-cols-2">
      <Card :title="t('integrations.ldapTitle')">
        <template v-if="integration">
          <div class="mb-3 flex items-center gap-2">
            <Badge :tone="integration.ldap.enabled ? 'success' : 'neutral'" size="sm">
              {{ integration.ldap.enabled ? t('integrations.enabled') : t('integrations.disabled') }}
            </Badge>
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
