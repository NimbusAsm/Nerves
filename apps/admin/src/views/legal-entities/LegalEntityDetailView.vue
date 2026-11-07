<script setup lang="ts">
import { ShieldCheck } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'

import { ApiError, describeError, getApi } from '@/api'
import type { AccountSummary, LegalEntityDetail } from '@/api/types'
import CursorPager from '@/components/CursorPager.vue'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useCursorList } from '@/composables/useCursorList'
import { useLabels } from '@/composables/useLabels'
import { formatDate, formatDateTime, initials, maskDigest } from '@/lib/format'

const labels = useLabels()
const api = getApi()
const route = useRoute()

const entityId = computed(() => String(route.params.id ?? ''))
const entity = ref<LegalEntityDetail | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const accounts = useCursorList<AccountSummary>(({ limit, cursor }) =>
  api.listLegalEntityAccounts(entityId.value, { limit, cursor }),
)

const hintKey = computed(() => {
  switch (entity.value?.verificationState) {
    case 'verified':
      return 'entities.verifiedHint'
    case 'pending':
      return 'entities.pendingHint'
    case 'rejected':
      return 'entities.rejectedHint'
    case 'expired':
      return 'entities.expiredHint'
    default:
      return 'entities.pendingHint'
  }
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    entity.value = await api.getLegalEntity(entityId.value)
    await accounts.reloadFresh()
  } catch (cause) {
    error.value = cause instanceof ApiError && cause.status === 404 ? String(cause.detail ?? cause.title) : describeError(cause)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader
      :title="entity?.displayName ?? $t('entities.detailTitle')"
      :description="$t('entities.detailDescription')"
      :back-to="{ name: 'legal-entities' }"
      :back-label="$t('entities.backToList')"
    >
      <template #actions>
        <Badge v-if="entity" :tone="labels.verificationTone(entity.verificationState)" size="md">
          {{ labels.verificationState(entity.verificationState) }}
        </Badge>
      </template>
    </PageHeader>

    <div
      v-if="error"
      class="flex flex-wrap items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
      role="alert"
    >
      <span>{{ error }}</span>
      <Button size="sm" @click="load">{{ $t('common.retry') }}</Button>
    </div>

    <div v-else-if="loading" class="rounded-lg border border-line bg-surface p-10 text-center text-sm text-muted">
      {{ $t('common.loading') }}
    </div>

    <template v-else-if="entity">
      <div class="grid gap-4 lg:grid-cols-[1.2fr_1fr]">
        <Card :title="$t('entities.infoTitle')">
          <div class="flex items-center gap-3">
            <span class="grid h-11 w-11 place-items-center rounded-full bg-brand-soft text-sm font-semibold text-brand">
              {{ initials(entity.displayName) }}
            </span>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-ink">{{ entity.displayName }}</p>
              <p class="mono truncate text-muted">{{ entity.id }}</p>
            </div>
          </div>

          <dl class="mt-4 grid grid-cols-1 gap-x-6 gap-y-2.5 text-xs sm:grid-cols-2">
            <div>
              <dt class="text-muted">{{ $t('entities.columnType') }}</dt>
              <dd class="mt-0.5 text-ink">{{ labels.entityType(entity.entityType) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.country') }}</dt>
              <dd class="mt-0.5 text-ink">{{ entity.country }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.documentType') }}</dt>
              <dd class="mt-0.5 text-ink">{{ labels.documentType(entity.idDocType) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.verifiedAt') }}</dt>
              <dd class="mt-0.5 text-ink">{{ formatDateTime(entity.verifiedAt) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.columnCreated') }}</dt>
              <dd class="mt-0.5 text-ink">{{ formatDate(entity.createdAt) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.accountCount') }}</dt>
              <dd class="mt-0.5 text-ink">{{ entity.accountCount }}</dd>
            </div>
            <div class="sm:col-span-2">
              <dt class="text-muted">{{ $t('entities.providerRef') }}</dt>
              <dd class="mono mt-0.5 truncate text-ink">{{ entity.providerRef ?? '—' }}</dd>
            </div>
            <div class="sm:col-span-2">
              <dt class="text-muted">{{ $t('entities.documentDigest') }}</dt>
              <dd class="mono mt-0.5 truncate text-ink">{{ maskDigest(entity.idDocDigest, 24, 8) }}</dd>
            </div>
          </dl>

          <p class="mt-4 border-t border-line pt-3 text-[11px] leading-relaxed text-muted">
            {{ $t('entities.digestHint') }}
          </p>
        </Card>

        <Card :title="$t('entities.verificationTitle')">
          <div class="flex items-start gap-3">
            <span
              class="grid h-10 w-10 shrink-0 place-items-center rounded-full"
              :class="entity.verificationState === 'verified' ? 'bg-emerald-50 text-emerald-600' : entity.verificationState === 'rejected' ? 'bg-red-50 text-red-600' : 'bg-amber-50 text-amber-600'"
            >
              <ShieldCheck class="h-5 w-5" aria-hidden="true" />
            </span>
            <div>
              <Badge :tone="labels.verificationTone(entity.verificationState)">
                {{ labels.verificationState(entity.verificationState) }}
              </Badge>
              <p class="mt-2 text-xs leading-relaxed text-muted">{{ $t(hintKey) }}</p>
            </div>
          </div>

          <ol class="mt-5 space-y-3 border-l border-line pl-4 text-xs">
            <li class="relative">
              <span class="absolute -left-[21px] top-1 h-2 w-2 rounded-full bg-slate-300" aria-hidden="true" />
              <p class="text-ink">{{ $t('status.verification.pending') }}</p>
              <p class="mt-0.5 text-muted">{{ formatDateTime(entity.createdAt) }}</p>
            </li>
            <li v-if="entity.verifiedAt" class="relative">
              <span
                class="absolute -left-[21px] top-1 h-2 w-2 rounded-full"
                :class="entity.verificationState === 'rejected' ? 'bg-red-500' : 'bg-emerald-500'"
                aria-hidden="true"
              />
              <p class="text-ink">{{ labels.verificationState(entity.verificationState) }}</p>
              <p class="mt-0.5 text-muted">{{ formatDateTime(entity.verifiedAt) }}</p>
            </li>
          </ol>
        </Card>
      </div>

      <Card class="mt-4" :title="$t('entities.accountsTitle')" :description="$t('entities.accountsDescription')" :padded="false">
        <Table>
          <thead>
            <tr>
              <th scope="col">{{ $t('accounts.columnAccount') }}</th>
              <th scope="col">{{ $t('accounts.columnType') }}</th>
              <th scope="col">{{ $t('accounts.columnStatus') }}</th>
              <th scope="col">{{ $t('accounts.columnMfa') }}</th>
              <th scope="col">{{ $t('accounts.columnLastLogin') }}</th>
              <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <TableMessage
              :colspan="6"
              :loading="accounts.loading.value"
              :error="accounts.error.value"
              :empty="accounts.isEmpty.value"
              :empty-text="$t('entities.emptyAccounts')"
              @retry="accounts.reloadFresh()"
            />
            <tr v-for="account in accounts.items.value" :key="account.id">
              <td>
                <RouterLink
                  :to="{ name: 'account-detail', params: { id: account.id } }"
                  class="text-sm font-medium text-ink hover:text-brand"
                >
                  {{ account.displayName }}
                </RouterLink>
                <p class="text-[11px] text-muted">{{ account.username }}</p>
              </td>
              <td class="text-xs text-muted">{{ labels.accountType(account.type) }}</td>
              <td>
                <Badge :tone="labels.accountStatusTone(account.status)" size="sm">
                  {{ labels.accountStatus(account.status) }}
                </Badge>
              </td>
              <td class="text-xs" :class="account.mfa ? 'text-emerald-600' : 'text-muted'">
                {{ account.mfa ? $t('common.yes') : $t('common.no') }}
              </td>
              <td class="whitespace-nowrap text-xs text-muted">{{ formatDateTime(account.lastLoginAt) }}</td>
              <td class="text-right">
                <RouterLink
                  :to="{ name: 'account-detail', params: { id: account.id } }"
                  class="inline-block rounded-md border border-line bg-surface px-2 py-1 text-xs text-ink hover:bg-canvas"
                >
                  {{ $t('common.view') }}
                </RouterLink>
              </td>
            </tr>
          </tbody>
        </Table>

        <CursorPager
          :page-index="accounts.pageIndex.value"
          :has-prev="accounts.hasPrev.value"
          :has-next="accounts.hasNext.value"
          :count="accounts.items.value.length"
          :loading="accounts.loading.value"
          @prev="accounts.prev()"
          @next="accounts.next()"
        />
      </Card>
    </template>
  </div>
</template>
