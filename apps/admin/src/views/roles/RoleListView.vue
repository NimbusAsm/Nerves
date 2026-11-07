<script setup lang="ts">
import { Pencil } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { Permission, Role } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import Textarea from '@/components/ui/Textarea.vue'
import { useLabels } from '@/composables/useLabels'
import { formatDateTime } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()

const roles = ref<Role[]>([])
const permissions = ref<Permission[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const canWrite = computed(() => auth.can('authz:role:write'))

const permissionGroups = computed(() => {
  const groups = new Map<string, Permission[]>()
  for (const permission of permissions.value) {
    const list = groups.get(permission.appName) ?? []
    list.push(permission)
    groups.set(permission.appName, list)
  }
  return [...groups.entries()].map(([appName, items]) => ({
    appName,
    items: [...items].sort((left, right) => left.code.localeCompare(right.code)),
  }))
})

const permissionByCode = computed(() => new Map(permissions.value.map((permission) => [permission.code, permission])))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [roleList, permissionList] = await Promise.all([api.listRoles(), api.listPermissions()])
    roles.value = roleList
    permissions.value = permissionList
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    loading.value = false
  }
}

onMounted(load)

const editing = ref<Role | null>(null)
const formName = ref('')
const formDescription = ref('')
const formPermissions = ref<string[]>([])
const saving = ref(false)
const saveError = ref<string | null>(null)

function openEdit(role: Role): void {
  editing.value = role
  formName.value = role.name
  formDescription.value = role.description
  formPermissions.value = [...role.permissions]
  saveError.value = null
}

function closeEdit(): void {
  if (saving.value) return
  editing.value = null
}

function hasPermission(code: string): boolean {
  return formPermissions.value.includes(code)
}

function togglePermission(code: string): void {
  formPermissions.value = hasPermission(code)
    ? formPermissions.value.filter((item) => item !== code)
    : [...formPermissions.value, code]
}

function selectAll(): void {
  formPermissions.value = permissions.value.map((permission) => permission.code)
}

function clearAll(): void {
  formPermissions.value = []
}

