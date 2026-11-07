<script setup lang="ts">
import { ChartColumn, Clock, ScrollText, TrendingUp, Users } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'

import { describeError, getApi } from '@/api'
import type { AuditEvent, DashboardStats } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useLabels } from '@/composables/useLabels'
import { formatDateTime, formatNumber, formatPercent } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'

const labels = useLabels()
const auth = useAuthStore()
const api = getApi()

const stats = ref<DashboardStats | null>(null)
const recent = ref<AuditEvent[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const trend = computed(() => stats.value?.loginTrend ?? [])
const trendMax = computed(() => Math.max(1, ...trend.value.map((point) => point.success + point.failed)))
const hasTrend = computed(() => trend.value.some((point) => point.success + point.failed > 0))

const statusRows = computed(() => {
  const value = stats.value
  if (!value) return []
  return [
    { key: 'dashboard.accountsActive', count: value.activeAccounts, color: 'bg-emerald-500' },
    { key: 'dashboard.accountsLocked', count: value.lockedAccounts, color: 'bg-amber-500' },
    { key: 'dashboard.accountsDisabled', count: value.disabledAccounts, color: 'bg-slate-400' },
  ]
})

const verificationRows = computed(() => {
  const value = stats.value
  if (!value) return []
  return [
    { key: 'dashboard.entitiesVerified', count: value.verifiedEntities, color: 'bg-emerald-500' },
    { key: 'dashboard.entitiesPending', count: value.pendingVerifications, color: 'bg-amber-500' },
  ]
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    stats.value = await api.getStats()
    if (auth.can('audit:event:read')) {
      recent.value = (await api.listAuditEvents({ limit: 6 })).items
    }
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
    <PageHeader :title="$t('dashboard.title')" :description="$t('dashboard.description')">
      <template #actions>
        <Button :loading="loading" @click="load">
          <TrendingUp class="h-4 w-4" aria-hidden="true" />
          {{ $t('common.refresh') }}
        </Button>
      </template>
    </PageHeader>

    <div
      v-if="error"
      class="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
      role="alert"
    >
      <span>{{ error }}</span>
      <Button size="sm" @click="load">{{ $t('common.retry') }}</Button>
    </div>

    <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <div class="rounded-lg border border-line bg-surface p-4 shadow-xs">
        <div class="flex items-center justify-between">
          <p class="text-xs font-medium text-muted">{{ $t('dashboard.totalAccounts') }}</p>
          <Users class="h-4 w-4 text-brand" aria-hidden="true" />
        </div>
        <p class="mt-2 text-2xl font-semibold tabular-nums text-ink">
          {{ stats ? formatNumber(stats.totalAccounts) : '—' }}
        </p>
        <p class="mt-1 text-[11px] text-muted">{{ $t('dashboard.totalAccountsHint') }}</p>
      </div>

      <div class="rounded-lg border border-line bg-surface p-4 shadow-xs">
        <div class="flex items-center justify-between">
          <p class="text-xs font-medium text-muted">{{ $t('dashboard.todayLogins') }}</p>
          <TrendingUp class="h-4 w-4 text-emerald-600" aria-hidden="true" />
        </div>
        <p class="mt-2 text-2xl font-semibold tabular-nums text-ink">
          {{ stats ? formatNumber(stats.todayLogins) : '—' }}
        </p>
        <p class="mt-1 text-[11px] text-muted">{{ $t('dashboard.todayLoginsHint') }}</p>
      </div>

      <div class="rounded-lg border border-line bg-surface p-4 shadow-xs">
        <div class="flex items-center justify-between">
          <p class="text-xs font-medium text-muted">{{ $t('dashboard.todayFailedLogins') }}</p>
          <ChartColumn class="h-4 w-4 text-red-500" aria-hidden="true" />
        </div>
        <p class="mt-2 text-2xl font-semibold tabular-nums text-ink">
          {{ stats ? formatNumber(stats.todayFailedLogins) : '—' }}
        </p>
        <p class="mt-1 text-[11px] text-muted">{{ $t('dashboard.todayFailedLoginsHint') }}</p>
      </div>

      <div class="rounded-lg border border-line bg-surface p-4 shadow-xs">
        <div class="flex items-center justify-between">
          <p class="text-xs font-medium text-muted">{{ $t('dashboard.activeSessions') }}</p>
          <Clock class="h-4 w-4 text-sky-600" aria-hidden="true" />
        </div>
        <p class="mt-2 text-2xl font-semibold tabular-nums text-ink">
          {{ stats ? formatNumber(stats.activeSessions) : '—' }}
        </p>
        <p class="mt-1 text-[11px] text-muted">{{ $t('dashboard.activeSessionsHint') }}</p>
      </div>
    </div>

    <div class="mt-4 grid gap-4 lg:grid-cols-3">
      <Card class="lg:col-span-2" :title="$t('dashboard.trendTitle')" :description="$t('dashboard.trendDescription')">
        <div class="flex items-center gap-4 text-[11px] text-muted">
          <span class="inline-flex items-center gap-1.5"><span class="h-2 w-2 rounded-full bg-brand" />{{ $t('dashboard.success') }}</span>
          <span class="inline-flex items-center gap-1.5"><span class="h-2 w-2 rounded-full bg-red-400" />{{ $t('dashboard.failed') }}</span>
        </div>

        <div v-if="!hasTrend" class="flex h-44 items-center justify-center text-sm text-muted">
          {{ $t('dashboard.trendEmpty') }}
        </div>
        <div v-else class="mt-4 flex h-44 items-end gap-2 sm:gap-3">
          <div v-for="point in trend" :key="point.date" class="flex min-w-0 flex-1 flex-col items-center gap-1">
            <div
              class="flex h-36 w-full flex-col justify-end gap-0.5"
              :title="`${point.date} · ${$t('dashboard.success')} ${point.success} · ${$t('dashboard.failed')} ${point.failed}`"
            >
              <div
                class="w-full rounded-t bg-red-400 transition-all"
                :style="{ height: `${(point.failed / trendMax) * 100}%` }"
              />
              <div
                class="w-full bg-brand transition-all"
                :class="point.failed === 0 ? 'rounded-t' : ''"
                :style="{ height: `${(point.success / trendMax) * 100}%` }"
              />
            </div>
            <span class="text-[10px] text-muted">{{ point.date.slice(5) }}</span>
          </div>
        </div>
      </Card>

      <Card :title="$t('dashboard.statusTitle')" :description="$t('dashboard.statusDescription')">
        <ul class="space-y-3.5">
          <li v-for="row in statusRows" :key="row.key">
            <div class="flex items-center justify-between text-xs">
              <span class="text-ink">{{ $t(row.key) }}</span>
              <span class="tabular-nums text-muted">
                {{ formatNumber(row.count) }}
                <span class="text-[10px]">· {{ formatPercent(row.count, stats?.totalAccounts ?? 0) }}</span>
              </span>
            </div>
            <div class="mt-1.5 h-1.5 overflow-hidden rounded-full bg-canvas">
              <div
                class="h-full rounded-full"
                :class="row.color"
                :style="{ width: formatPercent(row.count, stats?.totalAccounts ?? 0) }"
              />
            </div>
          </li>
        </ul>

        <div class="mt-5 border-t border-line pt-4">
          <p class="text-xs font-medium text-ink">{{ $t('dashboard.verificationTitle') }}</p>
          <ul class="mt-3 space-y-2">
            <li v-for="row in verificationRows" :key="row.key" class="flex items-center justify-between text-xs">
              <span class="inline-flex items-center gap-1.5 text-muted">
                <span class="h-1.5 w-1.5 rounded-full" :class="row.color" />
                {{ $t(row.key) }}
              </span>
              <span class="tabular-nums text-ink">{{ formatNumber(row.count) }}</span>
            </li>
          </ul>
          <div class="mt-4 flex items-center justify-between border-t border-line pt-3 text-[11px] text-muted">
            <span>{{ $t('dashboard.totalOrgs') }} · {{ stats ? stats.totalOrganizations : '—' }}</span>
            <span>{{ $t('dashboard.totalApps') }} · {{ stats ? stats.totalApplications : '—' }}</span>
          </div>
        </div>
      </Card>
    </div>

    <Card
      v-if="auth.can('audit:event:read')"
      class="mt-4"
      :title="$t('dashboard.recentTitle')"
      :description="$t('dashboard.recentDescription')"
      :padded="false"
    >
      <template #actions>
        <RouterLink :to="{ name: 'audit' }" class="inline-flex items-center gap-1 text-xs text-brand hover:underline">
          <ScrollText class="h-3.5 w-3.5" aria-hidden="true" />
          {{ $t('dashboard.viewAll') }}
        </RouterLink>
      </template>

      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('audit.columnTime') }}</th>
            <th scope="col">{{ $t('audit.columnAction') }}</th>
            <th scope="col">{{ $t('audit.columnActor') }}</th>
            <th scope="col">{{ $t('audit.columnTarget') }}</th>
            <th scope="col">{{ $t('audit.columnResult') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage :colspan="5" :loading="loading" :error="error" :empty="recent.length === 0" :empty-text="$t('audit.empty')" @retry="load" />
          <tr v-for="event in recent" :key="event.id">
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDateTime(event.at) }}</td>
            <td class="text-xs text-ink">{{ labels.auditAction(event.action) }}</td>
            <td class="text-xs text-muted">{{ event.actorName }}</td>
            <td class="text-xs text-muted">{{ event.targetLabel }}</td>
            <td>
              <Badge :tone="labels.auditResultTone(event.result)" size="sm">{{ labels.auditResult(event.result) }}</Badge>
            </td>
          </tr>
        </tbody>
      </Table>
    </Card>
  </div>
</template>
