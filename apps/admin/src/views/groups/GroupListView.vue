<script setup lang="ts">
import { Plus, Trash, Users } from '@lucide/vue'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { AccountSummary, Group, GroupMember } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const toasts = useToastStore()
const api = getApi()

const groups = ref<Group[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const selected = ref<Group | null>(null)
const members = ref<GroupMember[]>([])
const membersLoading = ref(false)

const showCreate = ref(false)
const creating = ref(false)
const form = ref({ realm: 'nerves', code: '', name: '' })

const accounts = ref<AccountSummary[]>([])
const memberAccountId = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    groups.value = await api.listGroups()
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    loading.value = false
  }
}

async function open(group: Group): Promise<void> {
  selected.value = group
  membersLoading.value = true
  try {
    members.value = await api.listGroupMembers(group.id)
    if (accounts.value.length === 0) {
      accounts.value = (await api.listAccounts({ limit: 100 })).items
    }
  } catch (cause) {
    toasts.error(describeError(cause))
  } finally {
    membersLoading.value = false
  }
}

async function create(): Promise<void> {
  creating.value = true
  try {
    await api.createGroup({ ...form.value })
    showCreate.value = false
    form.value = { realm: 'nerves', code: '', name: '' }
    toasts.success(t('groups.created'))
    await load()
  } catch (cause) {
    toasts.error(describeError(cause))
  } finally {
    creating.value = false
  }
}

async function remove(group: Group): Promise<void> {
  try {
    await api.deleteGroup(group.id)
    if (selected.value?.id === group.id) selected.value = null
    toasts.success(t('groups.deleted'))
    await load()
  } catch (cause) {
    toasts.error(describeError(cause))
  }
}

async function addMember(): Promise<void> {
  if (!selected.value || !memberAccountId.value) return
  try {
    await api.addGroupMember(selected.value.id, memberAccountId.value)
    memberAccountId.value = ''
    await open(selected.value)
    toasts.success(t('groups.memberAdded'))
  } catch (cause) {
    toasts.error(describeError(cause))
  }
}

async function removeMember(accountId: string): Promise<void> {
  if (!selected.value) return
  try {
    await api.removeGroupMember(selected.value.id, accountId)
    await open(selected.value)
    toasts.success(t('groups.memberRemoved'))
  } catch (cause) {
    toasts.error(describeError(cause))
  }
}

function accountLabel(accountId: string): string {
  return accounts.value.find((account) => account.id === accountId)?.displayName ?? accountId
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader :title="t('groups.title')" :description="t('groups.description')">
      <Button @click="showCreate = true"><Plus class="mr-1 size-4" />{{ t('groups.create') }}</Button>
    </PageHeader>

    <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
      <Card>
        <Table>
          <thead>
            <tr>
              <th>{{ t('groups.code') }}</th>
              <th>{{ t('groups.name') }}</th>
              <th>{{ t('groups.members') }}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <TableMessage v-if="loading" :colspan="4" :message="t('common.loading')" />
            <TableMessage v-else-if="error" :colspan="4" :message="error" />
            <TableMessage v-else-if="groups.length === 0" :colspan="4" :message="t('groups.empty')" />
            <tr v-for="group in groups" v-else :key="group.id" class="cursor-pointer" @click="open(group)">
              <td class="font-mono text-xs">{{ group.code }}</td>
              <td>{{ group.name }}</td>
              <td>{{ group.memberCount ?? 0 }}</td>
              <td class="text-right">
                <Button variant="ghost" size="sm" @click.stop="remove(group)">
                  <Trash class="size-4" />
                </Button>
              </td>
            </tr>
          </tbody>
        </Table>
      </Card>

      <Card>
        <template v-if="selected">
          <h3 class="mb-1 text-sm font-semibold">{{ selected.name }}</h3>
          <p class="mb-4 text-xs text-ink-500">{{ selected.code }}</p>

          <div class="mb-3 flex gap-2">
            <select v-model="memberAccountId" class="w-full rounded-md border border-ink-200 px-2 py-1 text-sm">
              <option value="">{{ t('groups.pickAccount') }}</option>
              <option v-for="account in accounts" :key="account.id" :value="account.id">
                {{ account.displayName }}
              </option>
            </select>
            <Button size="sm" @click="addMember">{{ t('groups.add') }}</Button>
          </div>

          <p v-if="membersLoading" class="text-xs text-ink-500">{{ t('common.loading') }}</p>
          <p v-else-if="members.length === 0" class="text-xs text-ink-500">{{ t('groups.noMembers') }}</p>
          <ul v-else class="space-y-2">
            <li v-for="member in members" :key="member.accountId" class="flex items-center justify-between text-sm">
              <span><Users class="mr-1 inline size-3" />{{ accountLabel(member.accountId) }}</span>
              <button class="text-xs text-red-600" @click="removeMember(member.accountId)">
                {{ t('groups.remove') }}
              </button>
            </li>
          </ul>
        </template>
        <p v-else class="text-xs text-ink-500">{{ t('groups.selectHint') }}</p>
      </Card>
    </div>

    <Modal :open="showCreate" :title="t('groups.create')" @update:open="showCreate = $event">
      <div class="space-y-3">
        <Input v-model="form.code" :label="t('groups.code')" />
        <Input v-model="form.name" :label="t('groups.name')" />
      </div>
      <template #footer>
        <Button variant="ghost" @click="showCreate = false">{{ t('common.cancel') }}</Button>
        <Button :loading="creating" @click="create">{{ t('common.save') }}</Button>
      </template>
    </Modal>
  </div>
</template>
