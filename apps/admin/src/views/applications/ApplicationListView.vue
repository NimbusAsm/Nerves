<script setup lang="ts">
import { Copy, Plus } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { Application } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Input from '@/components/ui/Input.vue'
import Modal from '@/components/ui/Modal.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import Textarea from '@/components/ui/Textarea.vue'
import { formatDate, formatDateTime, initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()

const applications = ref<Application[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const canWrite = computed(() => auth.can('directory:application:write'))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    applications.value = await api.listApplications()
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    loading.value = false
  }
}

onMounted(load)

const SCOPE_OPTIONS = ['openid', 'profile', 'email', 'offline_access', 'openid:data.read']

const createOpen = ref(false)
const formName = ref('')
const formClientId = ref('')
const formDescription = ref('')
const formLogo = ref('')
const formCallbacks = ref('')
const formScopes = ref<string[]>(['openid', 'profile'])
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

function openCreate(): void {
  createOpen.value = true
  formName.value = ''
  formClientId.value = ''
  formDescription.value = ''
  formLogo.value = ''
  formCallbacks.value = ''
  formScopes.value = ['openid', 'profile']
  saveError.value = null
  clientIdTouched.value = false
}

function toggleScope(scope: string): void {
  formScopes.value = formScopes.value.includes(scope)
    ? formScopes.value.filter((item) => item !== scope)
    : [...formScopes.value, scope]
}

async function submit(): Promise<void> {
  saving.value = true
  saveError.value = null
  try {
    const application = await api.createApplication({
      name: formName.value.trim(),
      clientId: formClientId.value.trim(),
      description: formDescription.value.trim(),
      logoUrl: formLogo.value.trim() || null,
      redirectUris: formCallbacks.value
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean),
      scopes: formScopes.value,
    })
    applications.value = [application, ...applications.value]
    toasts.success(t('apps.created'), application.clientId)
    createOpen.value = false
  } catch (cause) {
    saveError.value = describeError(cause)
  } finally {
    saving.value = false
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
        <Button :loading="loading" @click="load">{{ $t('common.refresh') }}</Button>
        <Button v-if="canWrite" variant="primary" @click="openCreate">
          <Plus class="h-4 w-4" aria-hidden="true" />
          {{ $t('apps.create') }}
        </Button>
      </template>
    </PageHeader>

    <Card :padded="false">
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
            :loading="loading"
            :error="error"
            :empty="applications.length === 0"
            :empty-text="$t('apps.empty')"
            @retry="load"
          />
          <tr v-for="application in applications" :key="application.id">
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
                  <p class="truncate text-sm font-medium text-ink">{{ application.name }}</p>
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
            <td class="text-right">
              <Button size="sm" variant="ghost" @click="detail = application">{{ $t('apps.showDetails') }}</Button>
            </td>
          </tr>
        </tbody>
      </Table>
    </Card>

    <Modal
      :open="createOpen"
      :title="$t('apps.createTitle')"
      :description="$t('apps.createDescription')"
      size="lg"
      @update:open="(value) => !value && (createOpen = false)"
    >
      <form class="space-y-3" @submit.prevent="submit">
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

        <p v-if="saveError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ saveError }}
        </p>
      </form>

      <template #footer>
        <Button :disabled="saving" @click="createOpen = false">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submit">{{ $t('common.create') }}</Button>
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
          <dt class="text-muted">{{ $t('apps.rotatedAtLabel') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatDateTime(detail.lastRotatedAt) }}</dd>
        </div>
      </dl>
      <template #footer>
        <Button @click="detail = null">{{ $t('common.close') }}</Button>
      </template>
    </Modal>
  </div>
</template>
