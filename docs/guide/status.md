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
| 七个可运行服务（gateway / idp / identity / session / authorization / directory / audit） | ✅ 已实现 | 均可启动，提供 /healthz、/readyz、/api/v1/meta、/openapi/v1.json；gateway 与 directory 目前只有骨架端点 |
| 共享服务默认值 | ✅ 已实现 | ProblemDetails、健康检查、OpenTelemetry、管理 API 令牌过滤 |
| 领域基元 | ✅ 已实现 | Result / Reason / UUIDv7 / 游标分页 / 领域事件 |
| protobuf 契约与契约门禁 | ✅ 已实现 | identity / session / authorization / audit 四个 proto；buf lint + breaking 进 CI |
| CI（三作业） | ✅ 已实现 | dotnet（build/test/format/pack/buf）+ frontend（admin 与 TS SDK）+ python（pytest），均绿 |
| 开发 compose | ✅ 已实现 | PG 18.6 + NATS 2.15.2 + 六个服务 |
| Helm Chart | ✅ 已实现 | helm lint --strict 与 kubeconform 通过；未在真实集群安装验证 |
| 管理后台（Vue 3） | ✅ 已实现 | 9 个页面、mock/live 切换、中英 i18n、35 个测试 |
| 管理聚合层（console-api） | 🧭 v1.0 计划 | Admin 的 live 模式仍需逐服务直连；未实现 BFF 聚合 |
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
| OIDC 登录（浏览器） | ✅ 已实现 | Razor 登录页 + cookie 会话；gRPC 验密 |
| 会话服务 | ✅ 已实现 | session schema、登录建会话、登出撤销、管理端列出/撤销 |
| MFA：TOTP / Passkey / 恢复码 | 🧭 v1.0 计划 | 凭据类型已建模，流程未实现 |
| 账户切换（switch-account） | 🧭 v1.0 计划 | — |
| 上游联邦：OIDC / LDAP | 🧭 v1.0 计划 | — |
| 失败锁定与限流 | 🧭 v1.0 计划 | 目前只有状态机，未接入限流器 |

## 授权、目录与审计

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| RBAC（角色 / 权限 / 分配） | ✅ 已实现 | authz schema；角色→权限；分配到账户或法人实体 |
| 决策 API（gRPC Check / ListPermissions） | ✅ 已实现 | 令牌签发时取权限快照，写入 permissions 与 authz_ver |
| 决策缓存与版本失效 | 🚧 部分实现 | 版本号随令牌下发并随分配递增；服务端缓存未实现 |
| 属性条件 / 外挂 PDP | 🔭 v1.1 计划 | v1 内置 RBAC；Casbin.NET 为可选模块 |
| 组织、成员、组、邀请 | 🧭 v1.0 计划 | directory 服务仍是空壳 |
| SCIM 2.0 | 🧭 v1.0 计划 | — |
| 实名核验 Provider | 🧭 v1.0 计划 | 状态机已落地，Provider 抽象与流程未实现 |
| Compliance.CN 适配 / 国密 / 归档 | 🔭 v1.1 计划 | v1 只交契约与状态机 |
| 审计（事件、哈希链、检索、校验） | ✅ 已实现 | 每流 SHA-256 链；account.created / locked / disabled、auth.login.succeeded / failed；/api/v1/audit-events 与 /verify |
| 审计归档到对象存储 | 🧭 v1.0 计划 | 当前只有数据库表 |
| 多分片启用与再平衡 | 🔭 v1.1 计划 | v1 交付分片就绪 |

## IdP / OIDC

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| OIDC Discovery 与 JWKS | ✅ 已实现 | /.well-known/openid-configuration 与 /.well-known/jwks |
| 授权码 + PKCE | ✅ 已实现 | /connect/authorize、/connect/token，端到端脚本实测 |
| 令牌签发 | ✅ 已实现 | ID / Access（含 sub、leid、name、email、permissions、authz_ver）/ Refresh |
| 注销 | ✅ 已实现 | /connect/logout，同时撤销该账户全部会话 |
| 用户信息端点 | 🧭 v1.0 计划 | /connect/userinfo 未实现 |
| Back-Channel Logout | 🧭 v1.0 计划 | 未实现 |
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
