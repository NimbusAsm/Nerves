<script setup lang="ts">
import { Copy, Pencil, Plus, RefreshCw, Trash } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { Application } from '@/api/types'
import CursorPager from '@/components/CursorPager.vue'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import Textarea from '@/components/ui/Textarea.vue'
import { useCursorList } from '@/composables/useCursorList'
import { debounce } from '@/lib/debounce'
import { formatDate, formatDateTime, initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()

const SCOPE_OPTIONS = ['openid', 'profile', 'email', 'roles', 'nerves.api', 'offline_access']

const search = ref('')
const list = useCursorList<Application>(({ limit, cursor }) =>
  api.listApplications({ limit, cursor, q: search.value.trim() || undefined }),
)

watch(search, debounce(() => void list.reloadFresh(), 250))
onMounted(() => void list.reloadFresh())

const canWrite = computed(() => auth.can('directory:application:write'))

const createOpen = ref(false)
const formName = ref('')
const formClientId = ref('')
const formDescription = ref('')
const formLogo = ref('')
const formCallbacks = ref('')
const formPostLogoutCallbacks = ref('')
const formScopes = ref<string[]>(['openid', 'profile'])
const formEnabled = ref(true)
const saving = ref(false)
const saveError = ref<string | null>(null)
const clientIdTouched = ref(false)

function suggestClientId(name: string): string {
  const slug = name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 50)
  if (slug.length >= 3) return slug
  return `client-${Date.now().toString(36).slice(-6)}`
}

watch(formName, (name) => {
  if (!clientIdTouched.value) formClientId.value = name.trim() ? suggestClientId(name.trim()) : ''
})

function toLines(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
}

function openCreate(): void {
  createOpen.value = true
  formName.value = ''
  formClientId.value = ''
  formDescription.value = ''
  formLogo.value = ''
  formCallbacks.value = ''
  formPostLogoutCallbacks.value = ''
  formScopes.value = ['openid', 'profile']
  formEnabled.value = true
  saveError.value = null
  clientIdTouched.value = false
}

function toggleScope(scope: string): void {
  formScopes.value = formScopes.value.includes(scope)
    ? formScopes.value.filter((item) => item !== scope)
    : [...formScopes.value, scope]
}

async function submitCreate(): Promise<void> {
  saving.value = true
  saveError.value = null
  try {
    const application = await api.createApplication({
      name: formName.value.trim(),
      clientId: formClientId.value.trim(),
      description: formDescription.value.trim(),
      logoUrl: formLogo.value.trim() || null,
      redirectUris: toLines(formCallbacks.value),
      postLogoutRedirectUris: toLines(formPostLogoutCallbacks.value),
      scopes: formScopes.value,
      enabled: formEnabled.value,
    })
    toasts.success(t('apps.created'), application.clientId)
    createOpen.value = false
    await list.reloadFresh()
  } catch (cause) {
    saveError.value = describeError(cause)
  } finally {
    saving.value = false
  }
}

const editing = ref<Application | null>(null)
const editName = ref('')
const editDescription = ref('')
const editLogo = ref('')
const editCallbacks = ref('')
const editPostLogoutCallbacks = ref('')
const editScopes = ref<string[]>([])
const editEnabled = ref(true)
const editBusy = ref(false)
const editError = ref<string | null>(null)

function openEdit(application: Application): void {
  editing.value = application
  editName.value = application.name
  editDescription.value = application.description
  editLogo.value = application.logoUrl ?? ''
  editCallbacks.value = application.redirectUris.join('\n')
  editPostLogoutCallbacks.value = application.postLogoutRedirectUris.join('\n')
  editScopes.value = [...application.scopes]
  editEnabled.value = application.status === 'active'
  editError.value = null
}

function toggleEditScope(scope: string): void {
  editScopes.value = editScopes.value.includes(scope)
    ? editScopes.value.filter((item) => item !== scope)
    : [...editScopes.value, scope]
}

