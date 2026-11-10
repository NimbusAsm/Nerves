import { localDateKey } from '@/lib/format'
import { missingTemplateValues, normalizeTemplateValues, NOTIFICATION_TEMPLATES } from '@/lib/notification-templates'
import type { NervesApi } from './contract'
import { ApiError } from './http'
import type {
  Group,
  IntegrationStatus,
  PdpStatus,
  PasskeyRegistration,
  PasskeyRegistrationResult,
  GroupMember,
  NotificationQuery,
  NotificationRecord,
  NotificationStatus,
  SendNotificationEmailInput,
  AccountDetail,
  AccountQuery,
  AccountStatus,
  AccountSummary,
  AccountType,
  ActorType,
  Application,
  ApplicationInput,
  AuditEvent,
  AuditQuery,
  AuditResult,
  CredentialStatus,
  CursorPage,
  CursorQuery,
  DashboardStats,
  IdDocumentType,
  LegalEntityDetail,
  LegalEntityQuery,
  LegalEntitySummary,
  LegalEntityType,
  LoginInput,
  LoginResult,
  OrgMember,
  Organization,
  OrgRole,
  Permission,
  Role,
  RoleScope,
  SessionInfo,
  VerificationState,
} from './types'

/**
 * In-memory implementation of the admin API.
 *
 * The dataset is deterministic for a given seed so screenshots, tests and manual
 * QA all see the same thing. Switch to the real services with
 * `VITE_USE_MOCK=false` — every view only depends on the `NervesApi` contract.
 */
export interface MockApiOptions {
  latencyMs?: number
  seed?: number
  now?: () => Date
}

const BASE32 = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'

interface DemoUser {
  id: string
  username: string
  password: string
  displayName: string
  email: string
  roles: string[]
}

const DEMO_USERS: DemoUser[] = [
  {
    id: 'usr_platform_admin',
    username: 'admin',
    password: 'nerves2026',
    displayName: '平台管理员',
    email: 'admin@nerves.example',
    roles: ['platform-admin'],
  },
  {
    id: 'usr_compliance_auditor',
    username: 'auditor',
    password: 'nerves2026',
    displayName: '合规审计员',
    email: 'auditor@nerves.example',
    roles: ['platform-auditor'],
  },
]

const PERMISSIONS: Permission[] = [
  { code: 'identity:account:read', appName: '身份服务', description: '读取账户资料与凭据状态' },
  { code: 'identity:account:write', appName: '身份服务', description: '停用或启用账户' },
  { code: 'identity:legal-entity:read', appName: '身份服务', description: '读取法人实体与实名信息' },
  { code: 'identity:legal-entity:verify', appName: '身份服务', description: '提交或复核实名认证结果' },
  { code: 'identity:credential:revoke', appName: '身份服务', description: '吊销账户凭据' },
  { code: 'session:session:read', appName: '会话服务', description: '查看账户会话' },
  { code: 'session:session:revoke', appName: '会话服务', description: '强制下线指定会话' },
  { code: 'authz:role:read', appName: '授权服务', description: '读取角色与权限目录' },
  { code: 'authz:role:write', appName: '授权服务', description: '编辑角色与授权' },
  { code: 'org:organization:read', appName: '组织服务', description: '读取组织与成员' },
  { code: 'org:organization:write', appName: '组织服务', description: '编辑组织成员与角色' },
  { code: 'audit:event:read', appName: '审计服务', description: '查询审计事件' },
  { code: 'audit:event:export', appName: '审计服务', description: '导出审计事件' },
  { code: 'directory:application:read', appName: '目录服务', description: '读取应用目录' },
  { code: 'directory:application:write', appName: '目录服务', description: '创建与修改应用' },
  { code: 'stats:read', appName: '平台运维', description: '读取概览统计' },
  { code: 'platform:signing-key:read', appName: '平台运维', description: '读取签名密钥元数据' },
  { code: 'platform:routing-index:read', appName: '平台运维', description: '读取路由索引（受限）' },
]

const ALL_PERMISSION_CODES = PERMISSIONS.map((permission) => permission.code)

const ADMIN_PERMISSIONS = [...ALL_PERMISSION_CODES, 'nerves:admin']
const AUDITOR_PERMISSIONS = [
  'stats:read',
  'identity:account:read',
  'identity:legal-entity:read',
  'session:session:read',
  'authz:role:read',
  'org:organization:read',
  'audit:event:read',
  'audit:event:export',
  'directory:application:read',
]

const PEOPLE = [
  ['张伟', 'zhang.wei'],
  ['王芳', 'wang.fang'],
  ['李强', 'li.qiang'],
  ['刘洋', 'liu.yang'],
  ['陈静', 'chen.jing'],
  ['杨帆', 'yang.fan'],
  ['赵磊', 'zhao.lei'],
  ['黄敏', 'huang.min'],
  ['周杰', 'zhou.jie'],
  ['吴桐', 'wu.tong'],
  ['徐晶', 'xu.jing'],
  ['孙浩', 'sun.hao'],
  ['马丽', 'ma.li'],
  ['朱峰', 'zhu.feng'],
  ['胡楠', 'hu.nan'],
  ['郭爽', 'guo.shuang'],
  ['何雨', 'he.yu'],
  ['高翔', 'gao.xiang'],
  ['林墨', 'lin.mo'],
  ['罗宾', 'luo.bin'],
  ['郑珂', 'zheng.ke'],
  ['梁辰', 'liang.chen'],
  ['谢宁', 'xie.ning'],
  ['宋佳', 'song.jia'],
  ['唐棠', 'tang.tang'],
  ['许愿', 'xu.yuan'],
  ['邓超', 'deng.chao'],
  ['冯雪', 'feng.xue'],
  ['韩哲', 'han.zhe'],
  ['曹颖', 'cao.ying'],
  ['彭越', 'peng.yue'],
  ['董然', 'dong.ran'],
  ['袁野', 'yuan.ye'],
  ['蒋薇', 'jiang.wei'],
  ['余杭', 'yu.hang'],
  ['杜若', 'du.ruo'],
  ['叶青', 'ye.qing'],
  ['程远', 'cheng.yuan'],
  ['苏芮', 'su.rui'],
  ['吕蒙', 'lv.meng'],
  ['丁一', 'ding.yi'],
  ['沈括', 'shen.kuo'],
  ['钟离', 'zhong.li'],
  ['谭雅', 'tan.ya'],
  ['陆沉', 'lu.chen'],
  ['白露', 'bai.lu'],
  ['顾言', 'gu.yan'],
  ['龙舟', 'long.zhou'],
] as const

const SERVICE_NAMES = [
  ['计费对账服务', 'svc.billing'],
  ['消息推送服务', 'svc.notification'],
  ['数据同步服务', 'svc.sync'],
  ['审计归档服务', 'svc.audit'],
  ['目录同步服务', 'svc.directory'],
  ['报表生成服务', 'svc.report'],
  ['网关探针', 'svc.gateway'],
  ['备份任务', 'svc.backup'],
] as const

const DEPARTMENTS = ['平台研发部', '安全合规部', '客户成功部', '数据平台部', '市场运营部']
const TITLES = ['工程师', '高级工程师', '技术专家', '产品经理', '运营经理', '安全工程师']
const DEVICES = [
  'Chrome 141 · macOS 15.7',
  'Edge 141 · Windows 11',
  'Safari 26.1 · macOS 15.7',
  'Firefox 145 · Ubuntu 24.04',
  'Nerves App 3.2 · iOS 19.2',
  'Nerves App 3.2 · Android 16',
]
const DISABLED_REASONS = ['员工离职', '安全事件应急响应', '客户申请注销', '长期未登录自动归档']

interface EntitySeed {
  displayName: string
  entityType: LegalEntityType
  country: string
  idDocType: IdDocumentType
  verificationState: VerificationState
  providerRef: string | null
}

