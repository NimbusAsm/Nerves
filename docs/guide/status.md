# 实现状态

> 本页是文档中“是否已实现”的**唯一事实来源**。核对时间：2026-11-07，仓库 `main`
> 的 M0 第 2 周。代码推进很快，精确到文件与端点的状态请以 `src/`、`proto/` 与
> git 历史为准；本页描述的是**能力**。

## 标注约定

| 标记 | 含义 |
| --- | --- |
| ✅ 已实现 | 当前仓库有可运行代码，或本次交付的产物已通过构建 / 校验 |
| 🧩 契约 / 模型已落地 | protobuf 契约或 EF 领域模型在仓库里，但还没有对外服务实现 |
| 🧭 v1.0 计划 | 方案承诺在 v1.0 交付，**当前未实现** |
| 🔭 v1.1 计划 | 明确排到 v1.1，**当前不可用**，不要按已交付来集成 |
| 🚧 进行中 | 仓库里已有代码或测试，但尚未达到可发布 / 可端到端使用的状态 |

::: tip 读法
导航栏、接入指南与 FAQ 中的每个能力都会带上以上标记。看到 🔭 v1.1 就表示
“设计上预留、v1.0 不交付”，看到 🧭 v1.0 就表示“方案里有，但当前分支还不能用”，
看到 🚧 表示“代码已在仓库中出现，但还不能当成可用的交付物”。
:::

## 平台与部署

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 六个核心服务骨架（gateway / idp / identity / session / authorization / directory） | ✅ 已实现 | 容器内监听 8080，提供 `/healthz`、`/readyz`、`/api/v1/meta` 与 `/openapi/v1.json` |
| 共享服务默认值（ProblemDetails、健康检查、OpenTelemetry） | ✅ 已实现 | `Nerves.ServiceDefaults`；OTLP endpoint 由 `OTEL_EXPORTER_OTLP_ENDPOINT` 控制 |
| 领域基元（`Result` / `Reason` / UUIDv7 / 游标分页 / 领域事件） | ✅ 已实现 | `Nerves.Core`，零第三方依赖 |
| protobuf 契约与 CI 契约门禁 | ✅ 已实现 | identity / session / authorization 三个 proto；`buf lint` + `buf breaking` |
| 开发用 Docker Compose（PG + NATS + 六服务） | ✅ 已实现 | `deploy/docker/docker-compose.yml`，端口 5100–5105 |
| Helm Chart（`deploy/helm/nerves`） | ✅ 已实现 | `helm lint --strict`（Helm 3 / 4）与 `kubeconform` 校验通过；**未在真实集群安装验证** |
| 文档站（VitePress） | ✅ 已实现 | `docs/`，`pnpm build` 通过 |
| 管理面板（Admin 前端） | 🚧 进行中 | `apps/admin` 已有 Vue 3 界面源码，默认使用 mock 数据源；真实管理 API 未实现 |
| 管理聚合层（console-api） | 🧭 v1.0 计划 | 尚未实现，Admin 目前没有真实后端可连 |
| 审计服务（audit） | 🧭 v1.0 计划 | Chart 预留了可选服务开关，但仓库尚无实现 |
| 通知服务（notification） | 🧭 v1.0 计划 | 同上 |
| CLI（迁移 / 初始化 / 诊断） | 🧭 v1.0 计划 | 当前迁移使用 `dotnet ef` |

## 身份、认证与会话

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 法人实体（LegalEntity）领域模型与 EF 持久化 | ✅ 已实现 | 含实名摘要（HMAC 结果）唯一索引、实名状态机、`xmin` 并发令牌 |
| 网络账户（NetworkAccount）领域模型与 EF 持久化 | ✅ 已实现 | realm 内用户名唯一、账户状态机、服务账户约束 |
| 首个 identity 迁移 | ✅ 已实现 | `InitialIdentity`；仓库记录已在 PostgreSQL 18.6 上执行验证 |
| 凭据模型（口令 / TOTP / Passkey / 恢复码） | 🧩 契约 / 模型已落地 | 数据模型在推进中；**凭据校验、MFA、Passkey 流程未实现** |
| 登录标识路由索引（identifier → account） | 🧩 契约 / 模型已落地 | 表结构与索引在推进中；注册 / 登录链路未接入 |
| 管理 API（`/api/v1/legal-entities`、`/api/v1/accounts` …） | 🧭 v1.0 计划 | 当前只有健康检查与 meta 端点 |
| 口令哈希与凭据校验 | 🧭 v1.0 计划 | 目标：PBKDF2-HMAC-SHA512 / 100k（`PasswordHasher` V3），常量时间比较 |
| 登录 / 登出 / 失败锁定 | 🧭 v1.0 计划 | 当前没有登录页与认证链路 |
| 会话、刷新令牌族、吊销 | 🧭 v1.0 计划 | session 服务目前是空壳；Redis 为可选组件 |
| MFA：TOTP / Passkey / 恢复码 | 🧭 v1.0 计划 | — |
| 账户切换（`switch-account`） | 🧭 v1.0 计划 | 同一法律实体名下多账户免重认证切换 |
| 上游联邦：OIDC / LDAP | 🧭 v1.0 计划 | — |

