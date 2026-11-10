# 控制台聚合 API（Console API）

`Nerves.ConsoleApi` 是管理台背后的**只读聚合层**：它自己不存数据，一次请求并行调用若干服务，
把结果合成一个响应。这样管理台一个问题只发一次请求，而不是在浏览器里串行扇出。

开发环境监听 **5109**（见 `deploy/docker/docker-compose.yml`），Helm 里对应 `services.console`。

## 端点

所有端点都在 `nerves:admin` 策略下，需要带调用者的 Bearer 令牌；令牌会**原样透传**给下游服务，
所以控制台不会成为越权的后门。

| 端点 | 聚合了什么 | 说明 |
| --- | --- | --- |
| `GET /api/v1/console/overview` | identity `/api/v1/stats`、authorization `/api/v1/decisions/stats`、compliance `/api/v1/compliance/summary`，以及每个服务的 `/api/v1/meta` | 首页概览：账户/法人数量、PDP 缓存、同意记录、各服务版本与延迟 |
| `GET /api/v1/console/account/{id}` | identity `/api/v1/accounts/{id}`、session `/api/v1/accounts/{id}/sessions`、audit `/api/v1/audit-events?actorId={id}` | 账户详情页；账户不存在时返回 404 |
| `GET /api/v1/console/entity/{id}` | identity `/api/v1/legal-entities/{id}` + 账户列表 | 法人详情页；实体不存在时返回 404 |

上游 JSON **原样透传**（`account`、`sessions`、`auditEvents` 等字段就是服务自己的响应），
控制台只负责编排，不复制第二份契约。

## 部分失败语义

单个下游失败**不会**让整个请求失败：

- 响应仍是 `200`，失败信息进 `warnings` 数组，例如
  `"session /api/v1/accounts/{id}/sessions timed out after 2s"`；
- 对应字段为 `null`，调用方据此显示"暂时不可用"而不是空白；
- 只有**主资源** 404（账户或法人不存在）才返回 404。

超时由 `Console:TimeoutSeconds`（默认 3 秒）控制，一次请求最多等到这些调用全部返回或超时。

## 配置

| 配置键 | 默认值 | 说明 |
| --- | --- | --- |
| `Console:Services:<name>` | 见下表 | 下游服务基地址，必须是绝对 URI |
| `Console:TimeoutSeconds` | 3 | 单次下游调用超时，取值 1–30 |
| `Console:MaxAuditEvents` | 20 | 账户详情里内联的审计事件条数，1–200 |
| `Console:AccountScanLimit` | 200 | 法人详情页扫描账户的上限，1–1000 |

必填服务（缺一个就拒绝启动）：`identity`、`session`、`authorization`、`audit`、`compliance`。

```text
Console__Services__identity=http://127.0.0.1:5102/
Console__Services__session=http://127.0.0.1:5103/
Console__Services__authorization=http://127.0.0.1:5104/
Console__Services__audit=http://127.0.0.1:5105/
Console__Services__compliance=http://127.0.0.1:5107/
```

## 健康与指标

| 路径 | 语义 |
| --- | --- |
| `/healthz` | 进程存活（固定 ok） |
| `/readyz` | 进程就绪（固定 ready） |
| `/healthz/downstream` | 逐个探测下游 `/healthz`；全部可达 `Healthy`，否则 `Degraded`（**不会**让 Pod 重启） |
| `/metrics` | Prometheus 指标 |
| `/api/v1/meta` | 服务名、版本、契约版本 |

自定义指标：`console_downstream_failures_total`（按 `service`、`status` 维度）与
`console_downstream_duration_milliseconds`（按 `service` 维度）。

## 已知限制

- **法人详情页的账户列表是有上限扫描**：identity 的账户列表还没有 `legalEntityId` 过滤参数，
  控制台只取一页（`AccountScanLimit`）再按 `legalEntityId` 过滤。截断时响应里
  `accountsTruncated=true` 并给出 warning。等 identity 补上过滤参数后应改为服务端过滤。
- **网关尚未转发 `/api/v1/console`**：管理台目前需要单独配置控制台地址（5109）。
  把控制台挂到网关需要改 `Nerves.Gateway` 的路由表，属于后续改动。

## 示例

```bash
TOKEN=$(curl -fsS -X POST http://127.0.0.1:5101/api/v1/auth/login \
  -H 'content-type: application/json' \
  -d '{"username":"admin","password":"ChangeMe!123"}' | jq -r .accessToken)

curl -fsS http://127.0.0.1:5109/api/v1/console/overview \
  -H "authorization: Bearer ${TOKEN}" | jq .
```