const ENTITY_SEEDS: EntitySeed[] = [
  {
    displayName: '云矩数阁网络科技（贵州省）有限责任公司',
    entityType: 'legal',
    country: 'CN',
    idDocType: 'uscc',
    verificationState: 'verified',
    providerRef: 'compliance-cn:verify-2026-0417-8821',
  },
  {
    displayName: '贵州省数字产业集团',
    entityType: 'gov',
    country: 'CN',
    idDocType: 'uscc',
    verificationState: 'verified',
    providerRef: 'compliance-cn:verify-2026-0302-1140',
  },
  {
    displayName: '陈知远',
    entityType: 'natural',
    country: 'CN',
    idDocType: 'resident-id',
    verificationState: 'verified',
    providerRef: 'compliance-cn:verify-2026-0519-3302',
  },
  {
    displayName: '李佩珊',
    entityType: 'natural',
    country: 'CN',
    idDocType: 'resident-id',
    verificationState: 'pending',
    providerRef: null,
  },
  {
    displayName: '王砚秋',
    entityType: 'natural',
    country: 'CN',
    idDocType: 'passport',
    verificationState: 'expired',
    providerRef: 'compliance-cn:verify-2025-1120-7715',
  },
  {
    displayName: '深圳前海云梭科技有限公司',
    entityType: 'legal',
    country: 'CN',
    idDocType: 'uscc',
    verificationState: 'rejected',
    providerRef: 'compliance-cn:reject-2026-0808-2044',
  },
  {
    displayName: '杭州云枢数据服务有限公司',
    entityType: 'legal',
    country: 'CN',
    idDocType: 'uscc',
    verificationState: 'verified',
    providerRef: 'compliance-cn:verify-2026-0211-6650',
  },
  {
    displayName: '赵逸舟',
    entityType: 'natural',
    country: 'CN',
    idDocType: 'resident-id',
    verificationState: 'verified',
    providerRef: 'compliance-cn:verify-2026-0703-9917',
  },
]

interface RoleSeed {
  id: string
  code: string
  name: string
  description: string
  scope: RoleScope
  appName: string | null
  builtIn: boolean
  permissionCodes: string[]
  memberCount: number
}

const ROLE_SEEDS: RoleSeed[] = [
  {
    id: 'role_platform_admin',
    code: 'platform-admin',
    name: '平台管理员',
    description: '账户中心全部管理权限，含密钥与路由索引的受限读取。',
    scope: 'global',
    appName: null,
    builtIn: true,
    permissionCodes: ALL_PERMISSION_CODES,
    memberCount: 2,
  },
  {
    id: 'role_platform_auditor',
    code: 'platform-auditor',
    name: '合规审计员',
    description: '只读账户、会话与审计数据，可导出审计事件。',
    scope: 'global',
    appName: null,
    builtIn: true,
    permissionCodes: AUDITOR_PERMISSIONS,
    memberCount: 3,
  },
  {
    id: 'role_account_admin',
    code: 'account-admin',
    name: '账户管理员',
    description: '管理指定应用的账户停用/启用与凭据吊销。',
    scope: 'app',
    appName: 'Nerves Console',
    builtIn: false,
    permissionCodes: [
      'identity:account:read',
      'identity:account:write',
      'identity:credential:revoke',
      'session:session:read',
    ],
    memberCount: 5,
  },
  {
    id: 'role_app_developer',
    code: 'app-developer',
    name: '应用开发者',
    description: '读取目录、申请应用并维护回调地址。',
    scope: 'app',
    appName: '开放数据门户',
    builtIn: false,
    permissionCodes: [
      'directory:application:read',
      'directory:application:write',
      'identity:account:read',
    ],
    memberCount: 8,
  },
  {
    id: 'role_support_agent',
    code: 'support-agent',
    name: '客服专员',
    description: '定位用户问题所需的只读权限与强制下线。',
    scope: 'global',
    appName: null,
    builtIn: false,
    permissionCodes: [
      'identity:account:read',
      'session:session:read',
      'session:session:revoke',
      'org:organization:read',
      'audit:event:read',
    ],
    memberCount: 6,
  },
  {
    id: 'role_org_owner',
    code: 'org-owner',
    name: '组织所有者',
    description: '在所属组织范围内维护成员与角色。',
    scope: 'org',
    appName: null,
    builtIn: false,
    permissionCodes: [
      'org:organization:read',
      'org:organization:write',
      'identity:account:read',
      'identity:legal-entity:read',
    ],
    memberCount: 4,
  },
]

interface OrgSeed {
  id: string
  code: string
  name: string
  description: string
}

const ORG_SEEDS: OrgSeed[] = [
  { id: 'org_platform', code: 'PLATFORM', name: '平台运营中心', description: '账户中心的产品、研发与运维团队。' },
  { id: 'org_cloud', code: 'CLOUD', name: '云服务事业部', description: '云虚机、对象存储与开放数据门户的运营主体。' },
  { id: 'org_security', code: 'SECURITY', name: '安全合规部', description: '等级保护、实名合规与审计响应。' },
  { id: 'org_opensource', code: 'OSS', name: 'Nerves 开源项目组', description: '开源框架的维护者与外部贡献者。' },
]

interface AppSeed {
  id: string
  name: string
  description: string
  clientId: string
  logoUrl: string | null
  redirectUris: string[]
  scopes: string[]
  status: 'active' | 'disabled'
  createdAtDaysAgo: number
  lastRotatedDaysAgo: number | null
}

const APP_SEEDS: AppSeed[] = [
  {
    id: 'app_console',
    name: 'Nerves Console',
    description: '账户中心自带的管理后台，使用授权码 + PKCE 登录。',
    clientId: 'nerves-console',
    logoUrl:
      "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='8' fill='%231f6feb'/%3E%3Cpath d='M9 23V9h3.6l6.8 9.2V9H23v14h-3.6l-6.8-9.2V23z' fill='%23fff'/%3E%3C/svg%3E",
    redirectUris: ['https://console.nerves.example/connect/callback'],
    scopes: ['openid', 'profile', 'email', 'offline_access'],
    status: 'active',
    createdAtDaysAgo: 420,
    lastRotatedDaysAgo: 63,
  },
  {
    id: 'app_website',
    name: '云矩数阁官网',
    description: '集团门户的单点登录接入，登录后跳回站点会话。',
    clientId: 'crequency-site',
    logoUrl: null,
    redirectUris: ['https://www.crequency.cn/auth/callback', 'https://crequency.cn/auth/callback'],
    scopes: ['openid', 'profile'],
    status: 'active',
    createdAtDaysAgo: 310,
    lastRotatedDaysAgo: 21,
  },
  {
    id: 'app_portal',
    name: '开放数据门户',
    description: '面向合作伙伴的数据目录，按组织授权访问数据集。',
    clientId: 'open-data-portal',
    logoUrl: null,
    redirectUris: ['https://data.nerves.example/oauth/callback'],
    scopes: ['openid', 'profile', 'openid:data.read'],
    status: 'active',
    createdAtDaysAgo: 180,
    lastRotatedDaysAgo: 180,
  },
  {
    id: 'app_cli',
    name: 'Nerves CLI',
    description: '命令行工具的设备码登录（device_code）客户端。',
    clientId: 'nerves-cli',
    logoUrl: null,
    redirectUris: ['http://127.0.0.1:41731/device/callback'],
    scopes: ['openid', 'offline_access'],
    status: 'active',
    createdAtDaysAgo: 96,
    lastRotatedDaysAgo: 96,
  },
  {
    id: 'app_docs',
    name: 'Nerves API 文档',
    description: '文档站的示例应用，用于演示授权码流程。',
    clientId: 'nerves-docs',
    logoUrl: null,
    redirectUris: ['https://docs.nerves.example/oauth/callback'],
    scopes: ['openid', 'profile'],
    status: 'active',
    createdAtDaysAgo: 58,
    lastRotatedDaysAgo: null,
  },
  {
    id: 'app_partner',
    name: '合作伙伴接入（测试）',
    description: '沙箱应用，合作伙伴联调期间使用，当前已停用。',
    clientId: 'partner-sandbox',
    logoUrl: null,
    redirectUris: ['https://partner.example.com/sso/callback'],
    scopes: ['openid', 'profile', 'email'],
    status: 'disabled',
    createdAtDaysAgo: 140,
    lastRotatedDaysAgo: 30,
  },
]

