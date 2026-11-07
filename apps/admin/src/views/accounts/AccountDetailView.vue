<script setup lang="ts">
import { KeyRound, Lock, Monitor, ShieldCheck } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'

import { ApiError, describeError, getApi } from '@/api'
import type { AccountDetail, AuditEvent, CredentialType, SessionInfo } from '@/api/types'
import PageHeader from '@/components/PageHeader.vue'
import Badge from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import Modal from '@/components/ui/Modal.vue'
import Table from '@/components/ui/Table.vue'
import TableMessage from '@/components/ui/TableMessage.vue'
import Textarea from '@/components/ui/Textarea.vue'
import { useAccountStatusAction } from '@/composables/useAccountStatusAction'
import { useLabels } from '@/composables/useLabels'
import { formatDate, formatDateTime, formatRelative, initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const { t } = useI18n()
const labels = useLabels()
const auth = useAuthStore()
const toasts = useToastStore()
const api = getApi()
const route = useRoute()

const accountId = computed(() => String(route.params.id ?? ''))

const account = ref<AccountDetail | null>(null)
const sessions = ref<SessionInfo[]>([])
const events = ref<AuditEvent[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const canWrite = computed(() => auth.can('identity:account:write'))
const canRevoke = computed(() => auth.can('session:session:revoke'))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    account.value = await api.getAccount(accountId.value)
    const [sessionPage, auditPage] = await Promise.all([
      api.listAccountSessions(accountId.value),
      api.listAccountAudit(accountId.value, { limit: 8 }),
    ])
    sessions.value = sessionPage
    events.value = auditPage.items
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      error.value = t('errors.notFoundDescription')
    } else {
      error.value = describeError(cause)
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)

const statusAction = useAccountStatusAction(load)

const sessionToRevoke = ref<SessionInfo | null>(null)
const revoking = ref(false)
const revokeError = ref<string | null>(null)

function askRevoke(session: SessionInfo): void {
  sessionToRevoke.value = session
  revokeError.value = null
}

async function confirmRevoke(): Promise<void> {
  const session = sessionToRevoke.value
  if (!session) return
  revoking.value = true
  revokeError.value = null
  try {
    await api.revokeSession(session.id)
    toasts.success(t('accounts.sessionRevoked'), session.device)
    sessionToRevoke.value = null
    await load()
  } catch (cause) {
    revokeError.value = describeError(cause)
  } finally {
    revoking.value = false
  }
}

const statusDialogTitle = computed(() =>
  statusAction.mode.value === 'disable' ? t('accounts.disableTitle') : t('accounts.enableTitle'),
)

function amrLabel(value: string): string {
  if (value === 'pwd') return labels.credentialType('password' satisfies CredentialType)
  if (value === 'otp') return labels.credentialType('totp' satisfies CredentialType)
  if (value === 'passkey') return labels.credentialType('passkey' satisfies CredentialType)
  return value
}

const customFields = computed(() => Object.entries(account.value?.customFields ?? {}))
</script>

<template>
  <div>
    <PageHeader
      :title="account?.displayName ?? $t('accounts.detailTitle')"
      :description="account ? `${account.username} · ${account.email}` : $t('accounts.detailDescription')"
      :back-to="{ name: 'accounts' }"
      :back-label="$t('accounts.backToList')"
    >
      <template #actions>
        <Badge v-if="account" :tone="labels.accountStatusTone(account.status)">
          {{ labels.accountStatus(account.status) }}
        </Badge>
        <Button v-if="account && canWrite" :loading="loading" @click="statusAction.open(account, account.status === 'disabled' ? 'enable' : 'disable')">
          {{ account.status === 'disabled' ? $t('accounts.enable') : $t('accounts.disable') }}
        </Button>
      </template>
    </PageHeader>

    <div
      v-if="error"
      class="flex flex-wrap items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
      role="alert"
    >
      <span>{{ error }}</span>
      <Button size="sm" @click="load">{{ $t('common.retry') }}</Button>
    </div>

    <div v-else-if="loading" class="rounded-lg border border-line bg-surface p-10 text-center text-sm text-muted">
      {{ $t('common.loading') }}
    </div>

    <template v-else-if="account">
      <div class="grid gap-4 xl:grid-cols-2">
        <Card :title="$t('accounts.basicInfo')">
          <div class="flex items-center gap-3">
            <span class="grid h-11 w-11 place-items-center rounded-full bg-brand-soft text-sm font-semibold text-brand">
              {{ initials(account.displayName) }}
            </span>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-ink">{{ account.displayName }}</p>
              <p class="mono truncate text-muted">{{ account.id }}</p>
            </div>
          </div>

          <dl class="mt-4 grid grid-cols-1 gap-x-6 gap-y-2.5 text-xs sm:grid-cols-2">
            <div>
              <dt class="text-muted">{{ $t('common.username') }}</dt>
              <dd class="mono mt-0.5 text-ink">{{ account.username }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.emailLabel') }}</dt>
              <dd class="mt-0.5 truncate text-ink">{{ account.email }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.phoneLabel') }}</dt>
              <dd class="mono mt-0.5 text-ink">{{ account.phone }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.typeLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ labels.accountType(account.type) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.realmLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ labels.realm(account.realm) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.departmentLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ account.department ?? '—' }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.titleLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ account.title ?? '—' }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.createdLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ formatDateTime(account.createdAt) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('accounts.lastLoginLabel') }}</dt>
              <dd class="mt-0.5 text-ink">{{ formatDateTime(account.lastLoginAt) }}</dd>
            </div>
            <div v-if="account.disabledReason">
              <dt class="text-muted">{{ $t('accounts.disabledReasonLabel') }}</dt>
              <dd class="mt-0.5 text-red-600">{{ account.disabledReason }}</dd>
            </div>
          </dl>

          <div v-if="customFields.length > 0" class="mt-4 border-t border-line pt-3">
            <p class="text-xs font-medium text-ink">{{ $t('accounts.customFields') }}</p>
            <dl class="mt-2 grid grid-cols-1 gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
              <div v-for="[key, value] in customFields" :key="key">
                <dt class="mono text-muted">{{ key }}</dt>
                <dd class="mt-0.5 text-ink">{{ value }}</dd>
              </div>
            </dl>
          </div>
        </Card>

        <Card :title="$t('accounts.entityInfo')">
          <RouterLink
            :to="{ name: 'legal-entity-detail', params: { id: account.legalEntity.id } }"
            class="text-sm font-medium text-ink hover:text-brand"
          >
            {{ account.legalEntity.displayName }}
          </RouterLink>
          <div class="mt-2 flex flex-wrap items-center gap-2">
            <Badge :tone="labels.verificationTone(account.legalEntity.verificationState)" size="sm">
              {{ labels.verificationState(account.legalEntity.verificationState) }}
            </Badge>
            <span class="text-xs text-muted">{{ labels.entityType(account.legalEntity.entityType) }}</span>
          </div>

          <dl class="mt-4 grid grid-cols-1 gap-x-6 gap-y-2.5 text-xs sm:grid-cols-2">
            <div>
              <dt class="text-muted">{{ $t('entities.documentType') }}</dt>
              <dd class="mt-0.5 text-ink">{{ labels.documentType(account.legalEntity.idDocType) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.verifiedAt') }}</dt>
              <dd class="mt-0.5 text-ink">{{ formatDateTime(account.legalEntity.verifiedAt) }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.country') }}</dt>
              <dd class="mt-0.5 text-ink">{{ account.legalEntity.country }}</dd>
            </div>
            <div>
              <dt class="text-muted">{{ $t('entities.accountCount') }}</dt>
              <dd class="mt-0.5 text-ink">{{ account.legalEntity.accountCount }}</dd>
            </div>
          </dl>

          <div class="mt-4 flex items-center gap-3 border-t border-line pt-3">
            <ShieldCheck class="h-4 w-4 text-emerald-600" aria-hidden="true" />
            <p class="flex-1 text-[11px] leading-relaxed text-muted">{{ $t('entities.digestHint') }}</p>
          </div>
          <RouterLink
            :to="{ name: 'legal-entity-detail', params: { id: account.legalEntity.id } }"
            class="mt-2 inline-block text-xs text-brand hover:underline"
          >
            {{ $t('accounts.viewEntity') }}
          </RouterLink>
        </Card>

        <Card :title="$t('accounts.credentialInfo')">
          <div class="mb-3 flex items-center gap-2">
            <Badge :tone="account.mfa ? 'success' : 'warning'" size="sm">
              {{ account.mfa ? $t('accounts.mfaEnabled') : $t('accounts.mfaDisabled') }}
            </Badge>
          </div>
          <ul class="divide-y divide-line">
            <li v-for="credential in account.credentials" :key="credential.type" class="flex items-start gap-3 py-2.5 first:pt-0 last:pb-0">
              <span class="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-canvas text-muted">
                <Lock v-if="credential.type === 'password'" class="h-4 w-4" aria-hidden="true" />
                <KeyRound v-else class="h-4 w-4" aria-hidden="true" />
              </span>
              <div class="min-w-0 flex-1">
                <p class="text-xs font-medium text-ink">{{ labels.credentialType(credential.type) }}</p>
                <p class="mt-0.5 text-[11px] text-muted">
                  {{ formatDate(credential.enrolledAt) }}
                  <span v-if="credential.lastUsedAt"> · {{ $t('accounts.credentialLastUsed') }} {{ formatRelative(credential.lastUsedAt) }}</span>
                </p>
              </div>
              <Badge tone="success" size="sm">{{ $t('accounts.credentialEnrolled') }}</Badge>
            </li>
          </ul>
        </Card>

        <Card :title="$t('accounts.sessionsTitle')" :description="$t('accounts.sessionsDescription')" :padded="false">
          <Table dense>
            <thead>
              <tr>
                <th scope="col">{{ $t('common.device') }}</th>
                <th scope="col">{{ $t('common.amr') }}</th>
                <th scope="col">{{ $t('common.lastSeen') }}</th>
                <th scope="col">{{ $t('common.expiresAt') }}</th>
                <th scope="col" class="text-right">{{ $t('common.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <TableMessage
                :colspan="5"
                :empty="sessions.length === 0"
                :empty-text="$t('accounts.noSessions')"
              />
              <tr v-for="session in sessions" :key="session.id">
                <td>
                  <div class="flex items-center gap-2">
                    <Monitor class="h-4 w-4 shrink-0 text-muted" aria-hidden="true" />
                    <div class="min-w-0">
                      <p class="truncate text-xs text-ink">{{ session.device }}</p>
                      <p class="mono truncate text-[10px] text-muted">{{ session.ipDigest }}</p>
                    </div>
                  </div>
                </td>
                <td class="text-xs text-muted">{{ session.amr.map(amrLabel).join(' + ') }}</td>
                <td class="whitespace-nowrap text-xs text-muted" :title="formatDateTime(session.lastSeenAt)">
                  {{ formatRelative(session.lastSeenAt) }}
                </td>
                <td class="whitespace-nowrap text-xs text-muted">{{ formatDate(session.absoluteExpiresAt) }}</td>
                <td class="text-right">
                  <Button v-if="canRevoke" size="sm" variant="ghost" class="text-red-600 hover:bg-red-50" @click="askRevoke(session)">
                    {{ $t('accounts.revoke') }}
                  </Button>
                </td>
              </tr>
            </tbody>
          </Table>
        </Card>
      </div>

      <Card
        class="mt-4"
        :title="$t('accounts.auditTitle')"
        :description="$t('accounts.auditDescription')"
      >
        <template #actions>
          <RouterLink :to="{ name: 'audit' }" class="text-xs text-brand hover:underline">
            {{ $t('accounts.viewAllAudit') }}
          </RouterLink>
        </template>

        <p v-if="events.length === 0" class="py-6 text-center text-sm text-muted">{{ $t('accounts.noAudit') }}</p>
        <ol v-else class="relative space-y-4 border-l border-line pl-4">
          <li v-for="event in events" :key="event.id" class="relative">
            <span
              class="absolute -left-[21px] top-1.5 h-2 w-2 rounded-full ring-2 ring-surface"
              :class="event.result === 'success' ? 'bg-emerald-500' : event.result === 'failure' ? 'bg-red-500' : 'bg-amber-500'"
              aria-hidden="true"
            />
            <div class="flex flex-wrap items-center gap-2">
              <p class="text-xs font-medium text-ink">{{ labels.auditAction(event.action) }}</p>
              <Badge :tone="labels.auditResultTone(event.result)" size="sm">{{ labels.auditResult(event.result) }}</Badge>
              <span class="text-[11px] text-muted" :title="formatDateTime(event.at)">{{ formatRelative(event.at) }}</span>
            </div>
            <p class="mt-0.5 text-[11px] leading-relaxed text-muted">
              {{ event.actorName }} · {{ event.detail }}
            </p>
          </li>
        </ol>
      </Card>
    </template>

    <Modal
      :open="statusAction.target.value !== null"
      :title="statusDialogTitle"
      :description="statusAction.mode.value === 'disable' ? $t('accounts.disableDescription') : $t('accounts.enableDescription')"
      @update:open="(value) => !value && statusAction.close()"
    >
      <div class="space-y-3">
        <div class="rounded-md border border-line bg-canvas/60 px-3 py-2">
          <p class="text-xs font-medium text-ink">{{ statusAction.target.value?.displayName }}</p>
          <p class="mono mt-0.5 text-muted">{{ statusAction.target.value?.username }}</p>
        </div>
        <div v-if="statusAction.mode.value === 'disable'">
          <label for="detail-disable-reason" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('accounts.disableReason') }}</label>
          <Textarea
            id="detail-disable-reason"
            v-model="statusAction.reason.value"
            :rows="3"
            :placeholder="$t('accounts.disableReasonPlaceholder')"
            :invalid="Boolean(statusAction.error.value)"
          />
        </div>
        <p v-if="statusAction.error.value" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
          {{ statusAction.error.value }}
        </p>
      </div>
      <template #footer>
        <Button :disabled="statusAction.saving.value" @click="statusAction.close()">{{ $t('common.cancel') }}</Button>
        <Button
          :variant="statusAction.mode.value === 'disable' ? 'danger' : 'primary'"
          :loading="statusAction.saving.value"
          @click="statusAction.confirm()"
        >
          {{ statusAction.mode.value === 'disable' ? $t('accounts.disableConfirm') : $t('accounts.enableConfirm') }}
        </Button>
      </template>
    </Modal>

    <Modal
      :open="sessionToRevoke !== null"
      :title="$t('accounts.revokeTitle')"
      :description="$t('accounts.revokeDescription')"
      size="sm"
      @update:open="(value) => !value && (sessionToRevoke = null)"
    >
      <div v-if="sessionToRevoke" class="rounded-md border border-line bg-canvas/60 px-3 py-2">
        <p class="text-xs text-ink">{{ sessionToRevoke.device }}</p>
        <p class="mono mt-0.5 text-muted">{{ sessionToRevoke.ipDigest }}</p>
      </div>
      <p v-if="revokeError" class="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700" role="alert">
        {{ revokeError }}
      </p>
      <template #footer>
        <Button :disabled="revoking" @click="sessionToRevoke = null">{{ $t('common.cancel') }}</Button>
        <Button variant="danger" :loading="revoking" @click="confirmRevoke">{{ $t('accounts.revokeConfirm') }}</Button>
      </template>
    </Modal>
  </div>
</template>