async function submitEdit(): Promise<void> {
  const application = editing.value
  if (!application) return

  editBusy.value = true
  editError.value = null
  try {
    const updated = await api.updateApplication(application.id, {
      name: editName.value.trim(),
      description: editDescription.value.trim(),
      logoUrl: editLogo.value.trim() || null,
      redirectUris: toLines(editCallbacks.value),
      postLogoutRedirectUris: toLines(editPostLogoutCallbacks.value),
      scopes: editScopes.value,
      enabled: editEnabled.value,
    })
    toasts.success(t('apps.updated'), updated.clientId)
    editing.value = null
    await list.reload()
  } catch (cause) {
    editError.value = describeError(cause)
  } finally {
    editBusy.value = false
  }
}

async function toggleStatus(application: Application): Promise<void> {
  try {
    const updated = await api.updateApplication(application.id, {
      enabled: application.status !== 'active',
    })
    toasts.success(updated.status === 'active' ? t('apps.enabled') : t('apps.disabled'), updated.clientId)
    await list.reload()
  } catch (cause) {
    toasts.error(t('toast.error'), describeError(cause))
  }
}

const deleting = ref<Application | null>(null)
const deleteBusy = ref(false)
const deleteError = ref<string | null>(null)

function askDelete(application: Application): void {
  deleting.value = application
  deleteError.value = null
}

async function confirmDelete(): Promise<void> {
  const application = deleting.value
  if (!application) return
  deleteBusy.value = true
  deleteError.value = null
  try {
    await api.deleteApplication(application.id)
    toasts.success(t('apps.deleted'), application.clientId)
    deleting.value = null
    await list.reloadFresh()
  } catch (cause) {
    deleteError.value = describeError(cause)
  } finally {
    deleteBusy.value = false
  }
}

const detail = ref<Application | null>(null)
const logoBroken = ref<Set<string>>(new Set())

function markLogoBroken(url: string): void {
  logoBroken.value = new Set([...logoBroken.value, url])
}

function showLogo(application: Application): boolean {
  return Boolean(application.logoUrl) && !logoBroken.value.has(application.logoUrl ?? '')
}

async function copyClientId(clientId: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(clientId)
    toasts.success(t('common.copied'), clientId)
  } catch {
    toasts.error(t('toast.error'), clientId)
  }
}
</script>