const LOGIN_ACTION = 'auth.login.succeeded'
const LOGIN_FAILED_ACTION = 'auth.login.failed'

export function createMockApi(options: MockApiOptions = {}): NervesApi {
  const latency = options.latencyMs ?? 140
  const seed = options.seed ?? 20261107
  const clock = options.now ?? (() => new Date())
  const rand = mulberry32(seed)
  const state = seedState({ rand, now: clock() })

  const delay = async () => {
    if (latency <= 0) return
    await new Promise((resolve) => setTimeout(resolve, latency))
  }

  const requireEntity = (id: string) => {
    const entity = state.entities.find((item) => item.id === id)
    if (!entity) throw new ApiError(404, '法人实体不存在', `未找到实体 ${id}。`)
    return entity
  }

  const requireAccount = (id: string) => {
    const account = state.accounts.find((item) => item.id === id)
    if (!account) throw new ApiError(404, '账户不存在', `未找到账户 ${id}。`)
    return account
  }

  const requireRole = (id: string) => {
    const role = state.roles.find((item) => item.id === id)
    if (!role) throw new ApiError(404, '角色不存在', `未找到角色 ${id}。`)
    return role
  }

  const requireOrganization = (id: string) => {
    const organization = state.organizations.find((item) => item.id === id)
    if (!organization) throw new ApiError(404, '组织不存在', `未找到组织 ${id}。`)
    return organization
  }

  const audit = (event: NewAuditEvent) => {
    state.auditEvents.push(makeAuditEvent({ ...event, at: event.at ?? clock() }, rand))
    state.auditEvents.sort((left, right) => right.at.localeCompare(left.at))
  }

  const accountSummary = (account: StateAccount): AccountSummary => {
    const entity = requireEntity(account.legalEntityId)
    const sessionCount = state.sessions.filter(
      (session) => session.accountId === account.id && new Date(session.absoluteExpiresAt) > clock(),
    ).length
    return {
      id: account.id,
      legalEntityId: account.legalEntityId,
      legalEntityName: entity.displayName,
      realm: account.realm,
      username: account.username,
      displayName: account.displayName,
      email: account.email,
      phone: account.phone,
      type: account.type,
      status: account.status,
      isPrimary: account.isPrimary,
      mfa: account.credentials.some((credential) => credential.type === 'totp' || credential.type === 'passkey'),
      sessionCount,
      createdAt: account.createdAt,
      lastLoginAt: account.lastLoginAt,
    }
  }

  const entitySummary = (entity: StateEntity): LegalEntitySummary => ({
    id: entity.id,
    displayName: entity.displayName,
    entityType: entity.entityType,
    country: entity.country,
    idDocType: entity.idDocType,
    verificationState: entity.verificationState,
    status: entity.status,
    verifiedAt: entity.verifiedAt,
    accountCount: state.accounts.filter((account) => account.legalEntityId === entity.id).length,
    createdAt: entity.createdAt,
  })

  return {
    mode: 'mock',

    async login(input: LoginInput): Promise<LoginResult> {
      await delay()
      const username = input.username.trim()
      const user = DEMO_USERS.find((candidate) => candidate.username === username && candidate.password === input.password)

      if (!user) {
        audit({
          actorType: 'account',
          actorId: `usr_unknown:${username}`,
          actorName: username || '未知用户',
          action: LOGIN_FAILED_ACTION,
          targetType: 'admin_console',
          targetId: 'console',
          targetLabel: '管理后台',
          result: 'failure',
          detail: '用户名或密码不正确，已记录一次失败尝试。',
        })
        await delay()
        throw new ApiError(401, '登录失败', '用户名或密码不正确。')
      }

      audit({
        actorType: 'admin',
        actorId: user.id,
        actorName: user.displayName,
        action: LOGIN_ACTION,
        targetType: 'admin_console',
        targetId: 'console',
        targetLabel: '管理后台',
        result: 'success',
        detail: '通过管理后台密码登录。',
      })

      const permissions = user.username === 'admin' ? ADMIN_PERMISSIONS : AUDITOR_PERMISSIONS
      return {
        token: `mock.${user.username}.${clock().getTime().toString(36)}`,
        user: {
          id: user.id,
          username: user.username,
          displayName: user.displayName,
          email: user.email,
          roles: user.roles,
          permissions: [...permissions],
        },
      }
    },

    async logout(): Promise<void> {
      await delay()
    },

    async getStats(): Promise<DashboardStats> {
      await delay()
      const now = clock()
      const today = localDateKey(now)
      const activeSessions = state.sessions.filter(
        (session) => new Date(session.absoluteExpiresAt) > now,
      ).length

      const trend: DashboardStats['loginTrend'] = []
      for (let offset = 6; offset >= 0; offset -= 1) {
        const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() - offset)
        const key = localDateKey(day)
        const events = state.auditEvents.filter((event) => localDateKey(event.at) === key)
        trend.push({
          date: key,
          success: events.filter((event) => event.action === LOGIN_ACTION).length,
          failed: events.filter((event) => event.action === LOGIN_FAILED_ACTION).length,
        })
      }

      return {
        totalAccounts: state.accounts.length,
        activeAccounts: state.accounts.filter((account) => account.status === 'active').length,
        lockedAccounts: state.accounts.filter((account) => account.status === 'locked').length,
        disabledAccounts: state.accounts.filter((account) => account.status === 'disabled').length,
        todayLogins: state.auditEvents.filter(
          (event) => event.action === LOGIN_ACTION && localDateKey(event.at) === today,
        ).length,
        todayFailedLogins: state.auditEvents.filter(
          (event) => event.action === LOGIN_FAILED_ACTION && localDateKey(event.at) === today,
        ).length,
        activeSessions,
        verifiedEntities: state.entities.filter((entity) => entity.verificationState === 'verified').length,
        pendingVerifications: state.entities.filter((entity) => entity.verificationState === 'pending').length,
        totalOrganizations: state.organizations.length,
        totalApplications: state.applications.length,
        loginTrend: trend,
      }
    },

    async listAccounts(query: AccountQuery): Promise<CursorPage<AccountSummary>> {
      await delay()
      const keyword = (query.q ?? '').trim().toLowerCase()
      const items = state.accounts
        .filter((account) => {
          if (query.status && account.status !== query.status) return false
          if (query.realm && account.realm !== query.realm) return false
          if (!keyword) return true
          const entity = requireEntity(account.legalEntityId)
          return [account.username, account.displayName, account.email, account.phone, entity.displayName]
            .some((value) => value.toLowerCase().includes(keyword))
        })
        .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
        .map(accountSummary)
      return paginate(items, query)
    },

    async getAccount(id: string): Promise<AccountDetail> {
      await delay()
      const account = requireAccount(id)
      const summary = accountSummary(account)
      return {
        ...summary,
        legalEntity: entitySummary(requireEntity(account.legalEntityId)),
        department: account.department,
        title: account.title,
        disabledReason: account.disabledReason,
        disabledAt: account.disabledAt,
        credentials: account.credentials.map(clone),
        customFields: { ...account.customFields },
      }
    },

    async setAccountStatus(id: string, status: AccountStatus, reason?: string): Promise<AccountSummary> {
      await delay()
      const account = requireAccount(id)
      if (account.status === status) {
        throw new ApiError(409, '状态未变化', `账户已经是 ${status} 状态。`)
      }
      if (status === 'disabled' && !(reason ?? '').trim()) {
        throw new ApiError(400, '缺少停用原因', '停用账户必须填写原因，用于审计追溯。')
      }

      account.status = status
      account.disabledReason = status === 'disabled' ? (reason ?? '').trim() : null
      account.disabledAt = status === 'disabled' ? clock().toISOString() : null

      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: status === 'disabled' ? 'account.disabled' : 'account.enabled',
        targetType: 'account',
        targetId: account.id,
        targetLabel: account.username,
        result: 'success',
        detail:
          status === 'disabled'
            ? `停用账户：${reason?.trim()}`
            : '重新启用账户，历史会话保持不变。',
      })

      return accountSummary(account)
    },

    async listAccountSessions(accountId: string): Promise<SessionInfo[]> {
      await delay()
      requireAccount(accountId)
      const now = clock()
      return state.sessions
        .filter((session) => session.accountId === accountId && new Date(session.absoluteExpiresAt) > now)
        .sort((left, right) => right.lastSeenAt.localeCompare(left.lastSeenAt))
        .map(clone)
    },

    async revokeSession(sessionId: string): Promise<void> {
      await delay()
      const index = state.sessions.findIndex((session) => session.id === sessionId)
      const session = state.sessions[index]
      if (!session) throw new ApiError(404, '会话不存在', `未找到会话 ${sessionId}。`)
      state.sessions.splice(index, 1)
      const account = state.accounts.find((item) => item.id === session.accountId)
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'session.revoked',
        targetType: 'session',
        targetId: session.id,
        targetLabel: account?.username ?? session.accountId,
        result: 'success',
        detail: `强制下线设备：${session.device}`,
      })
    },

    async listAccountAudit(accountId: string, query: CursorQuery): Promise<CursorPage<AuditEvent>> {
      await delay()
      requireAccount(accountId)
      const items = state.auditEvents
        .filter((event) => event.targetId === accountId || event.actorId === accountId)
        .sort((left, right) => right.at.localeCompare(left.at))
      return paginate(items, query)
    },

    async listLegalEntities(query: LegalEntityQuery): Promise<CursorPage<LegalEntitySummary>> {
      await delay()
      const keyword = (query.q ?? '').trim().toLowerCase()
      const items = state.entities
        .filter((entity) => {
          if (query.verificationState && entity.verificationState !== query.verificationState) return false
          if (!keyword) return true
          return (
            entity.displayName.toLowerCase().includes(keyword) ||
            entity.idDocDigest.toLowerCase().includes(keyword) ||
            (entity.providerRef ?? '').toLowerCase().includes(keyword)
          )
        })
        .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
        .map(entitySummary)
      return paginate(items, query)
    },

    async verifyLegalEntity(id: string, providerReference?: string): Promise<LegalEntityDetail> {
      const entity = requireEntity(id)
      entity.verificationState = 'verified'
      entity.verifiedAt = new Date().toISOString()
      entity.providerRef = providerReference ?? null
      return this.getLegalEntity(id)
    },

    async rejectLegalEntity(id: string, providerReference?: string): Promise<LegalEntityDetail> {
      const entity = requireEntity(id)
      entity.verificationState = 'rejected'
      entity.providerRef = providerReference ?? null
      return this.getLegalEntity(id)
    },

    async expireLegalEntity(id: string): Promise<LegalEntityDetail> {
      const entity = requireEntity(id)
      entity.verificationState = 'expired'
      return this.getLegalEntity(id)
    },

    async disableLegalEntity(id: string): Promise<{ id: string; status: 'active' | 'disabled'; disabledAccounts: number; revokedSessions: number }> {
      const entity = requireEntity(id)
      entity.status = 'disabled'
      return { id, status: 'disabled', disabledAccounts: 0, revokedSessions: 0 }
    },

    async enableLegalEntity(id: string): Promise<{ id: string; status: 'active' | 'disabled'; disabledAccounts: number; revokedSessions: number }> {
      const entity = requireEntity(id)
      entity.status = 'active'
      return { id, status: 'active', disabledAccounts: 0, revokedSessions: 0 }
    },

    async getLegalEntity(id: string): Promise<LegalEntityDetail> {
      await delay()
      const entity = requireEntity(id)
      return {
        ...entitySummary(entity),
        idDocType: entity.idDocType,
        idDocDigest: entity.idDocDigest,
        providerRef: entity.providerRef,
      }
    },

    async listLegalEntityAccounts(id: string, query: CursorQuery): Promise<CursorPage<AccountSummary>> {
      await delay()
      requireEntity(id)
      const items = state.accounts
        .filter((account) => account.legalEntityId === id)
        .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
        .map(accountSummary)
      return paginate(items, query)
    },

    async startPasskeyRegistration(accountId: string): Promise<PasskeyRegistration> {
      await delay()
      requireAccount(accountId)
      return {
        challengeId: 'mock-challenge-' + accountId,
        options: {
          challenge: 'bW9jay1jaGFsbGVuZ2U',
          rp: { id: 'nerves.local', name: 'Nerves' },
          user: { id: accountId, name: accountId, displayName: accountId },
          pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
        },
      }
    },

    async completePasskeyRegistration(
      accountId: string,
      _challengeId: string,
      _attestation: unknown,
      nickname?: string,
    ): Promise<PasskeyRegistrationResult> {
      await delay()
      const account = requireAccount(accountId)
      if (!account.credentials.some((credential) => credential.type === 'passkey')) {
        account.credentials.push({
          type: 'passkey',
          enrolledAt: new Date().toISOString(),
          lastUsedAt: null,
        })
      }
      void nickname
      return { active: true }
    },

    async removePasskeys(accountId: string): Promise<void> {
      await delay()
      const account = requireAccount(accountId)
      account.credentials = account.credentials.filter((credential) => credential.type !== 'passkey')
    },

    async getIntegrationStatus(): Promise<IntegrationStatus> {
      await delay()
      return {
        ldap: {
          enabled: false,
          host: '127.0.0.1',
          port: 389,
          useSsl: false,
          baseDn: 'dc=example,dc=com',
          userFilter: '(uid={0})',
          mailAttribute: 'mail',
          timeoutSeconds: 5,
          serviceAccount: false,
        },
        federation: [
          {
            id: 'upstream',
            displayName: 'Upstream OIDC',
            authority: 'https://idp.example.com',
            clientId: 'nerves-demo',
            redirectUri: 'https://id.example.com/api/v1/auth/federation/callback',
          },
        ],
      }
    },

    async updateLdapSettings(patch): Promise<IntegrationStatus['ldap']> {
      await delay()
      return { ...(await this.getIntegrationStatus()).ldap, ...patch }
    },

    async updatePdpSettings(patch): Promise<PdpStatus> {
      await delay()
      const current = await this.getPdpStatus()
      return { ...current, ...patch, enabled: patch.enabled ?? current.enabled }
    },

    async getPdpStatus(): Promise<PdpStatus> {
      await delay()
      return {
        enabled: false,
        url: null,
        timeoutSeconds: 3,
        failOpen: false,
        tokenConfigured: false,
      }
    },

    async listGroups(): Promise<Group[]> {
      await delay()
      return state.groups.map((group) => ({
        ...group,
        memberCount: state.groupMembers.filter((member) => member.groupId === group.id).length,
        members: state.groupMembers.filter((member) => member.groupId === group.id).map(clone),
      }))
    },

    async createGroup(input): Promise<Group> {
      await delay()
      if (state.groups.some((group) => group.code === input.code)) {
        throw new ApiError(409, '分组已存在', '分组代码重复。')
      }
      const group: Group = {
        id: "grp-" + String(state.groups.length + 1) + "-" + Date.now().toString(36),
        realm: input.realm,
        code: input.code,
        name: input.name,
        memberCount: 0,
      }
      state.groups.push(group)
      return clone(group)
    },

    async deleteGroup(id: string): Promise<void> {
      await delay()
      const index = state.groups.findIndex((group) => group.id === id)
      if (index < 0) throw new ApiError(404, '分组不存在', '该分组已被删除。')
      state.groups.splice(index, 1)
      state.groupMembers = state.groupMembers.filter((member) => member.groupId !== id)
    },

    async listGroupMembers(id: string): Promise<GroupMember[]> {
      await delay()
      return state.groupMembers
        .filter((member) => member.groupId === id)
        .map((member) => ({
          ...member,
          displayName: state.accounts.find((account) => account.id === member.accountId)?.displayName,
        }))
    },

    async addGroupMember(id: string, accountId: string): Promise<void> {
      await delay()
      requireAccount(accountId)
      if (!state.groupMembers.some((member) => member.groupId === id && member.accountId === accountId)) {
        state.groupMembers.push({ groupId: id, accountId, addedAt: new Date().toISOString() })
      }
    },

    async removeGroupMember(id: string, accountId: string): Promise<void> {
      await delay()
      state.groupMembers = state.groupMembers.filter(
        (member) => !(member.groupId === id && member.accountId === accountId),
      )
    },

    async listOrganizations(): Promise<Organization[]> {
      await delay()
      return state.organizations.map((organization) => ({
        ...organization,
        memberCount: state.members.filter((member) => member.organizationId === organization.id).length,
      }))
    },

    async listOrganizationMembers(organizationId: string): Promise<OrgMember[]> {
      await delay()
      requireOrganization(organizationId)
      return state.members
        .filter((member) => member.organizationId === organizationId)
        .sort((left, right) => left.joinedAt.localeCompare(right.joinedAt))
        .map(clone)
    },

    async addOrganizationMember(organizationId: string, accountId: string, role: OrgRole): Promise<OrgMember> {
      await delay()
      requireOrganization(organizationId)
      const account = requireAccount(accountId)
      if (state.members.some((member) => member.organizationId === organizationId && member.accountId === accountId)) {
        throw new ApiError(409, '成员已存在', `${account.username} 已经在该组织中。`)
      }

      const member: OrgMember = {
        id: `mem_${(state.members.length + 1).toString(36)}${Math.floor(rand() * 1_000_000).toString(36)}`,
        organizationId,
        accountId,
        accountName: account.displayName,
        username: account.username,
        orgRole: role,
        joinedAt: clock().toISOString(),
        lastActiveAt: account.lastLoginAt,
      }
      state.members.push(member)
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'organization.member.added',
        targetType: 'organization',
        targetId: organizationId,
        targetLabel: requireOrganization(organizationId).name,
        result: 'success',
        detail: `新增成员 ${account.username}，角色 ${role}。`,
      })
      return clone(member)
    },

    async updateOrganizationMember(organizationId: string, memberId: string, role: OrgRole): Promise<OrgMember> {
      await delay()
      requireOrganization(organizationId)
      const member = state.members.find(
        (item) => item.id === memberId && item.organizationId === organizationId,
      )
      if (!member) throw new ApiError(404, '成员不存在', `未找到成员 ${memberId}。`)
      member.orgRole = role
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'organization.member.updated',
        targetType: 'organization',
        targetId: organizationId,
        targetLabel: requireOrganization(organizationId).name,
        result: 'success',
        detail: `调整成员 ${member.username} 的角色为 ${role}。`,
      })
      return clone(member)
    },

    async removeOrganizationMember(organizationId: string, memberId: string): Promise<void> {
      await delay()
      requireOrganization(organizationId)
      const index = state.members.findIndex(
        (item) => item.id === memberId && item.organizationId === organizationId,
      )
      const member = state.members[index]
      if (!member) throw new ApiError(404, '成员不存在', `未找到成员 ${memberId}。`)
      state.members.splice(index, 1)
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'organization.member.removed',
        targetType: 'organization',
        targetId: organizationId,
        targetLabel: requireOrganization(organizationId).name,
        result: 'success',
        detail: `移除成员 ${member.username}。`,
      })
    },

    async listRoles(): Promise<Role[]> {
      await delay()
      return state.roles.map(clone)
    },

    async listPermissions(): Promise<Permission[]> {
      await delay()
      return PERMISSIONS.map(clone)
    },

    async updateRole(
      id: string,
      input: { name: string; description: string; permissions: string[] },
    ): Promise<Role> {
      await delay()
      const role = requireRole(id)
      const name = input.name.trim()
      if (name.length < 2) throw new ApiError(400, '参数校验失败', '角色名称至少 2 个字符。', undefined, {
        name: ['至少 2 个字符。'],
      })
      const unknown = input.permissions.find((code) => !ALL_PERMISSION_CODES.includes(code))
      if (unknown) throw new ApiError(400, '未知权限', `权限码 ${unknown} 不在目录中。`)
      if (input.permissions.length === 0) throw new ApiError(400, '参数校验失败', '角色至少需要一个权限。', undefined, {
        permissions: ['至少选择一个权限。'],
      })

      role.name = name
      role.description = input.description.trim()
      role.permissions = [...new Set(input.permissions)].sort()
      role.updatedAt = clock().toISOString()
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'role.updated',
        targetType: 'role',
        targetId: role.id,
        targetLabel: role.code,
        result: 'success',
        detail: `更新角色，授权 ${role.permissions.length} 项权限。`,
      })
      return clone(role)
    },

    async listApplications(): Promise<Application[]> {
      await delay()
      return state.applications.map(clone)
    },

    async createApplication(input: ApplicationInput): Promise<Application> {
      await delay()
      const name = input.name.trim()
      const clientId = input.clientId.trim()
      const redirectUris = input.redirectUris.map((uri) => uri.trim()).filter(Boolean)
      const scopes = [...new Set(input.scopes)]

      if (name.length < 2) throw new ApiError(400, '参数校验失败', '应用名称至少 2 个字符。', undefined, {
        name: ['至少 2 个字符。'],
      })
      if (!/^[a-z][a-z0-9-]{2,63}$/.test(clientId)) {
        throw new ApiError(400, '参数校验失败', 'client_id 需为 3-64 位小写字母、数字或连字符，并以字母开头。', undefined, {
          clientId: ['格式不合法。'],
        })
      }
      if (state.applications.some((application) => application.clientId === clientId)) {
        throw new ApiError(409, 'client_id 已存在', `应用 ${clientId} 已被占用。`)
      }
      if (redirectUris.length === 0) {
        throw new ApiError(400, '参数校验失败', '至少需要一个回调地址。', undefined, {
          redirectUris: ['至少填写一个回调地址。'],
        })
      }
      for (const uri of redirectUris) {
        if (!isAcceptableRedirect(uri)) {
          throw new ApiError(400, '回调地址不合法', `${uri} 必须是 https 绝对地址（本机开发允许 http://localhost）。`, undefined, {
            redirectUris: [`${uri} 不合法。`],
          })
        }
      }
      if (!scopes.includes('openid')) {
        throw new ApiError(400, '参数校验失败', 'OIDC 应用必须至少申请 openid。', undefined, {
          scopes: ['缺少 openid。'],
        })
      }

      const application: Application = {
        id: `app_${clientId.replaceAll('-', '_')}`,
        name,
        description: input.description.trim(),
        clientId,
        logoUrl: input.logoUrl?.trim() ? input.logoUrl.trim() : null,
        redirectUris,
        scopes,
        status: 'active',
        createdAt: clock().toISOString(),
        lastRotatedAt: clock().toISOString(),
      }
      state.applications.push(application)
      audit({
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'application.created',
        targetType: 'application',
        targetId: application.id,
        targetLabel: clientId,
        result: 'success',
        detail: `创建应用，回调地址 ${redirectUris.length} 个，scope ${scopes.join(' ')}。`,
      })
      return clone(application)
    },

    async listAuditEvents(query: AuditQuery): Promise<CursorPage<AuditEvent>> {
      await delay()
      const keyword = (query.actor ?? '').trim().toLowerCase()
      const action = (query.action ?? '').trim().toLowerCase()
      const items = state.auditEvents
        .filter((event) => {
          const key = localDateKey(event.at)
          if (query.from && key < query.from) return false
          if (query.to && key > query.to) return false
          if (query.result && event.result !== query.result) return false
          if (keyword && !event.actorName.toLowerCase().includes(keyword) && !event.actorId.toLowerCase().includes(keyword)) {
            return false
          }
          if (action && !event.action.toLowerCase().includes(action) && !event.detail.toLowerCase().includes(action)) {
            return false
          }
          return true
        })
        .sort((left, right) => right.at.localeCompare(left.at))
      return paginate(items, query)
    },

    async listNotifications(query: NotificationQuery): Promise<CursorPage<NotificationRecord>> {
      await delay()
      const recipient = (query.to ?? '').trim().toLowerCase()
      const items = state.notifications
        .filter((notification) => {
          if (query.status && notification.status !== query.status) return false
          if (recipient && !notification.to.toLowerCase().includes(recipient)) return false
          return true
        })
        .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
        .map(toNotificationRecord)
      return paginate(items, query)
    },

    async getNotification(id: string): Promise<NotificationRecord> {
      await delay()
      const notification = state.notifications.find((item) => item.id === id)
      if (!notification) throw new ApiError(404, '通知不存在', `未找到通知 ${id}。`)
      return toNotificationRecord(notification)
    },

    async sendNotificationEmail(input: SendNotificationEmailInput): Promise<NotificationRecord> {
      await delay()
      const to = input.to.trim().toLowerCase()
      if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(to)) {
        throw new ApiError(400, '参数校验失败', 'recipient address is not a valid email address')
      }
      const template = NOTIFICATION_TEMPLATES.find((item) => item.code === input.template)
      if (!template) {
        throw new ApiError(400, '参数校验失败', `unknown template ${input.template}`)
      }
      const missing = missingTemplateValues(input.template, input.values)
      if (missing.length > 0) {
        throw new ApiError(400, '参数校验失败', `missing template values: ${missing.join(', ')}`)
      }
      const now = clock()
      const values = normalizeTemplateValues(input.values)
      const notification: StateNotification = {
        id: `ntf_${randomBase32(rand, 22)}`,
        to,
        template: input.template,
        subject: renderMockSubject(input.template, values),
        status: 'sent',
        attempts: 0,
        error: null,
        createdAt: now.toISOString(),
        sentAt: now.toISOString(),
        nextAttemptAt: now.toISOString(),
      }
      state.notifications.push(notification)
      return toNotificationRecord(notification)
    },
  }
}

