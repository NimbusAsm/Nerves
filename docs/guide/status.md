# 实现状态

> 本页是文档中“是否已实现”的唯一事实来源。核对时间：2026-11-08，仓库 main。
> 精确到端点与文件的状态请以 src/、proto/ 与 git 历史为准。

## 标注约定

| 标记 | 含义 |
| --- | --- |
| ✅ 已实现 | 有可运行代码，且本次交付已实测 |
| 🚧 部分实现 | 有代码和测试，但未达到端到端可用 |
| 🧭 v1.0 计划 | 方案承诺 v1.0 交付，当前未实现 |
| 🔭 v1.1 计划 | 明确排到 v1.1，当前不可用 |

## 平台与部署

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 网关（gateway） | ✅ 已实现 | YARP 反向代理 /api/v1 到 identity/session/authorization/audit，并提供 /api/v1/auth/login 与受限 CORS |
| 服务骨架与健康端点 | ✅ 已实现 | 七个服务均可启动，提供 /healthz、/readyz、/api/v1/meta、/openapi/v1.json；directory 仍是骨架 |
| 共享服务默认值 | ✅ 已实现 | ProblemDetails、健康检查、OpenTelemetry、管理 API 令牌过滤 |
| 领域基元 | ✅ 已实现 | Result / Reason / UUIDv7 / 游标分页 / 领域事件 |
| protobuf 契约与契约门禁 | ✅ 已实现 | identity / session / authorization / audit 四个 proto；buf lint + breaking 进 CI |
| CI（三作业） | ✅ 已实现 | dotnet（build/test/format/pack/buf）+ frontend（admin 与 TS SDK）+ python（pytest），均绿 |
| 开发 compose | ✅ 已实现 | PG 18.6 + NATS 2.15.2 + 六个服务 |
| Helm Chart | ✅ 已实现 | helm lint --strict 与 kubeconform 通过；未在真实集群安装验证 |
| Helm chart | ✅ 已实现（生产可用） | 8 个 Deployment/Service、5 个 gRPC 端口、7 个迁移 Job（MigrateOnly）、2 个 DataProtection PVC；网关与 IdP 互通地址由 values 注入 |
| 集成状态页 | ✅ 已实现 | 管理台新增「集成」页：LDAP（启用/主机/BaseDN/过滤器）、OIDC 联邦提供方、PDP（启用/端点/超时/fail-open）；数据来自 IdP /api/v1/admin/integrations 与 Authorization /api/v1/admin/pdp，只读 |
| 管理后台（Vue 3） | ✅ 已实现 | 11 个页面（含用户组、集成）、mock/live 切换、中英 i18n、35 个测试 |
| 管理后台 live 模式 | ✅ 已实现 | VITE_USE_MOCK=false 走 OIDC 授权码 + PKCE（登录 API 取票 → 跳 IdP → 回调换令牌），令牌仅存内存 |
| 管理聚合层（console-api） | 🧭 v1.0 计划 | 目前由网关按路径转发，未做响应聚合 |
| 管理台通知中心 | ✅ 已实现 | 投递日志（游标分页、状态/收件人过滤、详情）、按模板发送（占位符动态表单）；API 走网关 `/api/v1/notifications`；credentials=omit（Bearer 内存令牌，无 Cookie） |
| 通知服务（notification） | ✅ 已实现 | Email 投递与投递日志（游标分页、状态/收件人过滤）；模板渲染与占位符校验；失败指数退避重试；SMTP 可关闭（no-op）；经网关 `/api/v1/notifications` 就绪 |
| CLI（迁移 / 初始化 / 诊断） | 🧭 v1.0 计划 | 当前迁移使用 dotnet ef |