## 授权、目录与合规

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 授权契约（`AuthorizationService`） | 🧩 契约 / 模型已落地 | 目前只有 `GetServiceInfo`；`ListAccountPermissions` 在 identity 契约中 |
| RBAC（角色 / 权限 / 作用域） | 🧭 v1.0 计划 | 目标模型：global / app / org 作用域，权限码 `app:resource:action` |
| 属性条件与决策 API（PDP） | 🧭 v1.0 计划 | 内置进程内判定；默认拒绝 |
| 决策缓存与 `authz_ver` 失效 | 🧭 v1.0 计划 | — |
| 组织、成员、组、邀请 | 🧭 v1.0 计划 | directory 服务目前是空壳 |
| SCIM 2.0 | 🧭 v1.0 计划 | 方案排 v1.0 |
| 实名核验 Provider 契约与状态机 | 🧭 v1.0 计划 | 状态机已在领域模型落地；Provider 抽象与流程未实现 |
| `Compliance.CN` 通道适配、日志留存、国密 | 🔭 v1.1 计划 | v1 只交契约与状态机 |
| 外挂 PDP（OpenFGA / SpiceDB 连接器） | 🔭 v1.1 计划 | v1 内置 RBAC；Casbin.NET 为可选模块 |
| 上游 SAML（SP）、企业微信 / 钉钉 | 🔭 v1.1 计划 | v1 的 SAML 只做上游联邦设计，不交付 |
| 多分片启用与再平衡 | 🔭 v1.1 计划 | v1 只交付“分片就绪” |

## IdP / OIDC

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| OIDC Discovery 与 JWKS | 🧭 v1.0 计划 | 目标：`/.well-known/openid-configuration`、`/.well-known/jwks.json` |
| 授权码 + PKCE | 🧭 v1.0 计划 | `/connect/authorize`、`/connect/token` |
| 令牌签发（ID / Access / Refresh）与轮换 | 🧭 v1.0 计划 | ES256 为主、RS256 兼容；JWKS 多 `kid` 并存 |
| 用户信息、注销、内省 | 🧭 v1.0 计划 | `/connect/userinfo`、`/connect/endsession`、`/connect/revoke` |

## SDK 与接入

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| .NET / TypeScript / Python SDK | 🚧 进行中 | `sdk/` 下已有三门客户端源码与测试用例；管理 API 尚未实现，也还没有 NuGet / npm / PyPI 发布包 |
| Java / Go / PHP SDK | 🔭 v1.1 计划 | 方案明确 v1.1；Go 使用 Functional Options 而非链式 |
| 标准 OIDC 客户端接入 | 🧭 v1.0 计划 | IdP 端点交付后，任意合规 OIDC 客户端即可接入 |

## 可观测与运维

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| `/healthz` / `/readyz` | ✅ 已实现 | 所有服务固定返回 ok；identity 额外注册了 EF 健康检查 |
| OpenTelemetry trace / metrics | ✅ 已实现 | ASP.NET Core 与 HttpClient 自动埋点；配置 OTLP 后导出 |
| 结构化日志与全链路 `trace_id` | 🧭 v1.0 计划 | 当前为框架默认日志 |
| 备份 / 恢复演练脚本 | 🧭 v1.0 计划 | 文档给出目标流程，仓库暂无脚本 |
| Helm 生产级 HA（PG 主备、NATS 集群） | 🧭 v1.0 计划 | 当前 Chart 内置单节点 PG / NATS 仅用于开发与小规模 |

## 如何核对

```bash
# 每个服务的端点
curl http://localhost:5006/healthz
curl http://localhost:5006/readyz
curl http://localhost:5006/api/v1/meta
curl http://localhost:5006/openapi/v1.json

# 契约
dotnet build
buf lint
```

如果你发现本页与代码不一致，请以代码为准并提 issue 或 PR 修正文档。
