import { useI18n } from 'vue-i18n'

import type {
  AccountStatus,
  AccountType,
  AuditResult,
  CredentialType,
  IdDocumentType,
  LegalEntityType,
  OrgRole,
  RoleScope,
  VerificationState,
} from '@/api/types'
import type { Tone } from '@/components/ui/tone'

/** Chinese/English labels for every enum on the wire, plus badge tones. */
export function useLabels() {
  const { t } = useI18n()

  function accountStatus(value: AccountStatus): string {
    switch (value) {
      case 'active':
        return t('status.account.active')
      case 'locked':
        return t('status.account.locked')
      case 'disabled':
        return t('status.account.disabled')
    }
  }

  function accountStatusTone(value: AccountStatus): Tone {
    switch (value) {
      case 'active':
        return 'success'
      case 'locked':
        return 'warning'
      case 'disabled':
        return 'neutral'
    }
  }

  function verificationState(value: VerificationState): string {
    switch (value) {
      case 'pending':
        return t('status.verification.pending')
      case 'verified':
        return t('status.verification.verified')
      case 'rejected':
        return t('status.verification.rejected')
      case 'expired':
        return t('status.verification.expired')
    }
  }

  function verificationTone(value: VerificationState): Tone {
    switch (value) {
      case 'pending':
        return 'warning'
      case 'verified':
        return 'success'
      case 'rejected':
        return 'danger'
      case 'expired':
        return 'neutral'
    }
  }

  function realm(value: string): string {
    if (value === 'cn') return t('status.realm.cn')
    if (value === 'partner') return t('status.realm.partner')
    return value
  }

  function accountType(value: AccountType): string {
    switch (value) {
      case 'personal':
        return t('status.accountType.personal')
      case 'service':
        return t('status.accountType.service')
      case 'managed':
        return t('status.accountType.managed')
    }
  }

  function entityType(value: LegalEntityType): string {
    switch (value) {
      case 'natural':
        return t('status.entityType.natural')
      case 'legal':
        return t('status.entityType.legal')
      case 'gov':
        return t('status.entityType.gov')
      case 'other':
        return t('status.entityType.other')
    }
  }

  function documentType(value: IdDocumentType): string {
    switch (value) {
      case 'resident-id':
        return t('status.documentType.resident-id')
      case 'passport':
        return t('status.documentType.passport')
      case 'uscc':
        return t('status.documentType.uscc')
      case 'other':
        return t('status.documentType.other')
    }
  }

  function credentialType(value: CredentialType): string {
    switch (value) {
      case 'password':
        return t('status.credentialType.password')
      case 'totp':
        return t('status.credentialType.totp')
      case 'passkey':
        return t('status.credentialType.passkey')
      case 'recovery-code':
        return t('status.credentialType.recovery-code')
    }
  }

  function auditResult(value: AuditResult): string {
    switch (value) {
      case 'success':
        return t('status.result.success')
      case 'failure':
        return t('status.result.failure')
      case 'denied':
        return t('status.result.denied')
    }
  }

  function auditResultTone(value: AuditResult): Tone {
    switch (value) {
      case 'success':
        return 'success'
      case 'failure':
        return 'danger'
      case 'denied':
        return 'warning'
    }
  }

  function orgRole(value: OrgRole): string {
    switch (value) {
      case 'owner':
        return t('status.orgRole.owner')
      case 'admin':
        return t('status.orgRole.admin')
      case 'member':
        return t('status.orgRole.member')
      case 'viewer':
        return t('status.orgRole.viewer')
    }
  }

  function roleScope(value: RoleScope): string {
    switch (value) {
      case 'global':
        return t('status.roleScope.global')
      case 'app':
        return t('status.roleScope.app')
      case 'org':
        return t('status.roleScope.org')
    }
  }

  function auditAction(action: string): string {
    switch (action) {
      case 'account.created':
        return t('status.action.accountCreated')
      case 'account.disabled':
        return t('status.action.accountDisabled')
      case 'account.enabled':
        return t('status.action.accountEnabled')
      case 'account.locked':
        return t('status.action.accountLocked')
      case 'auth.login.succeeded':
        return t('status.action.loginSucceeded')
      case 'auth.login.failed':
        return t('status.action.loginFailed')
      case 'session.revoked':
        return t('status.action.sessionRevoked')
      case 'legal_entity.verified':
        return t('status.action.entityVerified')
      case 'legal_entity.rejected':
        return t('status.action.entityRejected')
      case 'legal_entity.pending':
        return t('status.action.entityPending')
      case 'role.updated':
        return t('status.action.roleUpdated')
      case 'application.created':
        return t('status.action.applicationCreated')
      case 'credential.totp.enrolled':
        return t('status.action.totpEnrolled')
      case 'credential.passkey.enrolled':
        return t('status.action.passkeyEnrolled')
      case 'organization.member.added':
        return t('status.action.memberAdded')
      case 'organization.member.updated':
        return t('status.action.memberUpdated')
      case 'organization.member.removed':
        return t('status.action.memberRemoved')
      default:
        return action
    }
  }

  return {
    accountStatus,
    accountStatusTone,
    verificationState,
    verificationTone,
    realm,
    accountType,
    entityType,
    documentType,
    credentialType,
    auditResult,
    auditResultTone,
    orgRole,
    roleScope,
    auditAction,
  }
}