interface StateAccount {
  id: string
  legalEntityId: string
  realm: string
  username: string
  displayName: string
  email: string
  phone: string
  type: AccountType
  status: AccountStatus
  isPrimary: boolean
  createdAt: string
  lastLoginAt: string | null
  department: string | null
  title: string | null
  disabledReason: string | null
  disabledAt: string | null
  credentials: CredentialStatus[]
  customFields: Record<string, string>
}

interface StateEntity {
  id: string
  displayName: string
  entityType: LegalEntityType
  country: string
  idDocType: IdDocumentType
  status: 'active' | 'disabled' 
  idDocDigest: string
  verificationState: VerificationState
  verifiedAt: string | null
  providerRef: string | null
  createdAt: string
}

interface MockState {
  entities: StateEntity[]
  accounts: StateAccount[]
  sessions: SessionInfo[]
  auditEvents: AuditEvent[]
  organizations: Array<Omit<Organization, 'memberCount'>>
  groups: Group[]
  groupMembers: Array<{ groupId: string; accountId: string; addedAt: string }>
  members: OrgMember[]
  roles: Role[]
  applications: Application[]
  notifications: StateNotification[]
}

interface AuditInput {
  at: Date
  actorType: ActorType
  actorId: string
  actorName: string
  action: string
  targetType: string
  targetId: string
  targetLabel: string
  result: AuditResult
  detail: string
}