async function save(): Promise<void> {
  const role = editing.value
  if (!role) return
  saving.value = true
  saveError.value = null
  try {
    const updated = await api.updateRole(role.id, {
      name: formName.value.trim(),
      description: formDescription.value.trim(),
      permissions: formPermissions.value,
    })
    roles.value = roles.value.map((item) => (item.id === updated.id ? updated : item))
    toasts.success(t('roles.updated'), updated.name)
    editing.value = null
  } catch (cause) {
    saveError.value = describeError(cause)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader :title="$t('roles.title')" :description="$t('roles.description')">
      <template #actions>
        <Button :loading="loading" @click="load">{{ $t('common.refresh') }}</Button>
      </template>
    </PageHeader>

    <div
      v-if="error"
      class="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
      role="alert"
    >
      <span>{{ error }}</span>
      <Button size="sm" @click="load">{{ $t('common.retry') }}</Button>
    </div>

    <Card :title="$t('roles.rolesTitle')" :description="$t('roles.rolesDescription')" :padded="false">
      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('roles.columnRole') }}</th>
            <th scope="col">{{ $t('roles.columnScope') }}</th>
            <th scope="col">{{ $t('roles.columnApp') }}</th>
            <th scope="col">{{ $t('roles.columnPermissions') }}</th>
            <th scope="col">{{ $t('roles.columnMembers') }}</th>
            <th scope="col">{{ $t('roles.columnUpdated') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="7"
            :loading="loading"
            :error="error"
            :empty="roles.length === 0"
            @retry="load"
          />
          <tr v-for="role in roles" :key="role.id">
            <td>
              <div class="flex flex-wrap items-center gap-2">
                <p class="text-sm font-medium text-ink">{{ role.name }}</p>
                <Badge v-if="role.builtIn" tone="brand" size="sm">{{ $t('roles.builtInHint') }}</Badge>
              </div>
              <p class="mono mt-0.5 text-[11px] text-muted">{{ role.code }}</p>
              <p class="mt-1 max-w-md text-[11px] leading-relaxed text-muted">{{ role.description }}</p>
            </td>
            <td class="text-xs text-muted">{{ labels.roleScope(role.scope) }}</td>
            <td class="text-xs text-muted">{{ role.appName ?? '—' }}</td>
            <td>
              <span class="text-xs tabular-nums text-ink">{{ role.permissions.length }}</span>
              <span class="ml-1 text-[11px] text-muted">{{ $t('roles.permissionCount', { count: role.permissions.length }) }}</span>
            </td>
            <td class="text-xs tabular-nums text-muted">{{ role.memberCount }}</td>
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDateTime(role.updatedAt) }}</td>
            <td class="text-right">
              <Button v-if="canWrite" size="sm" variant="ghost" @click="openEdit(role)">
                <Pencil class="h-3.5 w-3.5" aria-hidden="true" />
                {{ $t('roles.edit') }}
              </Button>
            </td>
          </tr>
        </tbody>
      </Table>
    </Card>

    <Card class="mt-4" :title="$t('roles.permissionsTitle')" :description="$t('roles.permissionsDescription')" :padded="false">
      <Table dense>
        <thead>
          <tr>
            <th scope="col">{{ $t('common.code') }}</th>
            <th scope="col">{{ $t('common.appName') }}</th>
            <th scope="col">{{ $t('common.description') }}</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="group in permissionGroups" :key="group.appName">
            <tr class="bg-canvas/60">
              <td colspan="3" class="!py-1.5 text-[11px] font-medium uppercase tracking-wide text-muted">
                {{ group.appName }}
              </td>
            </tr>
            <tr v-for="permission in group.items" :key="permission.code">
              <td class="mono text-ink">{{ permission.code }}</td>
              <td class="text-xs text-muted">{{ permission.appName }}</td>
              <td class="text-xs text-muted">{{ permission.description }}</td>
            </tr>
          </template>
        </tbody>
      </Table>
    </Card>

    <Modal
      :open="editing !== null"
      :title="$t('roles.editTitle')"
      :description="$t('roles.editDescription')"
      size="lg"
      @update:open="(value) => !value && closeEdit()"
    >
      <div class="space-y-4">
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label for="role-name" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('roles.nameLabel') }}</label>
            <Input id="role-name" v-model="formName" :invalid="Boolean(saveError)" />
          </div>
          <div>
            <label for="role-code" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('common.code') }}</label>
            <Input id="role-code" :model-value="editing?.code ?? ''" disabled />
          </div>
        </div>

        <div>
          <label for="role-description" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('roles.descriptionLabel') }}</label>
          <Textarea id="role-description" v-model="formDescription" :rows="2" />
        </div>

        <div>
          <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
            <span class="text-xs font-medium text-ink">
              {{ $t('roles.permissionLabel') }} · {{ $t('roles.permissionCount', { count: formPermissions.length }) }}
            </span>
            <div class="flex items-center gap-2">
              <Button size="sm" variant="ghost" @click="selectAll">{{ $t('roles.selectAll') }}</Button>
              <Button size="sm" variant="ghost" @click="clearAll">{{ $t('roles.clearAll') }}</Button>
            </div>
          </div>

          <div class="max-h-72 space-y-3 overflow-y-auto rounded-md border border-line p-3">
            <div v-for="group in permissionGroups" :key="group.appName">
              <p class="mb-1.5 text-[11px] font-medium uppercase tracking-wide text-muted">{{ group.appName }}</p>
              <ul class="grid gap-1 sm:grid-cols-2">
                <li v-for="permission in group.items" :key="permission.code">
                  <label class="flex cursor-pointer items-start gap-2 rounded px-1.5 py-1 text-xs hover:bg-canvas">
                    <input
                      type="checkbox"
                      class="mt-0.5 h-3.5 w-3.5 rounded border-line text-brand focus:ring-brand/30"
                      :checked="hasPermission(permission.code)"
                      @change="togglePermission(permission.code)"
                    />
                    <span class="min-w-0">
                      <span class="mono block truncate text-ink">{{ permission.code }}</span>
                      <span class="block text-[11px] text-muted">{{ permission.description }}</span>
                    </span>
                  </label>
                </li>
              </ul>
            </div>
            <p v-if="permissionByCode.size === 0" class="text-xs text-muted">{{ $t('roles.emptyPermissions') }}</p>
          </div>
        </div>

        <p v-if="saveError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ saveError }}
        </p>
      </div>

      <template #footer>
        <Button :disabled="saving" @click="closeEdit">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="save">{{ $t('common.save') }}</Button>
      </template>
    </Modal>
  </div>
</template>
