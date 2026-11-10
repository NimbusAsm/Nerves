<script setup lang="ts">
import { Bell, Send, Search } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { NotificationRecord, NotificationStatus } from '@/api/types'
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
import { debounce } from '@/lib/debounce'
import { formatDateTime, formatRelative } from '@/lib/format'
import {
  findNotificationTemplate,
  missingTemplateValues,
  NOTIFICATION_TEMPLATES,
  normalizeTemplateValues,
} from '@/lib/notification-templates'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()

const search = ref('')
const status = ref<NotificationStatus | ''>('')

const list = useCursorList<NotificationRecord>(({ limit, cursor }) =>
  api.listNotifications({
    limit,
    cursor,
    status: status.value,
    to: search.value.trim() || undefined,
  }),
)

const reloadSoon = debounce(() => void list.reloadFresh(), 250)
watch([search, status], reloadSoon)
onMounted(() => void list.reloadFresh())

const hasFilters = computed(() => search.value !== '' || status.value !== '')

function resetFilters(): void {
  search.value = ''
  status.value = ''
}

const canSend = computed(() => auth.can('nerves:admin'))

const sendOpen = ref(false)
const formTo = ref('')
const formTemplate = ref(NOTIFICATION_TEMPLATES[0]?.code ?? '')
const formValues = ref<Record<string, string>>({})
const sending = ref(false)
const sendError = ref<string | null>(null)

const selectedTemplate = computed(() => findNotificationTemplate(formTemplate.value))

function openSend(): void {
  sendOpen.value = true
  sendError.value = null
  formTo.value = ''
  formTemplate.value = NOTIFICATION_TEMPLATES[0]?.code ?? ''
  formValues.value = defaultValues(formTemplate.value)
}

function defaultValues(code: string): Record<string, string> {
  const template = findNotificationTemplate(code)
  const values: Record<string, string> = {}
  for (const placeholder of template?.placeholders ?? []) values[placeholder.name] = ''
  return values
}

/** Renders the placeholder token without a literal mustache in the template. */
function placeholderToken(name: string): string {
  return '{' + '{' + name + '}' + '}'
}

/**
 * The examples contain an `@`, which vue-i18n treats as linked-message syntax
 * when it compiles a translation. Hints are therefore rendered as plain text.
 */
function placeholderHint(placeholder: { hintKey: string }): string {
  return t(placeholder.hintKey)
}

function onTemplateChange(): void {
  const previous = formValues.value
  const next = defaultValues(formTemplate.value)
  for (const key of Object.keys(next)) {
    next[key] = previous[key] ?? ''
  }
  formValues.value = next
}

async function submit(): Promise<void> {
  sendError.value = null
  const recipient = formTo.value.trim()
  if (!recipient) {
    sendError.value = t('notifications.errors.recipientRequired')
    return
  }

  const template = findNotificationTemplate(formTemplate.value)
  if (!template) {
    sendError.value = t('notifications.errors.templateRequired')
    return
  }

  const values = normalizeTemplateValues(formValues.value)
  const missing = missingTemplateValues(formTemplate.value, values)
  if (missing.length > 0) {
    sendError.value = t('notifications.errors.missingValues', { fields: missing.join(', ') })
    return
  }

  sending.value = true
  try {
    const created = await api.sendNotificationEmail({ to: recipient, template: template.code, values })
    toasts.success(t('notifications.sendQueued'), created.to)
    sendOpen.value = false
    await list.reloadFresh()
  } catch (cause) {
    sendError.value = describeError(cause)
  } finally {
    sending.value = false
  }
}

const detail = ref<NotificationRecord | null>(null)
</script>