/** Every field but the timestamp is required at the call site. */
type NewAuditEvent = Omit<AuditInput, 'at'> & { at?: Date }

function makeAuditEvent(input: AuditInput, rand: () => number): AuditEvent {
  return {
    id: `evt_${randomBase32(rand, 22)}`,
    at: input.at.toISOString(),
    realm: 'cn',
    actorType: input.actorType,
    actorId: input.actorId,
    actorName: input.actorName,
    action: input.action,
    targetType: input.targetType,
    targetId: input.targetId,
    targetLabel: input.targetLabel,
    result: input.result,
    ipDigest: `sha256:${randomHex(rand, 16)}`,
    traceId: randomHex(rand, 16),
    detail: input.detail,
  }
}

function seedState(context: { rand: () => number; now: Date }): MockState {
  const { rand } = context
  const now = context.now

  const entities: StateEntity[] = ENTITY_SEEDS.map((seedEntity, index) => {
    const createdAt = daysBefore(now, 30 + index * 41 + Math.floor(rand() * 12))
    const verifiedAt =
      seedEntity.verificationState === 'pending' || seedEntity.verificationState === 'rejected'
        ? null
        : daysBefore(now, 20 + index * 7)
    return {
      id: `le_${randomBase32(rand, 24)}`,
      displayName: seedEntity.displayName,
      entityType: seedEntity.entityType,
      country: seedEntity.country,
      idDocType: seedEntity.idDocType,
      idDocDigest: `hmac-sha256:${randomHex(rand, 32)}`,
      status: 'active',
      verificationState: seedEntity.verificationState,
      verifiedAt: verifiedAt?.toISOString() ?? null,
      providerRef: seedEntity.providerRef,
      createdAt: createdAt.toISOString(),
    }
  })

  const accounts: StateAccount[] = []
  for (let index = 0; index < 48; index += 1) {
    const entity = entities[index % entities.length]
    if (!entity) continue
    const person = PEOPLE[index % PEOPLE.length]
    if (!person) continue

    const isService = index % 7 === 5
    const isManaged = !isService && index % 11 === 3
    const service = SERVICE_NAMES[index % SERVICE_NAMES.length]
    if (!service) continue

    const username = isService ? service[1] : isManaged ? `${person[1]}.ops` : person[1]
    const displayName = isService ? service[0] : isManaged ? `${person[0]}（代管）` : person[0]
    const type: AccountType = isService ? 'service' : isManaged ? 'managed' : 'personal'
    const status: AccountStatus =
      index % 12 === 5 || index === 41 ? 'disabled' : index % 11 === 7 || index === 23 ? 'locked' : 'active'
    const createdAt = daysBefore(now, 3 + ((index * 37) % 560))
    const lastLoginDays = status === 'active' ? 1 + (index % 19) : status === 'locked' ? 34 + (index % 20) : null

    const credentials: CredentialStatus[] = [
      {
        type: 'password',
        enrolledAt: createdAt.toISOString(),
        lastUsedAt: lastLoginDays === null ? null : daysBefore(now, lastLoginDays).toISOString(),
      },
    ]
    const hasTotp = type === 'service' || index % 5 !== 0
    if (hasTotp) {
      credentials.push({
        type: 'totp',
        enrolledAt: daysBefore(createdAt, -2).toISOString(),
        lastUsedAt: lastLoginDays === null ? null : daysBefore(now, lastLoginDays).toISOString(),
      })
      credentials.push({
        type: 'recovery-code',
        enrolledAt: daysBefore(createdAt, -2).toISOString(),
        lastUsedAt: null,
      })
    }
    if (index % 9 === 4) {
      credentials.push({
        type: 'passkey',
        enrolledAt: daysBefore(now, 30 + index).toISOString(),
        lastUsedAt: lastLoginDays === null ? null : daysBefore(now, lastLoginDays).toISOString(),
      })
    }

    accounts.push({
      id: `acc_${randomBase32(rand, 24)}`,
      legalEntityId: entity.id,
      realm: index % 9 === 0 ? 'partner' : 'cn',
      username,
      displayName,
      email: `${username.replaceAll('.', '-')}@nerves.example`,
      phone: `13${index % 10}****${String(1000 + ((index * 137) % 9000))}`,
      type,
      status,
      isPrimary: index % 6 === 0,
      createdAt: createdAt.toISOString(),
      lastLoginAt: lastLoginDays === null ? null : daysBefore(now, lastLoginDays).toISOString(),
      department: type === 'service' ? null : DEPARTMENTS[index % DEPARTMENTS.length] ?? null,
      title: type === 'service' ? null : TITLES[index % TITLES.length] ?? null,
      disabledReason: status === 'disabled' ? DISABLED_REASONS[index % DISABLED_REASONS.length] ?? null : null,
      disabledAt: status === 'disabled' ? daysBefore(now, 2 + (index % 40)).toISOString() : null,
      credentials,
      customFields: type === 'service' ? { owner_team: '平台研发部' } : { employee_no: `E${String(10000 + index)}` },
    })
  }

  const sessions: SessionInfo[] = []
  for (const [index, account] of accounts.entries()) {
    if (account.status === 'disabled') continue
    const count = account.status === 'locked' ? 1 : index % 3
    for (let slot = 0; slot < count; slot += 1) {
      const device = DEVICES[(index + slot) % DEVICES.length] ?? DEVICES[0] ?? 'Chrome'
      const createdAt = daysBefore(now, (index + slot) % 12)
      const amr = account.credentials.some((credential) => credential.type === 'passkey')
        ? ['passkey']
        : account.credentials.some((credential) => credential.type === 'totp')
          ? ['pwd', 'otp']
          : ['pwd']
      sessions.push({
        id: `ses_${randomBase32(rand, 24)}`,
        accountId: account.id,
        device,
        amr,
        ipDigest: `sha256:${randomHex(rand, 12)}`,
        createdAt: createdAt.toISOString(),
        lastSeenAt: minutesBefore(now, 3 + Math.floor(rand() * 600)).toISOString(),
        absoluteExpiresAt: daysBefore(createdAt, -30).toISOString(),
      })
    }
  }

  const auditEvents: AuditEvent[] = []
  const push = (event: NewAuditEvent) => {
    auditEvents.push(makeAuditEvent({ ...event, at: event.at ?? now }, rand))
  }

  for (const account of accounts) {
    push({
      at: new Date(account.createdAt),
      actorType: 'admin',
      actorId: 'usr_platform_admin',
      actorName: '平台管理员',
      action: 'account.created',
      targetType: 'account',
      targetId: account.id,
      targetLabel: account.username,
      result: 'success',
      detail: `创建账户类型 ${account.type}，归属实体 ${account.legalEntityId}。`,
    })

    if (account.status === 'disabled' && account.disabledAt) {
      push({
        at: new Date(account.disabledAt),
        actorType: 'admin',
        actorId: 'usr_platform_admin',
        actorName: '平台管理员',
        action: 'account.disabled',
        targetType: 'account',
        targetId: account.id,
        targetLabel: account.username,
        result: 'success',
        detail: `停用账户：${account.disabledReason ?? '未填写原因'}`,
      })
    }
    if (account.status === 'locked') {
      push({
        at: minutesBefore(now, 120 + Math.floor(rand() * 2000)),
        actorType: 'system',
        actorId: 'system:risk-engine',
        actorName: '风控引擎',
        action: 'account.locked',
        targetType: 'account',
        targetId: account.id,
        targetLabel: account.username,
        result: 'success',
        detail: '连续失败登录触发锁定，等待管理员复核。',
      })
    }
    for (const credential of account.credentials) {
      if (credential.type === 'totp' || credential.type === 'passkey') {
        push({
          at: new Date(credential.enrolledAt),
          actorType: 'account',
          actorId: account.id,
          actorName: account.displayName,
          action: `credential.${credential.type}.enrolled`,
          targetType: 'credential',
          targetId: account.id,
          targetLabel: account.username,
          result: 'success',
          detail: `绑定 ${credential.type} 凭据。`,
        })
      }
    }
  }

  for (const entity of entities) {
    if (entity.verificationState === 'verified' && entity.verifiedAt) {
      push({
        at: new Date(entity.verifiedAt),
        actorType: 'admin',
        actorId: 'usr_compliance_auditor',
        actorName: '合规审计员',
        action: 'legal_entity.verified',
        targetType: 'legal_entity',
        targetId: entity.id,
        targetLabel: entity.displayName,
        result: 'success',
        detail: `实名核验通过，来源 ${entity.providerRef ?? '人工复核'}。`,
      })
    }
    if (entity.verificationState === 'rejected') {
      push({
        at: daysBefore(now, 6),
        actorType: 'admin',
        actorId: 'usr_compliance_auditor',
        actorName: '合规审计员',
        action: 'legal_entity.rejected',
        targetType: 'legal_entity',
        targetId: entity.id,
        targetLabel: entity.displayName,
        result: 'denied',
        detail: '统一社会信用代码与营业执照影像不一致，已驳回。',
      })
    }
    if (entity.verificationState === 'pending') {
      push({
        at: daysBefore(now, 2),
        actorType: 'account',
        actorId: 'usr_self_service',
        actorName: '自助注册',
        action: 'legal_entity.pending',
        targetType: 'legal_entity',
        targetId: entity.id,
        targetLabel: entity.displayName,
        result: 'success',
        detail: '提交实名材料，等待 Compliance.CN 核验。',
      })
    }
  }

  const activeAccounts = accounts.filter((account) => account.status === 'active')
  for (let dayOffset = 13; dayOffset >= 0; dayOffset -= 1) {
    const dayEnd = new Date(now.getFullYear(), now.getMonth(), now.getDate() - dayOffset + 1)
    const dayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate() - dayOffset)
    const elapsed =
      dayOffset === 0
        ? Math.max(0.06, (now.getTime() - dayStart.getTime()) / (24 * 60 * 60 * 1000))
        : 1
    const successCount = Math.max(2, Math.round((6 + Math.floor(rand() * 14)) * elapsed))
    const failureCount = Math.max(1, Math.round((1 + Math.floor(rand() * 4)) * elapsed))

    for (let index = 0; index < successCount; index += 1) {
      const account = activeAccounts[Math.floor(rand() * activeAccounts.length)]
      if (!account) continue
      const at = randomTimeBetween(dayStart, dayOffset === 0 ? now : dayEnd, rand)
      push({
        at,
        actorType: 'account',
        actorId: account.id,
        actorName: account.displayName,
        action: LOGIN_ACTION,
        targetType: 'account',
        targetId: account.id,
        targetLabel: account.username,
        result: 'success',
        detail: `来自 ${DEVICES[Math.floor(rand() * DEVICES.length)] ?? 'Chrome'} 的登录。`,
      })
    }
    for (let index = 0; index < failureCount; index += 1) {
      const account = activeAccounts[Math.floor(rand() * activeAccounts.length)]
      if (!account) continue
      push({
        at: randomTimeBetween(dayStart, dayOffset === 0 ? now : dayEnd, rand),
        actorType: 'account',
        actorId: account.id,
        actorName: account.displayName,
        action: LOGIN_FAILED_ACTION,
        targetType: 'account',
        targetId: account.id,
        targetLabel: account.username,
        result: 'failure',
        detail: '密码校验失败，已累计失败次数。',
      })
    }
  }

  for (const [index, role] of ROLE_SEEDS.entries()) {
    push({
      at: daysBefore(now, index * 9 + 1),
      actorType: 'admin',
      actorId: 'usr_platform_admin',
      actorName: '平台管理员',
      action: 'role.updated',
      targetType: 'role',
      targetId: role.id,
      targetLabel: role.code,
      result: 'success',
      detail: `角色授权调整为 ${role.permissionCodes.length} 项权限。`,
    })
  }

  const members: OrgMember[] = []
  let memberIndex = 0
  const roles: OrgMember['orgRole'][] = ['owner', 'admin', 'member', 'member', 'viewer']
  for (const organization of ORG_SEEDS) {
    const size = 4 + (memberIndex % 5)
    for (let slot = 0; slot < size; slot += 1) {
      const account = accounts[(memberIndex * 7 + slot * 3) % accounts.length]
      if (!account) continue
      members.push({
        id: `mem_${organization.code.toLowerCase()}_${slot}`,
        organizationId: organization.id,
        accountId: account.id,
        accountName: account.displayName,
        username: account.username,
        orgRole: roles[slot % roles.length] ?? 'member',
        joinedAt: daysBefore(now, 20 + slot * 17 + memberIndex * 3).toISOString(),
        lastActiveAt: account.lastLoginAt,
      })
    }
    memberIndex += 1
  }

  const applications: Application[] = APP_SEEDS.map((application) => ({
    id: application.id,
    name: application.name,
    description: application.description,
    clientId: application.clientId,
    logoUrl: application.logoUrl,
    redirectUris: [...application.redirectUris],
    scopes: [...application.scopes],
    status: application.status,
    createdAt: daysBefore(now, application.createdAtDaysAgo).toISOString(),
    lastRotatedAt:
      application.lastRotatedDaysAgo === null
        ? null
        : daysBefore(now, application.lastRotatedDaysAgo).toISOString(),
  }))

  for (const application of applications) {
    push({
      at: new Date(application.createdAt),
      actorType: 'admin',
      actorId: 'usr_platform_admin',
      actorName: '平台管理员',
      action: 'application.created',
      targetType: 'application',
      targetId: application.id,
      targetLabel: application.clientId,
      result: 'success',
      detail: `创建应用 ${application.name}。`,
    })
  }

  auditEvents.sort((left, right) => right.at.localeCompare(left.at))

  return {
    entities,
    accounts,
    sessions,
    auditEvents,
    groups: [
      { id: 'grp-1', realm: 'nerves', code: 'engineering', name: 'Engineering' },
      { id: 'grp-2', realm: 'nerves', code: 'finance', name: 'Finance' },
    ],
    groupMembers: [],
    organizations: ORG_SEEDS.map((organization, index) => ({
      ...organization,
      createdAt: daysBefore(now, 120 + index * 30).toISOString(),
    })),
    members,
    roles: ROLE_SEEDS.map((role, index) => ({
      id: role.id,
      code: role.code,
      name: role.name,
      description: role.description,
      scope: role.scope,
      appName: role.appName,
      builtIn: role.builtIn,
      permissions: [...role.permissionCodes].sort(),
      memberCount: role.memberCount,
      updatedAt: daysBefore(now, index * 9 + 1).toISOString(),
    })),
    applications,
    notifications: seedNotifications(rand, now),
  }
}