<template>
  <div>
    <PageHeader :title="$t('apps.title')" :description="$t('apps.description')">
      <template #actions>
        <Button :loading="list.loading.value" @click="list.reload()">
          <RefreshCw class="h-4 w-4" aria-hidden="true" />
          {{ $t('common.refresh') }}
        </Button>
        <Button v-if="canWrite" variant="primary" @click="openCreate">
          <Plus class="h-4 w-4" aria-hidden="true" />
          {{ $t('apps.create') }}
        </Button>
      </template>
    </PageHeader>

    <Card :padded="false">
      <div class="border-b border-line p-4">
        <Input v-model="search" type="search" :placeholder="$t('apps.searchPlaceholder')" :aria-label="$t('common.search')" />
      </div>

      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('apps.columnApplication') }}</th>
            <th scope="col">{{ $t('apps.columnClientId') }}</th>
            <th scope="col">{{ $t('apps.columnCallbacks') }}</th>
            <th scope="col">{{ $t('apps.columnScopes') }}</th>
            <th scope="col">{{ $t('apps.columnStatus') }}</th>
            <th scope="col">{{ $t('apps.columnCreated') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="7"
            :loading="list.loading.value"
            :error="list.error.value"
            :empty="list.isEmpty.value"
            :empty-text="$t('apps.empty')"
            @retry="list.reloadFresh()"
          />
          <tr v-for="application in list.items.value" :key="application.id">
            <td>
              <div class="flex items-center gap-2.5">
                <span class="relative grid h-8 w-8 shrink-0 place-items-center overflow-hidden rounded-md bg-brand-soft text-[11px] font-semibold text-brand">
                  {{ initials(application.name) }}
                  <img
                    v-if="showLogo(application)"
                    :src="application.logoUrl ?? ''"
                    :alt="application.name"
                    class="absolute inset-0 h-full w-full object-cover"
                    @error="markLogoBroken(application.logoUrl ?? '')"
                  />
                </span>
                <div class="min-w-0">
                  <p class="flex items-center gap-1.5 truncate text-sm font-medium text-ink">
                    {{ application.name }}
                    <Badge v-if="application.builtIn" tone="brand" size="sm">{{ $t('apps.builtIn') }}</Badge>
                  </p>
                  <p class="max-w-[280px] truncate text-[11px] text-muted">
                    {{ application.description || $t('apps.noDescription') }}
                  </p>
                </div>
              </div>
            </td>
            <td>
              <div class="flex items-center gap-1.5">
                <span class="mono text-ink">{{ application.clientId }}</span>
                <button
                  type="button"
                  class="rounded p-0.5 text-muted transition-colors hover:text-ink"
                  :aria-label="$t('common.copy')"
                  :title="$t('common.copy')"
                  @click="copyClientId(application.clientId)"
                >
                  <Copy class="h-3.5 w-3.5" />
                </button>
              </div>
            </td>
            <td class="text-xs text-muted">
              {{ $t('apps.callbacksCount', { count: application.redirectUris.length }) }}
            </td>
            <td>
              <div class="flex max-w-[220px] flex-wrap gap-1">
                <Badge v-for="scope in application.scopes.slice(0, 3)" :key="scope" tone="neutral" size="sm">{{ scope }}</Badge>
                <Badge v-if="application.scopes.length > 3" tone="neutral" size="sm">+{{ application.scopes.length - 3 }}</Badge>
              </div>
            </td>
            <td>
              <Badge :tone="application.status === 'active' ? 'success' : 'neutral'" size="sm">
                {{ application.status === 'active' ? $t('apps.statusActive') : $t('apps.statusDisabled') }}
              </Badge>
            </td>
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDate(application.createdAt) }}</td>
            <td>
              <div class="flex items-center justify-end gap-1.5">
                <Button size="sm" variant="ghost" @click="detail = application">{{ $t('apps.showDetails') }}</Button>
                <Button v-if="canWrite" size="sm" variant="ghost" @click="openEdit(application)">
                  <Pencil class="h-3.5 w-3.5" aria-hidden="true" />
                  {{ $t('common.edit') }}
                </Button>
                <Button
                  v-if="canWrite && !application.builtIn"
                  size="sm"
                  variant="ghost"
                  class="text-red-600 hover:bg-red-50"
                  @click="askDelete(application)"
                >
                  <Trash class="h-3.5 w-3.5" aria-hidden="true" />
                  {{ $t('common.remove') }}
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
    </Card>

    <Modal
      :open="createOpen"
      :title="$t('apps.createTitle')"
      :description="$t('apps.createDescription')"
      size="lg"
      @update:open="(value) => !value && (createOpen = false)"
    >
      <form class="space-y-3" @submit.prevent="submitCreate">
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label for="app-name" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.nameLabel') }}</label>
            <Input id="app-name" v-model="formName" :placeholder="$t('apps.namePlaceholder')" :invalid="Boolean(saveError)" />
          </div>
          <div>
            <label for="app-client-id" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.clientIdLabel') }}</label>
            <Input
              id="app-client-id"
              v-model="formClientId"
              class="mono"
              :placeholder="$t('apps.clientIdPlaceholder')"
              :invalid="Boolean(saveError)"
              @input="clientIdTouched = true"
            />
            <p class="mt-1 text-[11px] leading-relaxed text-muted">{{ $t('apps.clientIdHint') }}</p>
          </div>
        </div>

        <div>
          <label for="app-description" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.descriptionLabel') }}</label>
          <Textarea id="app-description" v-model="formDescription" :rows="2" :placeholder="$t('apps.descriptionPlaceholder')" />
        </div>

        <div>
          <label for="app-logo" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.logoLabel') }}</label>
          <Input id="app-logo" v-model="formLogo" :placeholder="$t('apps.logoPlaceholder')" />
          <p class="mt-1 text-[11px] leading-relaxed text-muted">{{ $t('apps.logoHint') }}</p>
        </div>

        <div>
          <label for="app-callbacks" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.callbacksLabel') }}</label>
          <Textarea
            id="app-callbacks"
            v-model="formCallbacks"
            :rows="3"
            class="mono"
            :placeholder="$t('apps.callbacksPlaceholder')"
            :invalid="Boolean(saveError)"
          />
          <p class="mt-1 text-[11px] leading-relaxed text-muted">{{ $t('apps.callbacksHint') }}</p>
        </div>

        <div>
          <label for="app-post-logout" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.postLogoutLabel') }}</label>
          <Textarea id="app-post-logout" v-model="formPostLogoutCallbacks" :rows="2" class="mono" :placeholder="$t('apps.postLogoutPlaceholder')" />
          <p class="mt-1 text-[11px] leading-relaxed text-muted">{{ $t('apps.postLogoutHint') }}</p>
        </div>

        <div>
          <p class="mb-1.5 text-xs font-medium text-ink">{{ $t('apps.scopesLabel') }}</p>
          <div class="flex flex-wrap gap-2">
            <label
              v-for="scope in SCOPE_OPTIONS"
              :key="scope"
              class="inline-flex cursor-pointer items-center gap-1.5 rounded-md border px-2 py-1 text-xs"
              :class="formScopes.includes(scope) ? 'border-brand bg-brand-soft text-brand' : 'border-line text-muted'"
            >
              <input
                type="checkbox"
                class="h-3 w-3 rounded border-line text-brand"
                :checked="formScopes.includes(scope)"
                @change="toggleScope(scope)"
              />
              <span class="mono">{{ scope }}</span>
            </label>
          </div>
          <p class="mt-1 text-[11px] leading-relaxed text-muted">{{ $t('apps.scopesHint') }}</p>
        </div>

        <label class="flex items-center gap-2 text-xs text-ink">
          <input v-model="formEnabled" type="checkbox" class="h-3.5 w-3.5 rounded border-line text-brand" />
          {{ $t('apps.enabledLabel') }}
        </label>

        <p v-if="saveError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ saveError }}
        </p>
      </form>

      <template #footer>
        <Button :disabled="saving" @click="createOpen = false">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submitCreate">{{ $t('common.create') }}</Button>
      </template>
    </Modal>

    <Modal
      :open="editing !== null"
      :title="$t('apps.editTitle')"
      :description="$t('apps.editDescription')"
      size="lg"
      @update:open="(value) => !value && (editing = null)"
    >
      <div class="space-y-3">
        <div class="rounded-md border border-line bg-canvas/60 px-3 py-2">
          <p class="text-xs font-medium text-ink">{{ editing?.name }}</p>
          <p class="mono mt-0.5 text-muted">{{ editing?.clientId }}</p>
        </div>

        <div>
          <label for="edit-app-name" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.nameLabel') }}</label>
          <Input id="edit-app-name" v-model="editName" :invalid="Boolean(editError)" />
        </div>

        <div>
          <label for="edit-app-description" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.descriptionLabel') }}</label>
          <Textarea id="edit-app-description" v-model="editDescription" :rows="2" />
        </div>

        <div>
          <label for="edit-app-logo" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.logoLabel') }}</label>
          <Input id="edit-app-logo" v-model="editLogo" :placeholder="$t('apps.logoPlaceholder')" />
        </div>

        <div>
          <label for="edit-app-callbacks" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.callbacksLabel') }}</label>
          <Textarea id="edit-app-callbacks" v-model="editCallbacks" :rows="3" class="mono" />
        </div>

        <div>
          <label for="edit-app-post-logout" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('apps.postLogoutLabel') }}</label>
          <Textarea id="edit-app-post-logout" v-model="editPostLogoutCallbacks" :rows="2" class="mono" />
        </div>

        <div>
          <p class="mb-1.5 text-xs font-medium text-ink">{{ $t('apps.scopesLabel') }}</p>
          <div class="flex flex-wrap gap-2">
            <label
              v-for="scope in SCOPE_OPTIONS"
              :key="scope"
              class="inline-flex cursor-pointer items-center gap-1.5 rounded-md border px-2 py-1 text-xs"
              :class="editScopes.includes(scope) ? 'border-brand bg-brand-soft text-brand' : 'border-line text-muted'"
            >
              <input
                type="checkbox"
                class="h-3 w-3 rounded border-line text-brand"
                :checked="editScopes.includes(scope)"
                @change="toggleEditScope(scope)"
              />
              <span class="mono">{{ scope }}</span>
            </label>
          </div>
        </div>

        <label class="flex items-center gap-2 text-xs text-ink">
          <input v-model="editEnabled" type="checkbox" class="h-3.5 w-3.5 rounded border-line text-brand" />
          {{ $t('apps.enabledLabel') }}
        </label>

        <p v-if="editError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ editError }}
        </p>
      </div>

      <template #footer>
        <Button :disabled="editBusy" @click="editing = null">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="editBusy" @click="submitEdit">{{ $t('common.save') }}</Button>
      </template>
    </Modal>

    <Modal
      :open="deleting !== null"
      :title="$t('apps.deleteTitle')"
      :description="$t('apps.deleteDescription')"
      size="sm"
      @update:open="(value) => !value && (deleting = null)"
    >
      <div v-if="deleting" class="rounded-md border border-line bg-canvas/60 px-3 py-2">
        <p class="text-xs font-medium text-ink">{{ deleting.name }}</p>
        <p class="mono mt-0.5 text-muted">{{ deleting.clientId }}</p>
      </div>
      <p v-if="deleteError" class="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
        {{ deleteError }}
      </p>
      <template #footer>
        <Button :disabled="deleteBusy" @click="deleting = null">{{ $t('common.cancel') }}</Button>
        <Button variant="danger" :loading="deleteBusy" @click="confirmDelete">{{ $t('common.remove') }}</Button>
      </template>
    </Modal>

    <Modal
      :open="detail !== null"
      :title="detail?.name ?? $t('apps.detailTitle')"
      size="lg"
      @update:open="(value) => !value && (detail = null)"
    >
      <dl v-if="detail" class="grid gap-x-6 gap-y-3 text-xs sm:grid-cols-2">
        <div>
          <dt class="text-muted">{{ $t('apps.clientIdLabel') }}</dt>
          <dd class="mono mt-0.5 break-all text-ink">{{ detail.clientId }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('apps.columnStatus') }}</dt>
          <dd class="mt-0.5">
            <Badge :tone="detail.status === 'active' ? 'success' : 'neutral'" size="sm">
              {{ detail.status === 'active' ? $t('apps.statusActive') : $t('apps.statusDisabled') }}
            </Badge>
            <Badge v-if="detail.builtIn" tone="brand" size="sm" class="ml-1">{{ $t('apps.builtIn') }}</Badge>
          </dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('apps.descriptionLabel') }}</dt>
          <dd class="mt-0.5 leading-relaxed text-ink">{{ detail.description || $t('apps.noDescription') }}</dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('apps.callbacksLabel') }}</dt>
          <dd class="mt-1 space-y-1">
            <p v-for="uri in detail.redirectUris" :key="uri" class="mono break-all rounded bg-canvas px-2 py-1 text-ink">{{ uri }}</p>
            <p v-if="detail.redirectUris.length === 0" class="text-muted">{{ $t('common.none') }}</p>
          </dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('apps.postLogoutLabel') }}</dt>
          <dd class="mt-1 space-y-1">
            <p v-for="uri in detail.postLogoutRedirectUris" :key="uri" class="mono break-all rounded bg-canvas px-2 py-1 text-ink">{{ uri }}</p>
            <p v-if="detail.postLogoutRedirectUris.length === 0" class="text-muted">{{ $t('common.none') }}</p>
          </dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('apps.scopesLabel') }}</dt>
          <dd class="mt-1 flex flex-wrap gap-1">
            <Badge v-for="scope in detail.scopes" :key="scope" tone="neutral" size="sm">{{ scope }}</Badge>
          </dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('apps.createdAtLabel') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatDateTime(detail.createdAt) }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('apps.updatedAtLabel') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatDateTime(detail.updatedAt) }}</dd>
        </div>
      </dl>
      <template #footer>
        <Button v-if="canWrite" variant="secondary" :disabled="!detail" @click="detail && (openEdit(detail), (detail = null))">
          {{ $t('common.edit') }}
        </Button>
        <Button variant="secondary" :disabled="!detail || detail.builtIn" @click="detail && (toggleStatus(detail), (detail = null))">
          {{ detail?.status === 'active' ? $t('apps.disable') : $t('apps.enable') }}
        </Button>
        <Button @click="detail = null">{{ $t('common.close') }}</Button>
      </template>
    </Modal>
  </div>
</template>

