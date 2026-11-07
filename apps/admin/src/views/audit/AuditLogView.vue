<script setup lang="ts">
import { Download, Search } from '@lucide/vue'
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

import { describeError, getApi } from '@/api'
import type { AuditEvent, AuditResult } from '@/api/types'
import CursorPager from '@/components/CursorPager.vue'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Select from '@/components/ui/Select.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useCursorList } from '@/composables/useCursorList'
import { useLabels } from '@/composables/useLabels'
import { csvFilename, downloadCsv, toCsv } from '@/lib/csv'
import { formatDateTime } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()
const route = useRoute()

interface Filters {
  from: string
  to: string
  actor: string
  result: AuditResult | ''
  action: string
}

const draft = reactive<Filters>({ from: '', to: '', actor: '', result: '', action: '' })
const applied = ref<Filters>({ from: '', to: '', actor: '', result: '', action: '' })

const list = useCursorList<AuditEvent>(({ limit, cursor }) => {
  const filters = applied.value
  return api.listAuditEvents({
    limit,
    cursor,
    from: filters.from || undefined,
    to: filters.to || undefined,
    actor: filters.actor.trim() || undefined,
    result: filters.result,
    action: filters.action.trim() || undefined,
  })
})

function apply(): void {
  applied.value = { ...draft }
  void list.reloadFresh()
}

function reset(): void {
  draft.from = ''
  draft.to = ''
  draft.actor = ''
  draft.result = ''
  draft.action = ''
  apply()
}

onMounted(() => {
  const query = route.query
  if (typeof query.actor === 'string') draft.actor = query.actor
  if (typeof query.action === 'string') draft.action = query.action
  if (typeof query.from === 'string') draft.from = query.from
  if (typeof query.to === 'string') draft.to = query.to
  apply()
})

const exporting = ref(false)
const canExport = computed(() => auth.can('audit:event:export'))

async function exportCsv(): Promise<void> {
  exporting.value = true
  try {
    const collected: AuditEvent[] = []
    let cursor: string | null = null
    let pages = 0
    do {
      const page = await api.listAuditEvents({ ...applied.value, limit: 100, cursor })
      collected.push(...page.items)
      cursor = page.nextCursor
      pages += 1
    } while (cursor && collected.length < 2000 && pages < 25)

    if (collected.length === 0) {
      toasts.info(t('audit.exportEmpty'))
      return
    }

    const header = [
      t('audit.columnTime'),
      t('audit.columnAction'),
      t('audit.columnActor'),
      t('common.type'),
      t('audit.columnTarget'),
      t('common.target'),
      t('audit.columnResult'),
      t('common.ipDigest'),
      t('audit.columnTrace'),
      t('audit.columnDetail'),
    ]
    const rows = collected.map((event) => [
      formatDateTime(event.at),
      event.action,
      event.actorName,
      event.actorType,
      event.targetLabel,
      event.targetType,
      labels.auditResult(event.result),
      event.ipDigest,
      event.traceId,
      event.detail,
    ])
    downloadCsv(csvFilename('nerves-audit'), toCsv([header, ...rows]))
    toasts.success(t('audit.exportDone', { count: collected.length }))
  } catch (cause) {
    toasts.error(t('toast.error'), describeError(cause))
  } finally {
    exporting.value = false
  }
}

const activeEvent = ref<AuditEvent | null>(null)
</script>