interface StateNotification {
  id: string
  to: string
  template: string
  subject: string
  status: NotificationStatus
  attempts: number
  error: string | null
  createdAt: string
  sentAt: string | null
  nextAttemptAt: string
}

function toNotificationRecord(notification: StateNotification): NotificationRecord {
  return { ...notification }
}

function renderMockSubject(template: string, values: Record<string, string>): string {
  const subjects: Record<string, string> = {
    'account.welcome': `欢迎加入 ${values.displayName ?? '用户'}`,
    'account.password-reset': '重置您的 Nerves 账户密码',
    'security.sign-in': `新的登录：${values.device ?? '未知设备'}`,
  }
  return subjects[template] ?? template
}

function seedNotifications(rand: () => number, now: Date): StateNotification[] {
  const seeds: Array<{
    to: string
    template: string
    subject: string
    status: NotificationStatus
    attempts: number
    error: string | null
    minutesAgo: number
  }> = [
    { to: 'admin@nerves.example', template: 'account.welcome', subject: '欢迎加入 平台管理员', status: 'sent', attempts: 0, error: null, minutesAgo: 12 },
    { to: 'zhang.wei@nerves.example', template: 'account.password-reset', subject: '重置您的 Nerves 账户密码', status: 'sent', attempts: 1, error: null, minutesAgo: 48 },
    { to: 'liu.yang@nerves.example', template: 'security.sign-in', subject: '新的登录：Chrome 141 · macOS', status: 'pending', attempts: 0, error: null, minutesAgo: 3 },
    { to: 'partner@example.com', template: 'security.sign-in', subject: '新的登录：Edge 141 · Windows 11', status: 'failed', attempts: 5, error: 'Failure sending mail.: Connection refused', minutesAgo: 180 },
    { to: 'ops@nerves.example', template: 'account.welcome', subject: '欢迎加入 运维团队', status: 'sent', attempts: 0, error: null, minutesAgo: 600 },
    { to: 'contractor@example.com', template: 'account.password-reset', subject: '重置您的 Nerves 账户密码', status: 'failed', attempts: 2, error: 'mailbox unavailable', minutesAgo: 1440 },
    { to: 'chen.jing@nerves.example', template: 'account.welcome', subject: '欢迎加入 陈静', status: 'sent', attempts: 0, error: null, minutesAgo: 2880 },
  ]

  return seeds.map((seed) => {
    const createdAt = new Date(now.getTime() - seed.minutesAgo * 60_000)
    const sentAt = seed.status === 'sent' ? new Date(createdAt.getTime() + 1_000) : null
    return {
      id: `ntf_${randomBase32(rand, 22)}`,
      to: seed.to,
      template: seed.template,
      subject: seed.subject,
      status: seed.status,
      attempts: seed.attempts,
      error: seed.error,
      createdAt: createdAt.toISOString(),
      sentAt: sentAt?.toISOString() ?? null,
      nextAttemptAt: createdAt.toISOString(),
    }
  })
}

