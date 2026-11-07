<script setup lang="ts">
import { Search } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'

import { getApi } from '@/api'
import type { LegalEntitySummary, VerificationState } from '@/api/types'
import CursorPager from '@/components/CursorPager.vue'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Select from '@/components/ui/Select.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useCursorList } from '@/composables/useCursorList'
import { useLabels } from '@/composables/useLabels'
import { debounce } from '@/lib/debounce'
import { formatDate } from '@/lib/format'

const labels = useLabels()
const api = getApi()

const search = ref('')
const state = ref<VerificationState | ''>('')

const list = useCursorList<LegalEntitySummary>(({ limit, cursor }) =>
  api.listLegalEntities({
    limit,
    cursor,
    q: search.value.trim() || undefined,
    verificationState: state.value,
  }),
)

watch([search, state], debounce(() => void list.reloadFresh(), 250))
onMounted(() => void list.reloadFresh())

const hasFilters = computed(() => search.value !== '' || state.value !== '')
function resetFilters(): void {
  search.value = ''
  state.value = ''
}
</script>

<template>
  <div>
    <PageHeader :title="$t('entities.title')" :description="$t('entities.description')">
      <template #actions>
        <Button :loading="list.loading.value" @click="list.reload()">{{ $t('common.refresh') }}</Button>
      </template>
    </PageHeader>

    <div class="rounded-lg border border-line bg-surface shadow-xs">
      <div class="flex flex-col gap-3 border-b border-line p-4 sm:flex-row sm:items-center">
        <div class="relative min-w-0 flex-1">
          <Search class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <Input v-model="search" class="pl-8" type="search" :placeholder="$t('entities.searchPlaceholder')" :aria-label="$t('common.search')" />
        </div>
        <div class="flex items-center gap-2">
          <Select v-model="state" class="w-40" :aria-label="$t('entities.stateFilter')">
            <option value="">{{ $t('entities.allStates') }}</option>
            <option value="pending">{{ $t('status.verification.pending') }}</option>
            <option value="verified">{{ $t('status.verification.verified') }}</option>
            <option value="rejected">{{ $t('status.verification.rejected') }}</option>
            <option value="expired">{{ $t('status.verification.expired') }}</option>
          </Select>
          <Button v-if="hasFilters" variant="ghost" size="sm" @click="resetFilters">{{ $t('common.reset') }}</Button>
        </div>
      </div>

      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('entities.columnEntity') }}</th>
            <th scope="col">{{ $t('entities.columnType') }}</th>
            <th scope="col">{{ $t('entities.columnDocument') }}</th>
            <th scope="col">{{ $t('entities.columnState') }}</th>
            <th scope="col">{{ $t('entities.columnAccounts') }}</th>
            <th scope="col">{{ $t('entities.columnCreated') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="7"
            :loading="list.loading.value"
            :error="list.error.value"
            :empty="list.isEmpty.value"
            :empty-text="$t('entities.empty')"
            @retry="list.reloadFresh()"
          />
          <tr v-for="entity in list.items.value" :key="entity.id">
            <td>
              <RouterLink
                :to="{ name: 'legal-entity-detail', params: { id: entity.id } }"
                class="block max-w-[320px] truncate text-sm font-medium text-ink hover:text-brand"
              >
                {{ entity.displayName }}
              </RouterLink>
              <p class="mono mt-0.5 text-[11px] text-muted">{{ entity.id }}</p>
            </td>
            <td class="text-xs text-muted">{{ labels.entityType(entity.entityType) }}</td>
            <td class="text-xs text-muted">{{ labels.documentType(entity.idDocType) }}</td>
            <td>
              <Badge :tone="labels.verificationTone(entity.verificationState)" size="sm">
                {{ labels.verificationState(entity.verificationState) }}
              </Badge>
            </td>
            <td class="text-xs tabular-nums text-muted">{{ entity.accountCount }}</td>
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDate(entity.createdAt) }}</td>
            <td class="text-right">
              <RouterLink
                :to="{ name: 'legal-entity-detail', params: { id: entity.id } }"
                class="inline-block rounded-md border border-line bg-surface px-2 py-1 text-xs text-ink hover:bg-canvas"
              >
                {{ $t('common.view') }}
              </RouterLink>
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
    </div>
  </div>
</template>
