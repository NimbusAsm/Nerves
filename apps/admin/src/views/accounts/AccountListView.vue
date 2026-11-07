<script setup lang="ts">
import { Search, SlidersHorizontal } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

import { getApi } from '@/api'
import type { AccountStatus, AccountSummary } from '@/api/types'
import CursorPager from '@/components/CursorPager.vue'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Select from '@/components/ui/Select.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import Textarea from '@/components/ui/Textarea.vue'
import { useAccountStatusAction } from '@/composables/useAccountStatusAction'
import { useCursorList } from '@/composables/useCursorList'
import { useLabels } from '@/composables/useLabels'
import { debounce } from '@/lib/debounce'
import { formatDateTime, formatRelative, initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const api = getApi()

const search = ref('')
const status = ref<AccountStatus | ''>('')
const realm = ref('')

const list = useCursorList<AccountSummary>(({ limit, cursor }) =>
  api.listAccounts({
    limit,
    cursor,
    q: search.value.trim() || undefined,
    status: status.value,
    realm: realm.value,
  }),
)

const reloadSoon = debounce(() => void list.reloadFresh(), 250)
watch([search, status, realm], reloadSoon)
onMounted(() => void list.reloadFresh())

const canWrite = computed(() => auth.can('identity:account:write'))
const hasFilters = computed(() => search.value !== '' || status.value !== '' || realm.value !== '')

function resetFilters(): void {
  search.value = ''
  status.value = ''
  realm.value = ''
}

const statusAction = useAccountStatusAction(() => list.reload())

const dialogTitle = computed(() =>
  statusAction.mode.value === 'disable' ? t('accounts.disableTitle') : t('accounts.enableTitle'),
)
const dialogDescription = computed(() =>
  statusAction.mode.value === 'disable' ? t('accounts.disableDescription') : t('accounts.enableDescription'),
)
</script>

<template>
  <div>
    <PageHeader :title="$t('accounts.title')" :description="$t('accounts.description')">
      <template #actions>
        <Button :loading="list.loading.value" @click="list.reload()">
          <SlidersHorizontal class="h-4 w-4" aria-hidden="true" />
          {{ $t('common.refresh') }}
        </Button>
      </template>
    </PageHeader>

    <div class="rounded-lg border border-line bg-surface shadow-xs">
      <div class="flex flex-col gap-3 border-b border-line p-4 lg:flex-row lg:items-center">
        <div class="relative min-w-0 flex-1">
          <Search class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <Input
            v-model="search"
            class="pl-8"
            :placeholder="$t('accounts.searchPlaceholder')"
            type="search"
            :aria-label="$t('common.search')"
          />
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <Select v-model="status" class="w-36" :aria-label="$t('accounts.statusFilter')">
            <option value="">{{ $t('accounts.allStatuses') }}</option>
            <option value="active">{{ $t('status.account.active') }}</option>
            <option value="locked">{{ $t('status.account.locked') }}</option>
            <option value="disabled">{{ $t('status.account.disabled') }}</option>
          </Select>
          <Select v-model="realm" class="w-32" :aria-label="$t('accounts.realmFilter')">
            <option value="">{{ $t('accounts.allRealms') }}</option>
            <option value="cn">{{ $t('status.realm.cn') }}</option>
            <option value="partner">{{ $t('status.realm.partner') }}</option>
          </Select>
          <Button v-if="hasFilters" variant="ghost" size="sm" @click="resetFilters">
            {{ $t('common.reset') }}
          </Button>
        </div>
      </div>

      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('accounts.columnAccount') }}</th>
            <th scope="col">{{ $t('accounts.columnEntity') }}</th>
            <th scope="col">{{ $t('accounts.columnType') }}</th>
            <th scope="col">{{ $t('accounts.columnStatus') }}</th>
            <th scope="col">{{ $t('accounts.columnMfa') }}</th>
            <th scope="col">{{ $t('accounts.columnSessions') }}</th>
            <th scope="col">{{ $t('accounts.columnLastLogin') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="8"
            :loading="list.loading.value"
            :error="list.error.value"
            :empty="list.isEmpty.value"
            :empty-text="$t('accounts.empty')"
            @retry="list.reloadFresh()"
          />
          <tr v-for="account in list.items.value" :key="account.id">
            <td>
              <div class="flex items-center gap-2.5">
                <span class="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-soft text-[11px] font-semibold text-brand">
                  {{ initials(account.displayName) }}
                </span>
                <div class="min-w-0">
                  <RouterLink
                    :to="{ name: 'account-detail', params: { id: account.id } }"
                    class="block truncate text-sm font-medium text-ink hover:text-brand"
                  >
                    {{ account.displayName }}
                  </RouterLink>
                  <p class="truncate text-[11px] text-muted">{{ account.username }} · {{ account.email }}</p>
                </div>
              </div>
            </td>
            <td class="max-w-[220px]">
              <RouterLink
                :to="{ name: 'legal-entity-detail', params: { id: account.legalEntityId } }"
                class="block truncate text-xs text-muted hover:text-brand"
              >
                {{ account.legalEntityName }}
              </RouterLink>
            </td>
            <td class="text-xs text-muted">{{ labels.accountType(account.type) }}</td>
            <td>
              <div class="flex items-center gap-1.5">
                <Badge :tone="labels.accountStatusTone(account.status)" size="sm">
                  {{ labels.accountStatus(account.status) }}
                </Badge>
                <span v-if="account.isPrimary" class="text-[10px] text-muted">{{ $t('accounts.isPrimary') }}</span>
              </div>
            </td>
            <td>
              <span :class="account.mfa ? 'text-emerald-600' : 'text-muted'">{{ account.mfa ? $t('common.yes') : $t('common.no') }}</span>
            </td>
            <td class="text-xs tabular-nums text-muted">{{ account.sessionCount }}</td>
            <td class="whitespace-nowrap text-xs text-muted" :title="formatDateTime(account.lastLoginAt)">
              {{ account.lastLoginAt ? formatRelative(account.lastLoginAt) : '—' }}
            </td>
            <td>
              <div class="flex items-center justify-end gap-1.5">
                <RouterLink
                  :to="{ name: 'account-detail', params: { id: account.id } }"
                  class="rounded-md border border-line bg-surface px-2 py-1 text-xs text-ink hover:bg-canvas"
                >
                  {{ $t('accounts.details') }}
                </RouterLink>
                <Button
                  v-if="canWrite && account.status !== 'disabled'"
                  size="sm"
                  variant="ghost"
                  class="text-red-600 hover:bg-red-50"
                  @click="statusAction.open(account, 'disable')"
                >
                  {{ $t('accounts.disable') }}
                </Button>
                <Button
                  v-else-if="canWrite"
                  size="sm"
                  variant="ghost"
                  class="text-emerald-600 hover:bg-emerald-50"
                  @click="statusAction.open(account, 'enable')"
                >
                  {{ $t('accounts.enable') }}
                </Button>
              </div>
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

    <Modal
      :open="statusAction.target.value !== null"
      :title="dialogTitle"
      :description="dialogDescription"
      @update:open="(value) => !value && statusAction.close()"
    >
      <div class="space-y-3">
        <div class="rounded-md border border-line bg-canvas/60 px-3 py-2">
          <p class="text-xs font-medium text-ink">{{ statusAction.target.value?.displayName }}</p>
          <p class="mono mt-0.5 text-muted">{{ statusAction.target.value?.username }}</p>
        </div>

        <div v-if="statusAction.mode.value === 'disable'">
          <label for="disable-reason" class="mb-1.5 block text-xs font-medium text-ink">
            {{ $t('accounts.disableReason') }}
          </label>
          <Textarea
            id="disable-reason"
            v-model="statusAction.reason.value"
            :rows="3"
            :placeholder="$t('accounts.disableReasonPlaceholder')"
            :invalid="Boolean(statusAction.error.value)"
          />
        </div>

        <p
          v-if="statusAction.error.value"
          class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700"
          role="alert"
        >
          {{ statusAction.error.value }}
        </p>
      </div>

      <template #footer>
        <Button :disabled="statusAction.saving.value" @click="statusAction.close()">
          {{ $t('common.cancel') }}
        </Button>
        <Button
          :variant="statusAction.mode.value === 'disable' ? 'danger' : 'primary'"
          :loading="statusAction.saving.value"
          @click="statusAction.confirm()"
        >
          {{ statusAction.mode.value === 'disable' ? $t('accounts.disableConfirm') : $t('accounts.enableConfirm') }}
        </Button>
      </template>
    </Modal>
  </div>
</template>