<template>
  <div>
    <PageHeader :title="$t('audit.title')" :description="$t('audit.description')">
      <template #actions>
        <Button :loading="exporting" :disabled="!canExport" @click="exportCsv">
          <Download class="h-4 w-4" aria-hidden="true" />
          {{ $t('audit.export') }}
        </Button>
      </template>
    </PageHeader>

    <Card :padded="true" class="mb-4">
      <form class="grid gap-3 sm:grid-cols-2 xl:grid-cols-5" @submit.prevent="apply">
        <div>
          <label for="audit-from" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('audit.from') }}</label>
          <Input id="audit-from" v-model="draft.from" type="date" />
        </div>
        <div>
          <label for="audit-to" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('audit.to') }}</label>
          <Input id="audit-to" v-model="draft.to" type="date" />
        </div>
        <div>
          <label for="audit-actor" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('audit.actor') }}</label>
          <Input id="audit-actor" v-model="draft.actor" :placeholder="$t('audit.actorPlaceholder')" />
        </div>
        <div>
          <label for="audit-result" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('audit.result') }}</label>
          <Select id="audit-result" v-model="draft.result">
            <option value="">{{ $t('audit.allResults') }}</option>
            <option value="success">{{ $t('status.result.success') }}</option>
            <option value="failure">{{ $t('status.result.failure') }}</option>
            <option value="denied">{{ $t('status.result.denied') }}</option>
          </Select>
        </div>
        <div>
          <label for="audit-action" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('audit.action') }}</label>
          <Input id="audit-action" v-model="draft.action" :placeholder="$t('audit.actionPlaceholder')" />
        </div>

        <div class="flex items-center gap-2 sm:col-span-2 xl:col-span-5">
          <Button type="submit" variant="primary">
            <Search class="h-4 w-4" aria-hidden="true" />
            {{ $t('audit.apply') }}
          </Button>
          <Button type="button" variant="ghost" @click="reset">{{ $t('audit.reset') }}</Button>
        </div>
      </form>
    </Card>

    <Card :padded="false">
      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('audit.columnTime') }}</th>
            <th scope="col">{{ $t('audit.columnAction') }}</th>
            <th scope="col">{{ $t('audit.columnActor') }}</th>
            <th scope="col">{{ $t('audit.columnTarget') }}</th>
            <th scope="col">{{ $t('audit.columnResult') }}</th>
            <th scope="col">{{ $t('audit.columnDetail') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="7"
            :loading="list.loading.value"
            :error="list.error.value"
            :empty="list.isEmpty.value"
            :empty-text="$t('audit.empty')"
            @retry="list.reloadFresh()"
          />
          <tr v-for="event in list.items.value" :key="event.id">
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDateTime(event.at) }}</td>
            <td class="whitespace-nowrap text-xs text-ink">{{ labels.auditAction(event.action) }}</td>
            <td class="text-xs text-muted">{{ event.actorName }}</td>
            <td class="max-w-[220px] truncate text-xs text-muted">{{ event.targetLabel }}</td>
            <td>
              <Badge :tone="labels.auditResultTone(event.result)" size="sm">{{ labels.auditResult(event.result) }}</Badge>
            </td>
            <td class="max-w-[280px] truncate text-xs text-muted" :title="event.detail">{{ event.detail }}</td>
            <td class="text-right">
              <Button size="sm" variant="ghost" @click="activeEvent = event">{{ $t('common.view') }}</Button>
            </td>
          </tr>
        </tbody>
      </Table>

      <CursorPager
        :page-index="list.pageIndex.value"
        :has-prev="list.hasPrev.value"
        :has-next="list.hasNext.value"
        :count="list.items.value.length"
        :loading="list.loading.value"
        @prev="list.prev()"
        @next="list.next()"
      />
    </Card>

    <Modal
      :open="activeEvent !== null"
      :title="$t('audit.eventTitle')"
      size="lg"
      @update:open="(value) => !value && (activeEvent = null)"
    >
      <dl v-if="activeEvent" class="grid gap-x-6 gap-y-3 text-xs sm:grid-cols-2">
        <div>
          <dt class="text-muted">{{ $t('audit.columnTime') }}</dt>
          <dd class="mt-0.5 mono text-ink">{{ formatDateTime(activeEvent.at) }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.columnResult') }}</dt>
          <dd class="mt-0.5">
            <Badge :tone="labels.auditResultTone(activeEvent.result)" size="sm">{{ labels.auditResult(activeEvent.result) }}</Badge>
          </dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.columnAction') }}</dt>
          <dd class="mt-0.5 text-ink">{{ labels.auditAction(activeEvent.action) }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('common.code') }}</dt>
          <dd class="mono mt-0.5 text-ink">{{ activeEvent.action }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.columnActor') }}</dt>
          <dd class="mt-0.5 text-ink">{{ activeEvent.actorName }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('common.actor') }} ID</dt>
          <dd class="mono mt-0.5 text-ink">{{ activeEvent.actorId }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.columnTarget') }}</dt>
          <dd class="mt-0.5 text-ink">{{ activeEvent.targetLabel }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.targetType') }}</dt>
          <dd class="mono mt-0.5 text-ink">{{ activeEvent.targetType }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('common.ipDigest') }}</dt>
          <dd class="mono mt-0.5 break-all text-ink">{{ activeEvent.ipDigest }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('audit.columnTrace') }}</dt>
          <dd class="mono mt-0.5 break-all text-ink">{{ activeEvent.traceId }}</dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('audit.columnDetail') }}</dt>
          <dd class="mt-0.5 leading-relaxed text-ink">{{ activeEvent.detail }}</dd>
        </div>
      </dl>
      <template #footer>
        <Button @click="activeEvent = null">{{ $t('common.close') }}</Button>
      </template>
    </Modal>
  </div>
</template>
