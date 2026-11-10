import type { NervesApi } from './contract'
import { buildQuery } from '@/lib/url'
import type {
  AccountDetail,
  AccountQuery,
  AccountStatus,
  AccountSummary,
  Application,
  ApplicationInput,
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

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** RFC 9457 ProblemDetails surfaced as a typed error. */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail: string | undefined
  readonly traceId: string | undefined
  readonly fieldErrors: Record<string, string[]> | undefined

  constructor(status: number, title: string, detail?: string, traceId?: string, fieldErrors?: Record<string, string[]>) {
    super(detail ? `${title}: ${detail}` : title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    this.traceId = traceId
    this.fieldErrors = fieldErrors
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

/** Message suitable for an inline alert. */
export function describeError(error: unknown): string {
  if (isApiError(error)) return error.detail ?? error.title
  if (error instanceof Error) return error.message
  return '请求失败，请稍后重试。'
}

export interface HttpApiOptions {
  /** Base URL, e.g. `/api/v1`. */
  baseUrl?: string
  getToken?: () => string | null
  onUnauthorized?: () => void
  fetchImpl?: typeof fetch
}

export function createHttpApi(options: HttpApiOptions = {}): NervesApi {
  const baseUrl = (options.baseUrl ?? '/api/v1').replace(/\/$/, '')
  const getToken = options.getToken ?? (() => null)
  const request = createRequester(options.fetchImpl ?? globalThis.fetch, baseUrl, getToken, options.onUnauthorized)

  return {
    mode: 'live',

    login: (input: LoginInput) =>
      request<LoginResult>('/auth/login', { method: 'POST', body: input }),

    logout: async () => {
      await request<void>('/auth/logout', { method: 'POST', allowEmpty: true }).catch(() => undefined)
    },

    getStats: () => request<DashboardStats>('/stats'),

    listAccounts: (query: AccountQuery) =>
      request<CursorPage<AccountSummary>>(
        `/accounts${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
          q: query.q,
          'filter[status]': query.status,
          'filter[realm]': query.realm,
          sort: '-createdAt',
        })}`,
      ),

    getAccount: (id: string) => request<AccountDetail>(`/accounts/${encodeURIComponent(id)}`),

    setAccountStatus: (id: string, status: AccountStatus, reason?: string) =>
      request<AccountSummary>(`/accounts/${encodeURIComponent(id)}/status`, {
        method: 'PATCH',
        body: { status, reason },
      }),

    listAccountSessions: (accountId: string) =>
      request<{ items: SessionInfo[] }>(`/accounts/${encodeURIComponent(accountId)}/sessions`).then(
        (page) => page.items,
      ),

    revokeSession: (sessionId: string) =>
      request<void>(`/sessions/${encodeURIComponent(sessionId)}`, { method: 'DELETE', allowEmpty: true }),

    listAccountAudit: (accountId: string, query: CursorQuery) =>
      request<CursorPage<AuditEvent>>(
        `/accounts/${encodeURIComponent(accountId)}/audit-events${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
        })}`,
      ),

    listLegalEntities: (query: LegalEntityQuery) =>
      request<CursorPage<LegalEntitySummary>>(
        `/legal-entities${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
          q: query.q,
          'filter[verificationState]': query.verificationState,
          sort: '-createdAt',
        })}`,
      ),

    getLegalEntity: (id: string) =>
      request<LegalEntityDetail>(`/legal-entities/${encodeURIComponent(id)}`),

    listLegalEntityAccounts: (id: string, query: CursorQuery) =>
      request<CursorPage<AccountSummary>>(
        `/legal-entities/${encodeURIComponent(id)}/accounts${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
          sort: '-createdAt',
        })}`,
      ),

    verifyLegalEntity: (id: string, providerReference?: string) =>
      request<LegalEntityDetail>('/legal-entities/' + encodeURIComponent(id) + '/verify', {
        method: 'POST',
        body: { providerReference: providerReference ?? null },
      }),

    rejectLegalEntity: (id: string, providerReference?: string) =>
      request<LegalEntityDetail>('/legal-entities/' + encodeURIComponent(id) + '/reject', {
        method: 'POST',
        body: { providerReference: providerReference ?? null },
      }),

    expireLegalEntity: (id: string) =>
      request<LegalEntityDetail>('/legal-entities/' + encodeURIComponent(id) + '/expire', {
        method: 'POST',
        body: {},
      }),

    disableLegalEntity: (id: string) =>
      request<LegalEntityTransition>('/legal-entities/' + encodeURIComponent(id) + '/disable', {
        method: 'POST',
        body: {},
      }),

    enableLegalEntity: (id: string) =>
      request<LegalEntityTransition>('/legal-entities/' + encodeURIComponent(id) + '/enable', {
        method: 'POST',
        body: {},
      }),

    startPasskeyRegistration: (accountId) =>
      request<PasskeyRegistration>(
        '/accounts/' + encodeURIComponent(accountId) + '/mfa/passkey/start',
        { method: 'POST', body: {} },
      ),

    completePasskeyRegistration: (accountId, challengeId, attestation, nickname) =>
      request<PasskeyRegistrationResult>(
        '/accounts/' + encodeURIComponent(accountId) + '/mfa/passkey/complete',
        { method: 'POST', body: { challengeId, attestation, nickname } },
      ),

    removePasskeys: async (accountId) => {
      await request<void>('/accounts/' + encodeURIComponent(accountId) + '/mfa/passkey', {
        method: 'DELETE',
        allowEmpty: true,
      })
    },

    getIntegrationStatus: () => request<IntegrationStatus>('/admin/integrations'),

    updateLdapSettings: (patch: LdapSettingsPatch) =>
      request<IntegrationStatus['ldap']>('/admin/integrations/ldap', { method: 'PATCH', body: patch }),

    updatePdpSettings: (patch: PdpSettingsPatch) =>
      request<PdpStatus>('/admin/pdp', { method: 'PATCH', body: patch }),

    getPdpStatus: () => request<PdpStatus>('/admin/pdp'),

    listGroups: () => request<Group[]>('/groups'),

    createGroup: (input) => request<Group>('/groups', { method: 'POST', body: input }),

    deleteGroup: async (id) => {
      await request<void>('/groups/' + encodeURIComponent(id), { method: 'DELETE', allowEmpty: true })
    },

    listGroupMembers: (id) =>
      request<GroupMember[]>('/groups/' + encodeURIComponent(id) + '/members'),

    addGroupMember: async (id, accountId) => {
      await request<void>('/groups/' + encodeURIComponent(id) + '/members', {
        method: 'POST',
        body: { accountId },
        allowEmpty: true,
      })
    },

    removeGroupMember: async (id, accountId) => {
      await request<void>(
        '/groups/' + encodeURIComponent(id) + '/members/' + encodeURIComponent(accountId),
        { method: 'DELETE', allowEmpty: true },
      )
    },

    listOrganizations: () => request<Organization[]>('/organizations'),

    listOrganizationMembers: (organizationId: string) =>
      request<{ items: OrgMember[] }>(
        `/organizations/${encodeURIComponent(organizationId)}/members`,
      ).then((page) => page.items),

    addOrganizationMember: (organizationId: string, accountId: string, role: OrgRole) =>
      request<OrgMember>(`/organizations/${encodeURIComponent(organizationId)}/members`, {
        method: 'POST',
        body: { accountId, orgRole: role },
      }),

    updateOrganizationMember: (organizationId: string, memberId: string, role: OrgRole) =>
      request<OrgMember>(
        `/organizations/${encodeURIComponent(organizationId)}/members/${encodeURIComponent(memberId)}`,
        { method: 'PATCH', body: { orgRole: role } },
      ),

    removeOrganizationMember: (organizationId: string, memberId: string) =>
      request<void>(
        `/organizations/${encodeURIComponent(organizationId)}/members/${encodeURIComponent(memberId)}`,
        { method: 'DELETE', allowEmpty: true },
      ),

    listRoles: () => request<Role[]>('/roles'),

    listPermissions: () => request<{ items: Permission[] }>('/permissions').then((page) => page.items),

    updateRole: (id: string, input: { name: string; description: string; permissions: string[] }) =>
      request<Role>(`/roles/${encodeURIComponent(id)}`, { method: 'PATCH', body: input }),

    listApplications: () => request<Application[]>('/applications'),

    createApplication: (input: ApplicationInput) =>
      request<Application>('/applications', { method: 'POST', body: input }),

    listAuditEvents: (query: AuditQuery) =>
      request<CursorPage<AuditEvent>>(
        `/audit-events${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
          'filter[from]': query.from,
          'filter[to]': query.to,
          'filter[result]': query.result,
          actor: query.actor,
          action: query.action,
          sort: '-at',
        })}`,
      ),

    listNotifications: (query: NotificationQuery) =>
      request<CursorPage<NotificationRecord>>(
        `/notifications${buildQuery({
          limit: query.limit,
          cursor: query.cursor,
          status: query.status,
          to: query.to,
        })}`,
      ),

    getNotification: (id: string) =>
      request<NotificationRecord>(`/notifications/${encodeURIComponent(id)}`),

    sendNotificationEmail: (input: SendNotificationEmailInput) =>
      request<NotificationRecord>('/notifications/email', {
        method: 'POST',
        body: {
          to: input.to,
          template: input.template,
          values: input.values,
        },
      }),
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE'
  body?: unknown
  /** 204 responses, or endpoints whose body we do not care about. */
  allowEmpty?: boolean
}

function createRequester(
  fetchImpl: typeof fetch,
  baseUrl: string,
  getToken: () => string | null,
  onUnauthorized?: () => void,
) {
  return async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    if (options.body !== undefined) headers.set('Content-Type', 'application/json')

    let response: Response
    try {
      response = await fetchImpl(`${baseUrl}${path}`, {
        method: options.method ?? 'GET',
        headers,
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
        // The console authenticates with a Bearer token held in memory and has no
        // session cookie to send. `include` also forces the browser to require
        // Access-Control-Allow-Credentials, which the gateway does not grant, so
        // every cross-origin call would fail. Use `omit` until a BFF with cookies
        // (and a matching CORS policy) replaces the token flow.
        credentials: 'omit',
      })
    } catch (cause) {
      throw new ApiError(0, '网络错误', cause instanceof Error ? cause.message : '无法连接到管理 API。')
    }

    if (response.status === 401) onUnauthorized?.()

    if (!response.ok) throw await toApiError(response)
    if (options.allowEmpty || response.status === 204) return undefined as T
    return (await response.json()) as T
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    problem = null
  }

  const title = problem?.title ?? response.statusText ?? '请求失败'
  const detail = problem?.detail
  return new ApiError(response.status, title, detail, problem?.traceId, problem?.errors)
}
