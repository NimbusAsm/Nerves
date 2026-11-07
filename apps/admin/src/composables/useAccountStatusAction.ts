import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { describeError, getApi } from '@/api'
import type { AccountSummary } from '@/api/types'
import { useToastStore } from '@/stores/toast'

export type AccountStatusMode = 'disable' | 'enable'

/**
 * Shared "disable / enable an account" flow with the reason prompt required by
 * the audit trail. The caller only supplies what to refresh afterwards.
 */
export function useAccountStatusAction(onDone: () => void | Promise<void>) {
  const { t } = useI18n()
  const toasts = useToastStore()
  const api = getApi()

  const target = ref<AccountSummary | null>(null)
  const mode = ref<AccountStatusMode>('disable')
  const reason = ref('')
  const saving = ref(false)
  const error = ref<string | null>(null)

  function open(account: AccountSummary, next: AccountStatusMode): void {
    target.value = account
    mode.value = next
    reason.value = ''
    error.value = null
  }

  function close(): void {
    if (saving.value) return
    target.value = null
  }

  async function confirm(): Promise<void> {
    const account = target.value
    if (!account) return

    saving.value = true
    error.value = null
    try {
      await api.setAccountStatus(
        account.id,
        mode.value === 'disable' ? 'disabled' : 'active',
        mode.value === 'disable' ? reason.value : undefined,
      )
      toasts.success(t('accounts.statusChanged'), account.username)
      target.value = null
      await onDone()
    } catch (cause) {
      error.value = describeError(cause)
    } finally {
      saving.value = false
    }
  }

  return { target, mode, reason, saving, error, open, close, confirm }
}