## 身份、认证与会话

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 法人实体（LegalEntity） | ✅ 已实现 | 实名摘要 HMAC 唯一索引、状态机、xmin 并发 |
| 网络账户（NetworkAccount） | ✅ 已实现 | realm 内用户名唯一、状态机、服务账户必须无实体 |
| 凭据与口令校验 | ✅ 已实现 | PBKDF2-HMAC-SHA512 / 100k，自描述 PHC 串，常量时间比较 |
| 登录标识路由索引 | ✅ 已实现 | HMAC-SHA256（type|realm|value），注册与登录共用 |
| 管理 API | ✅ 已实现 | /api/v1/legal-entities、/accounts、/stats：游标分页、过滤、停用/锁定、重名 409 |
| 登录（纯 API） | ✅ 已实现 | POST /api/v1/auth/login → 一次性 login_ticket（90 秒、单次使用）→ /connect/authorize；服务端不渲染任何页面 |
| 登录 UI | 由调用方实现 | 管理后台与第三方产品各自实现登录页，通过 API + OIDC 接入 |
| 会话服务 | ✅ 已实现 | session schema、登录建会话、登出撤销、管理端列出/撤销 |
| MFA：TOTP | ✅ 已实现 | 注册返回 otpauth URI（密钥一次可见）、激活需验证码、密钥用 Data Protection 加密存储；登录先返回 mfaRequired + 一次性 mfaTicket，再凭 /api/v1/auth/mfa 完成二次校验 |
| MFA：恢复码 | ✅ 已实现 | 生成 10 个一次性恢复码（明文只返回一次、库中仅存 SHA-256），可查询剩余数量与重置；登录时与 TOTP 共用 /api/v1/auth/mfa，使用后即作废 |
| MFA：Passkey | ✅ 已实现 | WebAuthn 注册/认证（Fido2NetLib）：challenge 单次使用并持久化，凭据 Data Protection 加密、签名计数递增校验；管理台账户详情可登记/移除通行密钥 |
| 账户切换 | ✅ 已实现 | 同一法人名下账户可凭当前 Bearer 令牌调用 /api/v1/auth/switch-account 换取新登录票据（不重输口令）；跨法人 403。实测新令牌 sub 为目标账户、leid 不变 |
| LDAP 目录同步 | ✅ 已实现 | Ldap:Sync 配置（Enabled/IntervalMinutes/BaseDn/Filter/AttributeMap）；后台按间隔同步用户到 identity（新 gRPC UpsertDirectoryAccount，realm+username 优先、email 次之，幂等且不改口令/不删除）；GET /api/v1/admin/directory-sync/status 返回最近一次统计；审计 directory.sync.completed |
| 会话生命周期 | ✅ 已实现 | `Session:IdleMinutes / AbsoluteHours / ExpirySweepSeconds` 可配；IdP Cookie 不滑动且带 Max-Age；session 服务定期把过期会话标记为 expired |
| 分片重算工具 | ✅ 已实现 | Sharding:Recompute（可 DryRun）重算 routing_index.Shard 并输出分布；幂等，供扩容前校验 |
| 领域事件 outbox | ✅ 已实现 | 实名状态变更在同一事务写入 outbox，每个账户一条 realname.changed；后台每 5s 投递审计并标记 dispatched，失败重试；GET /api/v1/events 增量拉取 |
| 口令轮换 | ✅ 已实现 | POST /api/v1/accounts/{id}/password：强度≥12 位、禁止与旧口令相同；替换凭据并撤销全部会话，写审计 account.password-changed |
| 法人实体停用 | ✅ 已实现 | POST /api/v1/legal-entities/{id}/disable 停用实体并连带停用名下全部账户、撤销各自会话（I6）；/enable 恢复实体状态 |
| 停用/锁定即撤销会话 | ✅ 已实现 | Identity 在 disable/lock 后经 session gRPC 撤销该账户全部会话（理由记入 RevokedReason） |
| 刷新令牌轮换 | ✅ 已实现 | 开启 offline_access 后发刷新令牌，每次换发都轮换；`SetRefreshTokenReuseLeeway(0)` 使复用旧令牌返回 invalid_grant 并撤销令牌家族 |
| 失败锁定 | ✅ 已实现 | 15 分钟内 5 次失败锁定 15 分钟；计数与锁定落在 idp.login_attempt，多副本与重启后仍生效 |