function paginate<T>(items: readonly T[], query: CursorQuery): CursorPage<T> {
  const limit = Math.min(Math.max(query.limit ?? 20, 1), 100)
  const offset = query.cursor ? decodeCursor(query.cursor) : 0
  if (offset < 0 || offset > items.length) {
    throw new ApiError(400, '游标已失效', '请从第一页重新开始查询。')
  }
  const page = items.slice(offset, offset + limit)
  const nextOffset = offset + limit
  return {
    items: page.map(clone),
    nextCursor: nextOffset < items.length ? encodeCursor(nextOffset) : null,
  }
}

export function encodeCursor(offset: number): string {
  return btoa(`o:${offset}`).replaceAll('=', '')
}

export function decodeCursor(cursor: string): number {
  try {
    const decoded = atob(cursor)
    const match = /^o:(\d+)$/.exec(decoded)
    if (!match?.[1]) throw new Error('malformed cursor')
    return Number(match[1])
  } catch {
    throw new ApiError(400, '游标已失效', '请从第一页重新开始查询。')
  }
}

function isAcceptableRedirect(uri: string): boolean {
  try {
    const url = new URL(uri)
    if (url.protocol === 'https:') return true
    const host = url.hostname
    return url.protocol === 'http:' && (host === 'localhost' || host === '127.0.0.1' || host === '[::1]')
  } catch {
    return false
  }
}

