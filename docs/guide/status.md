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
| 管理后台（Vue 3） | ✅ 已实现 | 9 个页面、mock/live 切换、中英 i18n、35 个测试 |
| 管理后台 live 模式 | ✅ 已实现 | VITE_USE_MOCK=false 走 OIDC 授权码 + PKCE（登录 API 取票 → 跳 IdP → 回调换令牌），令牌仅存内存 |
| 管理聚合层（console-api） | 🧭 v1.0 计划 | 目前由网关按路径转发，未做响应聚合 |
| 通知服务（notification） | 🧭 v1.0 计划 | — |
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
| MFA：Passkey | 🧭 v1.0 计划 | 凭据类型已建模，流程未实现 |
| 账户切换 | ✅ 已实现 | 同一法人名下账户可凭当前 Bearer 令牌调用 /api/v1/auth/switch-account 换取新登录票据（不重输口令）；跨法人 403。实测新令牌 sub 为目标账户、leid 不变 |
| 上游联邦：OIDC / LDAP | 🧭 v1.0 计划 | — |
| 失败锁定 | ✅ 已实现 | 15 分钟内 5 次失败锁定 15 分钟（内存实现；多副本需换 Redis）；限流器未接入 |

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
| SCIM 2.0 | ✅ 已实现（核心子集） | /scim/v2/Users（创建、筛选、PUT 停用、删除）由 Identity 提供；/scim/v2/Groups（创建、成员替换、删除、筛选）由 Authorization 提供；网关转发，需 nerves:admin |
| 实名核验 Provider | 🧭 v1.0 计划 | 状态机已落地，Provider 抽象与流程未实现 |
| Compliance.CN | ✅ 已实现（基础） | 同意书授予/查询/撤销（撤销幂等）、留存策略按类别 upsert、汇总统计；schema compliance；经网关 /api/v1/compliance/* 暴露，需 nerves:admin |
| 审计（事件、哈希链、检索、校验） | ✅ 已实现 | 每流 SHA-256 链；account.created / locked / disabled、auth.login.succeeded / failed；/api/v1/audit-events 与 /verify |
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