<template>
  <div>
    <PageHeader :title="$t('notifications.title')" :description="$t('notifications.description')">
      <template #actions>
        <Button :loading="list.loading.value" @click="list.reload()">{{ $t('common.refresh') }}</Button>
        <Button v-if="canSend" variant="primary" @click="openSend">
          <Send class="h-4 w-4" aria-hidden="true" />
          {{ $t('notifications.send') }}
        </Button>
      </template>
    </PageHeader>

    <Card :padded="false">
      <div class="flex flex-col gap-3 border-b border-line p-4 sm:flex-row sm:items-center">
        <div class="relative min-w-0 flex-1">
          <Search class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <Input
            v-model="search"
            class="pl-8"
            type="search"
            :placeholder="$t('notifications.searchPlaceholder')"
            :aria-label="$t('common.search')"
          />
        </div>
        <div class="flex items-center gap-2">
          <Select v-model="status" class="w-36" :aria-label="$t('notifications.statusFilter')">
            <option value="">{{ $t('notifications.allStatuses') }}</option>
            <option value="pending">{{ $t('status.notification.pending') }}</option>
            <option value="sent">{{ $t('status.notification.sent') }}</option>
            <option value="failed">{{ $t('status.notification.failed') }}</option>
          </Select>
          <Button v-if="hasFilters" variant="ghost" size="sm" @click="resetFilters">{{ $t('common.reset') }}</Button>
        </div>
      </div>

      <Table>
        <thead>
          <tr>
            <th scope="col">{{ $t('notifications.columnRecipient') }}</th>
            <th scope="col">{{ $t('notifications.columnTemplate') }}</th>
            <th scope="col">{{ $t('notifications.columnStatus') }}</th>
            <th scope="col">{{ $t('notifications.columnAttempts') }}</th>
            <th scope="col">{{ $t('notifications.columnCreated') }}</th>
            <th scope="col">{{ $t('notifications.columnSent') }}</th>
            <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <TableMessage
            :colspan="7"
            :loading="list.loading.value"
            :error="list.error.value"
            :empty="list.isEmpty.value"
            :empty-text="$t('notifications.empty')"
            @retry="list.reloadFresh()"
          />
          <tr v-for="notification in list.items.value" :key="notification.id">
            <td>
              <div class="flex items-center gap-2.5">
                <span class="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-soft text-brand">
                  <Bell class="h-3.5 w-3.5" aria-hidden="true" />
                </span>
                <div class="min-w-0">
                  <p class="truncate text-xs font-medium text-ink">{{ notification.to }}</p>
                  <p class="max-w-[220px] truncate text-[11px] text-muted">{{ notification.subject }}</p>
                </div>
              </div>
            </td>
            <td class="text-xs text-muted">
              {{ findNotificationTemplate(notification.template) ? $t(findNotificationTemplate(notification.template)!.labelKey) : notification.template }}
            </td>
            <td>
              <Badge :tone="labels.notificationTone(notification.status)" size="sm">
                {{ labels.notificationStatus(notification.status) }}
              </Badge>
            </td>
            <td class="text-xs tabular-nums text-muted">{{ notification.attempts }}</td>
            <td class="whitespace-nowrap text-xs text-muted">{{ formatDateTime(notification.createdAt) }}</td>
            <td class="whitespace-nowrap text-xs text-muted">
              <template v-if="notification.sentAt">{{ formatDateTime(notification.sentAt) }}</template>
              <template v-else>—</template>
            </td>
            <td class="text-right">
              <Button size="sm" variant="ghost" @click="detail = notification">{{ $t('common.view') }}</Button>
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
      :open="sendOpen"
      :title="$t('notifications.sendTitle')"
      :description="$t('notifications.sendDescription')"
      size="lg"
      @update:open="(value) => !value && (sendOpen = false)"
    >
      <form class="space-y-4" @submit.prevent="submit">
        <div>
          <label for="notification-to" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('notifications.recipientLabel') }}</label>
          <Input
            id="notification-to"
            v-model="formTo"
            type="email"
            :placeholder="$t('notifications.recipientPlaceholder')"
            :invalid="Boolean(sendError)"
          />
        </div>

        <div>
          <label for="notification-template" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('notifications.templateLabel') }}</label>
          <Select id="notification-template" v-model="formTemplate" @update:model-value="onTemplateChange">
            <option v-for="template in NOTIFICATION_TEMPLATES" :key="template.code" :value="template.code">
              {{ $t(template.labelKey) }}
            </option>
          </Select>
          <p v-if="selectedTemplate" class="mt-1 text-[11px] leading-relaxed text-muted">
            {{ $t(selectedTemplate.descriptionKey) }}
          </p>
        </div>

        <div>
          <p class="mb-2 text-xs font-medium text-ink">{{ $t('notifications.valuesLabel') }}</p>
          <div class="space-y-3 rounded-md border border-line bg-canvas/40 p-3">
            <div v-for="placeholder in selectedTemplate?.placeholders ?? []" :key="placeholder.name">
              <label :for="`notification-value-${placeholder.name}`" class="mb-1 block text-[11px] font-medium text-ink">
                {{ $t(placeholder.labelKey) }}
                <code class="mono ml-1 rounded bg-surface px-1 text-[10px] text-muted">{{ placeholderToken(placeholder.name) }}</code>
              </label>
              <Input
                :id="`notification-value-${placeholder.name}`"
                v-model="formValues[placeholder.name]"
                :placeholder="placeholderHint(placeholder)"
              />
            </div>
            <p v-if="(selectedTemplate?.placeholders ?? []).length === 0" class="text-xs text-muted">
              {{ $t('notifications.noValues') }}
            </p>
          </div>
        </div>

        <p v-if="sendError" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ sendError }}
        </p>
      </form>

      <template #footer>
        <Button :disabled="sending" @click="sendOpen = false">{{ $t('common.cancel') }}</Button>
        <Button variant="primary" :loading="sending" @click="submit">{{ $t('notifications.sendAction') }}</Button>
      </template>
    </Modal>

    <Modal
      :open="detail !== null"
      :title="$t('notifications.detailTitle')"
      size="lg"
      @update:open="(value) => !value && (detail = null)"
    >
      <dl v-if="detail" class="grid gap-x-6 gap-y-3 text-xs sm:grid-cols-2">
        <div>
          <dt class="text-muted">{{ $t('notifications.recipientLabel') }}</dt>
          <dd class="mt-0.5 break-all text-ink">{{ detail.to }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('notifications.columnStatus') }}</dt>
          <dd class="mt-0.5">
            <Badge :tone="labels.notificationTone(detail.status)" size="sm">
              {{ labels.notificationStatus(detail.status) }}
            </Badge>
          </dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('notifications.columnTemplate') }}</dt>
          <dd class="mono mt-0.5 text-ink">{{ detail.template }}</dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-muted">{{ $t('notifications.subjectLabel') }}</dt>
          <dd class="mt-0.5 text-ink">{{ detail.subject }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('notifications.columnAttempts') }}</dt>
          <dd class="mt-0.5 tabular-nums text-ink">{{ detail.attempts }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('notifications.columnCreated') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatDateTime(detail.createdAt) }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('notifications.columnSent') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatDateTime(detail.sentAt) }}</dd>
        </div>
        <div>
          <dt class="text-muted">{{ $t('notifications.nextAttemptLabel') }}</dt>
          <dd class="mt-0.5 text-ink">{{ formatRelative(detail.nextAttemptAt) }}</dd>
        </div>
        <div v-if="detail.error" class="sm:col-span-2">
          <dt class="text-muted">{{ $t('notifications.errorLabel') }}</dt>
          <dd class="mt-0.5 break-all rounded bg-red-50 px-2 py-1 text-red-700">{{ detail.error }}</dd>
        </div>
      </dl>
      <template #footer>
        <Button @click="detail = null">{{ $t('common.close') }}</Button>
      </template>
    </Modal>
  </div>
</template>
