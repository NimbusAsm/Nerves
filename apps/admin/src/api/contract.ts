import type {
  AccountDetail,
  AccountQuery,
  AccountStatus,
  AccountSummary,
  Application,
  ApplicationInput,
  ApplicationPatch,
  ApplicationQuery,
  AuditEvent,
  AuditQuery,
  CursorPage,
  CursorQuery,
  DashboardStats,
  Group,
  IntegrationStatus,
  LdapSettingsPatch,
  GroupMember,
  LegalEntityDetail,
  LegalEntityQuery,
  LegalEntitySummary,
  LegalEntityTransition,
  NotificationQuery,
  NotificationRecord,
  SendNotificationEmailInput,
  LoginInput,
  LoginResult,
  OrgMember,
  Organization,
  OrgRole,
  PasskeyRegistration,
  PdpStatus,
  PdpSettingsPatch,
  PasskeyRegistrationResult,
  Permission,
  Role,
  SessionInfo,
} from './types'

/**
 * The one surface every view talks to. `createHttpApi` implements it against
 * `/api/v1/*`; `createMockApi` implements it with an in-memory, deterministic
 * dataset so the console is usable before the services land.
 */
export interface NervesApi {
  readonly mode: 'mock' | 'live'

  login(input: LoginInput): Promise<LoginResult>
  logout(): Promise<void>
  getStats(): Promise<DashboardStats>

  listAccounts(query: AccountQuery): Promise<CursorPage<AccountSummary>>
  getAccount(id: string): Promise<AccountDetail>
  setAccountStatus(id: string, status: AccountStatus, reason?: string): Promise<AccountSummary>

  listAccountSessions(accountId: string): Promise<SessionInfo[]>
  revokeSession(sessionId: string): Promise<void>
  listAccountAudit(accountId: string, query: CursorQuery): Promise<CursorPage<AuditEvent>>

  listLegalEntities(query: LegalEntityQuery): Promise<CursorPage<LegalEntitySummary>>
  getLegalEntity(id: string): Promise<LegalEntityDetail>
  listLegalEntityAccounts(id: string, query: CursorQuery): Promise<CursorPage<AccountSummary>>
  verifyLegalEntity(id: string, providerReference?: string): Promise<LegalEntityDetail>
  rejectLegalEntity(id: string, providerReference?: string): Promise<LegalEntityDetail>
  expireLegalEntity(id: string): Promise<LegalEntityDetail>
  disableLegalEntity(id: string): Promise<LegalEntityTransition>
  enableLegalEntity(id: string): Promise<LegalEntityTransition>

  startPasskeyRegistration(accountId: string): Promise<PasskeyRegistration>
  completePasskeyRegistration(
    accountId: string,
    challengeId: string,
    attestation: unknown,
    nickname?: string,
  ): Promise<PasskeyRegistrationResult>
  removePasskeys(accountId: string): Promise<void>

  getIntegrationStatus(): Promise<IntegrationStatus>
  updateLdapSettings(patch: LdapSettingsPatch): Promise<IntegrationStatus['ldap']>
  updatePdpSettings(patch: PdpSettingsPatch): Promise<PdpStatus>
  getPdpStatus(): Promise<PdpStatus>

  listGroups(): Promise<Group[]>
  createGroup(input: { realm: string; code: string; name: string }): Promise<Group>
  deleteGroup(id: string): Promise<void>
  listGroupMembers(id: string): Promise<GroupMember[]>
  addGroupMember(id: string, accountId: string): Promise<void>
  removeGroupMember(id: string, accountId: string): Promise<void>

  listOrganizations(): Promise<Organization[]>
  listOrganizationMembers(organizationId: string): Promise<OrgMember[]>
  addOrganizationMember(organizationId: string, accountId: string, role: OrgRole): Promise<OrgMember>
  updateOrganizationMember(organizationId: string, memberId: string, role: OrgRole): Promise<OrgMember>
  removeOrganizationMember(organizationId: string, memberId: string): Promise<void>

  listRoles(): Promise<Role[]>
  listPermissions(): Promise<Permission[]>
  updateRole(id: string, input: { name: string; description: string; permissions: string[] }): Promise<Role>

  listApplications(query?: ApplicationQuery): Promise<CursorPage<Application>>
  getApplication(id: string): Promise<Application>
  createApplication(input: ApplicationInput): Promise<Application>
  updateApplication(id: string, patch: ApplicationPatch): Promise<Application>
  deleteApplication(id: string): Promise<void>

  listAuditEvents(query: AuditQuery): Promise<CursorPage<AuditEvent>>

  /** Delivery log of the notification service, newest first. */
  listNotifications(query: NotificationQuery): Promise<CursorPage<NotificationRecord>>
  getNotification(id: string): Promise<NotificationRecord>
  sendNotificationEmail(input: SendNotificationEmailInput): Promise<NotificationRecord>
}