function mulberry32(seed: number): () => number {
  let state = seed >>> 0
  return () => {
    state = (state + 0x6d2b79f5) >>> 0
    let value = state
    value = Math.imul(value ^ (value >>> 15), value | 1)
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
    return ((value ^ (value >>> 14)) >>> 0) / 4_294_967_296
  }
}

function randomBase32(rand: () => number, length: number): string {
  let result = ''
  for (let index = 0; index < length; index += 1) {
    result += BASE32[Math.floor(rand() * BASE32.length)] ?? '0'
  }
  return result
}

function randomHex(rand: () => number, length: number): string {
  let result = ''
  for (let index = 0; index < length; index += 1) {
    result += Math.floor(rand() * 16).toString(16)
  }
  return result
}

function daysBefore(from: Date, days: number): Date {
  const date = new Date(from)
  date.setDate(date.getDate() - days)
  return date
}

function minutesBefore(from: Date, minutes: number): Date {
  return new Date(from.getTime() - minutes * 60_000)
}

function randomTimeBetween(start: Date, end: Date, rand: () => number): Date {
  const span = Math.max(end.getTime() - start.getTime(), 1)
  return new Date(start.getTime() + Math.floor(rand() * span))
}

function clone<T>(value: T): T {
  return structuredClone(value)
}
