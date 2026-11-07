<script setup lang="ts">
import { Plus, Search, Trash, Users } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { getApi } from '@/api'
import type { AccountSummary, OrgMember, Organization, OrgRole } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Select from '@/components/ui/Select.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useLabels } from '@/composables/useLabels'
import { debounce } from '@/lib/debounce'
import { formatDate, formatRelative, initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()

const organizations = ref<Organization[]>([])
const selectedId = ref<string | null>(null)
const loadingOrganizations = ref(true)
const organizationsError = ref<string | null>(null)

const members = ref<OrgMember[]>([])
const loadingMembers = ref(false)
const membersError = ref<string | null>(null)

const canWrite = computed(() => auth.can('org:organization:write'))
const selected = computed(() => organizations.value.find((organization) => organization.id === selectedId.value) ?? null)

async function loadOrganizations(): Promise<void> {
  loadingOrganizations.value = true
  organizationsError.value = null
  try {
    organizations.value = await api.listOrganizations()
    if (!selectedId.value && organizations.value[0]) selectedId.value = organizations.value[0].id
  } catch (cause) {
    organizationsError.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    loadingOrganizations.value = false
  }
}

async function loadMembers(): Promise<void> {
  const organizationId = selectedId.value
  if (!organizationId) {
    members.value = []
    return
  }
  loadingMembers.value = true
  membersError.value = null
  try {
    members.value = await api.listOrganizationMembers(organizationId)
  } catch (cause) {
    membersError.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    loadingMembers.value = false
  }
}

watch(selectedId, () => void loadMembers())
onMounted(async () => {
  await loadOrganizations()
  await loadMembers()
})

async function changeRole(member: OrgMember, role: string): Promise<void> {
  const organizationId = selectedId.value
  if (!organizationId || member.orgRole === role) return
  try {
    const updated = await api.updateOrganizationMember(organizationId, member.id, role as OrgRole)
    members.value = members.value.map((item) => (item.id === updated.id ? updated : item))
    toasts.success(t('orgs.roleUpdated'), member.username)
  } catch (cause) {
    toasts.error(t('toast.error'), cause instanceof Error ? cause.message : String(cause))
    await loadMembers()
  }
}

const removing = ref<OrgMember | null>(null)
const removingBusy = ref(false)
const removeError = ref<string | null>(null)

function askRemove(member: OrgMember): void {
  removing.value = member
  removeError.value = null
}

async function confirmRemove(): Promise<void> {
  const organizationId = selectedId.value
  const member = removing.value
  if (!organizationId || !member) return
  removingBusy.value = true
  removeError.value = null
  try {
    await api.removeOrganizationMember(organizationId, member.id)
    toasts.success(t('orgs.memberRemoved'), member.username)
    removing.value = null
    members.value = members.value.filter((item) => item.id !== member.id)
    await loadOrganizations()
  } catch (cause) {
    removeError.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    removingBusy.value = false
  }
}

const addOpen = ref(false)
const accountSearch = ref('')
const accountResults = ref<AccountSummary[]>([])
const accountLoading = ref(false)
const pickedAccount = ref<AccountSummary | null>(null)
const newRole = ref<OrgRole>('member')
const addingBusy = ref(false)
const addError = ref<string | null>(null)

const searchAccounts = debounce(async () => {
  accountLoading.value = true
  try {
    const page = await api.listAccounts({ limit: 8, q: accountSearch.value.trim() || undefined })
    accountResults.value = page.items
  } catch {
    accountResults.value = []
  } finally {
    accountLoading.value = false
  }
}, 250)

watch(accountSearch, searchAccounts)

function openAdd(): void {
  addOpen.value = true
  addError.value = null
  pickedAccount.value = null
  newRole.value = 'member'
  accountSearch.value = ''
  accountResults.value = []
  void searchAccounts()
}

function pick(account: AccountSummary): void {
  pickedAccount.value = account
}

async function confirmAdd(): Promise<void> {
  const organizationId = selectedId.value
  const account = pickedAccount.value
  if (!organizationId || !account) {
    addError.value = t('orgs.selectAccount')
    return
  }
  addingBusy.value = true
  addError.value = null
  try {
    await api.addOrganizationMember(organizationId, account.id, newRole.value)
    toasts.success(t('orgs.memberAdded'), account.username)
    addOpen.value = false
    await Promise.all([loadMembers(), loadOrganizations()])
  } catch (cause) {
    addError.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    addingBusy.value = false
  }
}

const ROLE_OPTIONS: OrgRole[] = ['owner', 'admin', 'member', 'viewer']
</script>

<template>
  <div>
    <PageHeader :title="$t('orgs.title')" :description="$t('orgs.description')">
      <template #actions>
        <Button v-if="canWrite && selected" variant="primary" @click="openAdd">
          <Plus class="h-4 w-4" aria-hidden="true" />
          {{ $t('orgs.addMember') }}
        </Button>
      </template>
    </PageHeader>

    <div class="grid gap-4 lg:grid-cols-[300px_1fr]">
      <Card :title="$t('orgs.listTitle')" :padded="false">
        <div v-if="loadingOrganizations" class="px-4 py-8 text-center text-sm text-muted">{{ $t('common.loading') }}</div>
        <p v-else-if="organizationsError" class="px-4 py-8 text-center text-sm text-red-600">{{ organizationsError }}</p>
        <p v-else-if="organizations.length === 0" class="px-4 py-8 text-center text-sm text-muted">{{ $t('common.emptyText') }}</p>
        <ul v-else class="divide-y divide-line">
          <li v-for="organization in organizations" :key="organization.id">
            <button
              type="button"
              class="flex w-full items-start gap-2.5 px-4 py-3 text-left transition-colors hover:bg-canvas"
              :class="organization.id === selectedId ? 'border-l-2 border-brand bg-brand-soft/60' : 'border-l-2 border-transparent'"
              @click="selectedId = organization.id"
            >
              <span class="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-surface text-muted ring-1 ring-line">
                <Users class="h-4 w-4" aria-hidden="true" />
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-medium text-ink">{{ organization.name }}</span>
                <span class="mono block text-[11px] text-muted">{{ organization.code }} · {{ organization.memberCount }} {{ $t('common.members') }}</span>
              </span>
            </button>
          </li>
        </ul>
      </Card>

      <Card v-if="selected" :title="$t('orgs.membersTitle')" :description="$t('orgs.membersDescription')" :padded="false">
        <template #actions>
          <p class="text-xs text-muted">{{ selected.name }}</p>
        </template>

        <Table>
          <thead>
            <tr>
              <th scope="col">{{ $t('orgs.columnAccount') }}</th>
              <th scope="col">{{ $t('orgs.columnRole') }}</th>
              <th scope="col">{{ $t('orgs.columnJoined') }}</th>
              <th scope="col">{{ $t('orgs.columnLastActive') }}</th>
              <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <TableMessage
              :colspan="5"
              :loading="loadingMembers"
              :error="membersError"
              :empty="members.length === 0"
              :empty-text="$t('orgs.emptyMembers')"
              @retry="loadMembers"
            />
            <tr v-for="member in members" :key="member.id">
              <td>
                <div class="flex items-center gap-2.5">
                  <span class="grid h-7 w-7 place-items-center rounded-full bg-brand-soft text-[10px] font-semibold text-brand">
                    {{ initials(member.accountName) }}
                  </span>
                  <div>
                    <p class="text-xs font-medium text-ink">{{ member.accountName }}</p>
                    <p class="text-[11px] text-muted">{{ member.username }}</p>
                  </div>
                </div>
              </td>
              <td>
                <Select
                  v-if="canWrite"
                  :model-value="member.orgRole"
                  class="w-28"
                  :aria-label="$t('orgs.selectRole')"
                  @update:model-value="(value) => changeRole(member, value)"
                >
                  <option v-for="role in ROLE_OPTIONS" :key="role" :value="role">{{ labels.orgRole(role) }}</option>
                </Select>
                <Badge v-else tone="neutral" size="sm">{{ labels.orgRole(member.orgRole) }}</Badge>
              </td>
              <td class="whitespace-nowrap text-xs text-muted">{{ formatDate(member.joinedAt) }}</td>
              <td class="whitespace-nowrap text-xs text-muted">
                {{ member.lastActiveAt ? formatRelative(member.lastActiveAt) : '—' }}
              </td>
              <td class="text-right">
                <Button v-if="canWrite" size="sm" variant="ghost" class="text-red-600 hover:bg-red-50" @click="askRemove(member)">
                  <Trash class="h-3.5 w-3.5" aria-hidden="true" />
                  {{ $t('orgs.remove') }}
                </Button>
              </td>
            </tr>
          </tbody>
        </Table>
      </Card>

      <Card v-else>
        <p class="py-10 text-center text-sm text-muted">{{ $t('orgs.noSelection') }}</p>
      </Card>
    </div>

    <Modal
      :open="addOpen"
      :title="$t('orgs.addMemberTitle')"
      :description="$t('orgs.addMemberDescription')"
      @update:open="(value) => !value && (addOpen = false)"
    >
      <div class="space-y-3">
        <div>
          <label for="member-search" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('orgs.selectAccount') }}</label>
          <div class="relative">
            <Search class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
            <Input id="member-search" v-model="accountSearch" class="pl-8" :placeholder="$t('orgs.searchAccount')" />
          </div>
          <div class="mt-2 max-h-44 overflow-y-auto rounded-md border border-line">
            <p v-if="accountLoading" class="px-3 py-3 text-xs text-muted">{{ $t('common.loading') }}</p>
            <p v-else-if="accountResults.length === 0" class="px-3 py-3 text-xs text-muted">{{ $t('orgs.emptyAccounts') }}</p>
            <button
              v-for="account in accountResults"
              :key="account.id"
              type="button"
              class="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-xs transition-colors hover:bg-canvas"
              :class="pickedAccount?.id === account.id ? 'bg-brand-soft text-brand' : 'text-ink'"
              @click="pick(account)"
            >
              <span class="min-w-0">
                <span class="block truncate font-medium">{{ account.displayName }}</span>
                <span class="block truncate text-[11px] text-muted">{{ account.username }} · {{ account.legalEntityName }}</span>
              </span>
              <Badge :tone="labels.accountStatusTone(account.status)" size="sm">{{ labels.accountStatus(account.status) }}</Badge>
            </button>
          </div>
        </div>

        <div>
          <label for="member-role" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('orgs.selectRole') }}</label>
          <Select id="member-role" v-model="newRole" class="w-40">
            <option v-for="role in ROLE_OPTIONS" :key="role" :value="role">{{ labels.orgRole(role) }}</option>
          </Select>
        </div>

        <p v-if="addError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ addError }}
        </p>
      </div>

      <template #footer>
        <Button :disabled="addingBusy" @click="addOpen = false">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="addingBusy" @click="confirmAdd">{{ $t('common.add') }}</Button>
      </template>
    </Modal>

    <Modal
      :open="removing !== null"
      :title="$t('orgs.removeTitle')"
      :description="$t('orgs.removeDescription')"
      size="sm"
      @update:open="(value) => !value && (removing = null)"
    >
      <div v-if="removing" class="rounded-md border border-line bg-canvas/60 px-3 py-2">
        <p class="text-xs font-medium text-ink">{{ removing.accountName }}</p>
        <p class="text-[11px] text-muted">{{ removing.username }} · {{ labels.orgRole(removing.orgRole) }}</p>
      </div>
      <p v-if="removeError" class="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
        {{ removeError }}
      </p>
      <template #footer>
        <Button :disabled="removingBusy" @click="removing = null">{{ $t('common.cancel') }}</Button>
        <Button variant="danger" :loading="removingBusy" @click="confirmRemove">{{ $t('orgs.remove') }}</Button>
      </template>
    </Modal>
  </div>
</template>