## 授权、目录与审计

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| RBAC（角色 / 权限 / 分配） | ✅ 已实现 | authz schema；角色→权限；分配到账户或法人实体 |
| 决策 API（gRPC Check / ListPermissions） | ✅ 已实现 | 令牌签发时取权限快照，写入 permissions 与 authz_ver |
| 决策缓存与版本失效 | 🚧 部分实现 | 版本号随令牌下发并随分配递增；服务端缓存未实现 |
| 属性条件 / 外挂 PDP | 🔭 v1.1 计划 | v1 内置 RBAC；Casbin.NET 为可选模块 |
| 组织、成员、邀请 | ✅ 已实现 | directory schema：组织（realm 内 slug 唯一）、成员生命周期、邀请（只存 token 哈希，原文仅返回一次）；含 gRPC ListMemberships/IsMember |
| 授权决策缓存 | ✅ 已实现 | 按 account+legalEntity 缓存权限快照，用 subject 版本号失效（角色/组变更即失效）；POST /api/v1/decisions/check 与 GET /api/v1/decisions/stats |
| 用户组 | ✅ 已实现 | 组 CRUD、成员增删、按组授予角色；权限解析展开组成员，成员变动递增版本使令牌刷新 |
| 上游 LDAP 联邦 | ✅ 已实现（基础） | POST /api/v1/auth/federation/ldap 用服务账号检索后二次绑定校验口令；按 mail 或 username 关联本地账户，成功签发登录票据，未关联 403；配置 Ldap:Enabled/Host/BaseDn/UserFilter |
| 上游 OIDC 联邦 | ✅ 已实现（OIDC） | 配置多个上游提供方；POST /api/v1/auth/federation/start 返回授权 URL（state + PKCE），回调校验 state 单次使用并验签上游 id_token（JWKS + aud），按 email 关联本地账户后签发登录票据；未关联返回 403 |
| 网关路由 | ✅ 已实现 | 路由表内置于代码，下游地址由 Gateway:Services:* 配置（缺省回落本机开发端口），任意环境可直接接线 |
| 运维文档 | ✅ 已实现 | 备份与恢复、密钥轮换、升级与回滚、上线安全检查四篇，并提供 scripts/backup.sh（pg_dump + 密钥包 + 校验和） |
| 反向代理支持 | ✅ 已实现 | 所有服务启用 ForwardedHeaders（X-Forwarded-Proto/For），IdP issuer 随 https 头切换；默认只信任 loopback，Proxy:TrustAll 显式放开 |
| Prometheus 指标 | ✅ 已实现 | 所有服务暴露 GET /metrics（http_requests_received_total、dotnet_*、process_*）；由 MapNervesDefaults 统一挂载 |
| 端到端冒烟 | ✅ 已实现 | scripts/e2e-smoke.sh 启动全部后端服务（独立端口段），walk 登录 API → OIDC → 令牌 → 网关管理 API；CI e2e 作业在 postgres service 上运行 |
| 仅迁移模式 | ✅ 已实现 | Database:MigrateOnly=true 时服务应用迁移后以 0 退出，供 Kubernetes 迁移 Job 使用；默认仍由 MigrateOnStart 控制 |
| 分片路由元数据 | ✅ 已实现（元数据层） | 账户→逻辑分片为纯函数（SHA-256 取模），routing_index 记录 shard 并建索引，ResolveIdentifier 返回分片；Identity 之外的数据面仍是单库，切换到多库需要部署级改造 |
| SCIM 2.0 | ✅ 已实现（核心子集） | /scim/v2/Users（创建、筛选、PUT 停用、删除）由 Identity 提供；/scim/v2/Groups（创建、成员替换、删除、筛选）由 Authorization 提供；网关转发，需 nerves:admin |
| 实名核验 Provider | 🧭 v1.0 计划 | 状态机已落地，Provider 抽象与流程未实现 |
| 数据主体导出/删除 | ✅ 已实现 | GET /api/v1/compliance/subjects/{id}/export 返回账户资料、同意书与留存策略；POST .../erase 创建删除请求、撤销全部有效同意书并停用账户；identity 新增 DisableAccount gRPC |
| Compliance.CN | ✅ 已实现（基础） | 同意书授予/查询/撤销（撤销幂等）、留存策略按类别 upsert、汇总统计；schema compliance；经网关 /api/v1/compliance/* 暴露，需 nerves:admin |
| 审计（事件、哈希链、检索、校验） | ✅ 已实现 | 每流 SHA-256 链；account.created / locked / disabled、auth.login.succeeded / failed；/api/v1/audit-events 与 /verify |
| 授权变更审计 | ✅ 已实现 | 角色创建/授予/撤销、分组创建/删除、成员增删均写入审计（authz.role.* / authz.group.*），经 gRPC 尽力投递，不影响主操作 |
| DataProtection 密钥持久化 | ✅ 已实现 | 统一 AddNervesDataProtection：配置 DataProtection:KeysDirectory 落到文件系统；非 Development 未配置则启动失败；compose 为 identity/idp 挂载独立卷 |
| 审计导出 | ✅ 已实现 | GET /api/v1/audit-events/export?format=csv\|json&limit=N 返回附件（文件名含时间戳）；CSV 按 RFC 4180 转义；需 nerves:admin |
| 审计归档 | ✅ 已实现 | 未归档事件按 UTC 日写成 NDJSON，附 sha256sum 清单；记录归档时间戳；POST /api/v1/audit-events/archive 手动触发，GET .../archive/status 查看进度；后台任务默认每 30 秒一轮 |
| 多分片启用与再平衡 | 🔭 v1.1 计划 | v1 交付分片就绪 |

## IdP / OIDC

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| OIDC Discovery 与 JWKS | ✅ 已实现 | /.well-known/openid-configuration 与 /.well-known/jwks |
| 授权码 + PKCE | ✅ 已实现 | /connect/authorize、/connect/token，端到端脚本实测 |
| 令牌签发 | ✅ 已实现 | ID / Access（含 sub、leid、name、email、permissions、authz_ver）/ Refresh |
| 注销 | ✅ 已实现 | /connect/logout，同时撤销该账户全部会话 |
| 用户信息端点 | ✅ 已实现 | /connect/userinfo 返回 sub/name/email/leid/permissions；无令牌 401 |
| Back-Channel Logout | ✅ 已实现 | POST /api/v1/auth/logout（Bearer）撤销全部会话并写审计；按配置向各 RP 的 backchannel_logout_uri 投递 RS256 签名的 logout_token（iss/aud/sub/iat/jti/events）；投递失败不阻断登出 |
| 上游 SAML（SP） | 🔭 v1.1 计划 | — |

## SDK

| 语言 | 状态 | 说明 |
| --- | --- | --- |
| .NET | ✅ 已实现 | Nerves.Client，dotnet pack 验证；26 个测试 |
| TypeScript | ✅ 已实现 | @nerves/sdk，pnpm build/typecheck/test 通过；25 个测试 |
| Python | ✅ 已实现 | nerves-sdk，pytest 通过；29 个测试 |
| Java / Go / PHP | 🔭 v1.1 计划 | 目录已预留说明 |

## 已知缺口（按优先级）

1. Gateway 仍是空壳：没有边缘路由、BFF 与账户页托管。
2. Directory 仍是空壳：组织 / 成员 / 邀请 / SCIM 未实现。
3. 管理后台 live 模式缺少 /api/v1/auth/login 与聚合层，目前以 mock 模式演示。
4. 集成测试（Testcontainers 真 PG）与 Docker 镜像构建尚未纳入 CI。
5. IdP 的 userinfo、Back-Channel Logout、MFA 流程、失败锁定待补。
