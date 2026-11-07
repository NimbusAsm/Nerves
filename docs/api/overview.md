# API 概览

> **状态**：当前只有健康检查、meta 与 OpenAPI 可用；管理 API、OIDC 端点均是
> 🧭 v1.0 计划。详见 [实现状态](/guide/status)。

## 三个 API 面

| 面 | 前缀 | 协议 | 使用方 | 状态 |
| --- | --- | --- | --- | --- |
| OIDC / OAuth | `/connect/*`、`/.well-known/*` | HTTP | 各产品的 OIDC 客户端 | 🧭 v1.0 计划 |
| 管理 API | `/api/v1/*` | REST + JSON | Admin、CLI、自动化、SDK | 🧭 v1.0 计划 |
| 内部 API | gRPC | protobuf | 服务之间 | 🧩 仅契约 |

## 当前真正可用的端点

六个服务（gateway / idp / identity / session / authorization / directory）都暴露：

| 方法 | 路径 | 响应 | 说明 |
| --- | --- | --- | --- |
| GET | `/healthz` | `{"status":"ok"}` | 存活探针 |
| GET | `/readyz` | `{"status":"ready"}` | 就绪探针 |
| GET | `/api/v1/meta` | `{"service":"nerves-gateway","version":"0.1.0.0","contract":"v1"}` | 服务名、程序集版本、契约版本 |
| GET | `/openapi/v1.json` | OpenAPI 文档 | 仅包含当前已注册的端点 |

示例：

```bash
curl -fsS http://localhost:5006/healthz
# {"status":"ok"}

curl -fsS http://localhost:5006/api/v1/meta
# {"service":"nerves-gateway","version":"0.1.0.0","contract":"v1"}
```

::: note 就绪探针尚未反映依赖
`/readyz` 目前固定返回 ready；identity 虽然注册了 EF 健康检查，但没有把
`/readyz` 绑定到数据库探针。Kubernetes 部署时不要认为 ready 就代表数据库可用。
:::

## 管理 API 约定（v1.0 目标）

| 项 | 规则 |
| --- | --- |
| 错误 | RFC 9457 ProblemDetails + `traceId` |
| 分页 | 游标：`?limit=50&cursor=...`；禁止 offset 深翻页 |
| 过滤 / 排序 | `?filter[status]=active&sort=-createdAt` |
| 并发 | `ETag` + `If-Match` |
| 幂等 | 写请求支持 `Idempotency-Key`（24 小时窗口） |
| 鉴权 | 管理面板用会话 Cookie + CSRF；自动化用 PAT |
| 权限 | 每个端点声明权限码，未声明应拒绝启动 |
| 版本 | `/api/v1` 保持 12 个月兼容；破坏性变更走 `/api/v2` |

::: warning 当前错误响应尚未统一
骨架已注册 ProblemDetails 服务，但通用的 404 / 405 仍由默认 StatusCodePages
处理。统一为 RFC 9457 属于 v1.0 计划，接入方不要依赖当前错误格式。
:::

## 资源骨架（v1.0 目标）

```text
/api/v1/legal-entities                    法律实体（实名主体）
/api/v1/legal-entities/{id}/accounts
/api/v1/accounts                          网络账户
/api/v1/accounts/{id}/credentials         凭据（只写不读）
/api/v1/accounts/{id}/sessions
/api/v1/roles  /permissions  /policies
/api/v1/organizations  /organizations/{id}/members
/api/v1/applications                      应用目录
/api/v1/audit-events                      只读
/api/v1/signing-keys
/api/v1/routing-index                     运维用，受限
```

这些都是**规划**，当前请求会得到 404。

## OIDC 端点（v1.0 目标）

```text
GET  /.well-known/openid-configuration
GET  /.well-known/jwks.json
GET  /connect/authorize        POST /connect/token
GET  /connect/userinfo         POST /connect/introspect
POST /connect/revoke           GET  /connect/endsession
POST /connect/logout           POST /connect/switch-account（扩展）
```

`switch-account` 是本项目独有的扩展：同一法律实体名下的多个网络账户之间切换，
不需要重新输入口令；返回新的 `sub`，`leid` 不变，并记录审计。

## 约定细节

### 游标分页

- 请求：`?limit=50&cursor=<opaque>`
- 响应：`{ "items": [...], "nextCursor": "<opaque>|null" }`
- 游标是不透明字符串，客户端不得解析或拼接；
- 不提供 offset 分页：深翻页在大表上会退化，且无法由单分片完成。

### 幂等

- 所有写接口接受 `Idempotency-Key`；
- 同一个 key + 同一个请求体在 24 小时内返回首次结果；
- 事件消费按 `event_id` 去重（至少一次投递）；
- 重试必须复用同一个 key。

### 错误格式（目标）

```json
{
  "type": "https://nerves.example/problems/account-not-found",
  "title": "Account not found",
  "status": 404,
  "detail": "No account with the supplied identifier.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

安全约束：认证相关错误不得区分“用户不存在”与“口令错误”。

### 版本

- URL 主版本：`/api/v1`；
- 兼容承诺 12 个月；
- 破坏性变更走 `/api/v2`，两个版本并存一个周期；
- 内部 gRPC 契约以 `proto/` 为准，`buf breaking` 阻止破坏性修改。
