# 通知服务（Email 投递）

`Nerves.Notification` 负责把系统邮件排进投递日志并异步发送。它不渲染登录页，也不保存邮件正文以外的用户资料；每次投递都是 `notification` schema 里的一行记录。

## 能力

| 能力 | 说明 |
| --- | --- |
| 模板邮件 | 内置 `account.welcome`、`account.password-reset`、`security.sign-in`，支持 `{{name}}` 占位符 |
| 投递日志 | `notification.notification_message`：收件人、模板、主题、状态、尝试次数、错误、创建时间、发送时间 |
| 重试 | 后台 worker 定期扫描 `pending` 且到期的记录，指数退避，达到上限后标记 `failed` |
| 发送通道 | `Smtp:Disabled=true`（默认）使用 no-op sender，只写日志；否则通过 SMTP 投递 |
| 管理 API | 需要 `nerves:admin` 权限的 Bearer 令牌 |

## 管理 API

```text
POST /api/v1/notifications/email     创建一封模板邮件（进入 pending）
GET  /api/v1/notifications           投递日志，游标分页
GET  /api/v1/notifications/{id}      单条投递详情
```

创建请求：

```json
{
  "to": "user@example.com",
  "template": "account.welcome",
  "values": {
    "displayName": "张伟",
    "username": "zhang.wei",
    "issuer": "https://nerves.example.com/"
  }
}
```

- `to` 必须是纯邮箱地址：不接受显示名、换行或超长地址，避免 SMTP 头注入。
- 自定义 `subject`/`body` 目前被拒绝（400）；要新增文案请新增模板。
- 模板缺少占位符参数或模板不存在时返回 400，不会把 `{{resetUrl}}` 这样的内容发出去。

查询参数：`limit`（1–200，默认 50）、`cursor`（上一页返回的 `nextCursor`）、`status`（`pending` / `sent` / `failed`）、`to`（收件人精确匹配）。

响应：

```json
{
  "items": [
    {
      "id": "01a1c3b0-...",
      "to": "user@example.com",
      "template": "account.welcome",
      "subject": "欢迎加入 张伟",
      "status": "sent",
      "attempts": 0,
      "error": null,
      "createdAt": "2026-11-10T17:30:00+00:00",
      "sentAt": "2026-11-10T17:30:04+00:00",
      "nextAttemptAt": "2026-11-10T17:30:00+00:00"
    }
  ],
  "nextCursor": null
}
```

## 配置

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `Smtp:Disabled` | `true` | true 时使用 no-op sender，不连 SMTP |
| `Smtp:Host` / `Smtp:Port` | 空 / `587` | SMTP 服务器 |
| `Smtp:User` / `Smtp:Password` | 空 | 留空则不做 SMTP 认证；Helm 用 Secret 注入口令 |
| `Smtp:From` | `noreply@nerves.local` | 发件地址 |
| `Smtp:Tls` | `true` | STARTTLS / SMTPS |
| `Delivery:SweepSeconds` | `30` | worker 扫描间隔（5–3600） |
| `Delivery:MaxAttempts` | `5` | 最大尝试次数（1–20） |
| `Delivery:BaseDelaySeconds` | `60` | 首次重试等待 |
| `Delivery:MaxDelaySeconds` | `3600` | 退避上限 |
| `Delivery:BatchSize` | `20` | 单轮最多处理的记录数 |

重试时间线（默认值）：失败后 60s → 120s → 240s → 480s，第 5 次仍失败则 `failed`、不再重试。退避由 `RetryPolicy` 计算，记录在 `nextAttemptAt`，因此服务重启不会丢失进度。

## 迁移

`notification` schema 由 EF Core 迁移创建（`20261110172320_InitialNotification`）。开发环境设置 `Database:MigrateOnStart=true` 即可自动建表；Kubernetes 用 `Database:MigrateOnly=true` 的迁移 Job 执行（Helm chart 已包含）。

```bash
dotnet ef database update \
  --project src/services/Nerves.Notification \
  --startup-project src/services/Nerves.Notification
```

## 本地验证

```bash
# 1. 启动服务（默认 Development，端口 5108，no-op sender）
dotnet run --project src/services/Nerves.Notification

# 2. 用管理员账户走 IdP 获取令牌（见部署文档），然后创建一封邮件
curl -sS http://127.0.0.1:5108/api/v1/notifications/email \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"to":"user@example.com","template":"account.welcome","values":{"displayName":"张伟","username":"zhang.wei","issuer":"https://nerves.example.com/"}}'

# 3. 观察状态从 pending 变为 sent（worker 每 5 秒一轮）
curl -sS "http://127.0.0.1:5108/api/v1/notifications?status=sent" \
  -H "Authorization: Bearer $TOKEN"
```

## 自动化实测

`src/services/Nerves.Notification/scripts/notification-smoke.py` 是一段无第三方依赖的实测脚本，用真实的 IdP 登录流程拿令牌，再验证投递状态流转。服务需先以对应配置启动：

```bash
# 阶段 1：Smtp:Disabled=true，期望 pending -> sent
Smtp__Disabled=true Delivery__SweepSeconds=2 \
  dotnet run --project src/services/Nerves.Notification
NOTIFY_BASE=http://127.0.0.1:5108 python3 src/services/Nerves.Notification/scripts/notification-smoke.py phase1

# 阶段 2：Smtp:Disabled=false 且指向不可达端口，期望 pending -> failed
# 覆盖：201 创建、401 无令牌、400 非法收件人/未知模板/缺占位符、状态过滤
```

脚本使用 `E2E_IDP` / `NOTIFY_BASE` / `E2E_USERNAME` / `E2E_PASSWORD` 环境变量，默认指向本地 compose 端口（IdP 5101、notification 5108）。

## 保留与隐私

投递日志保存渲染后的主题与正文，因此可能包含重置口令链接等短期敏感内容。建议按部署策略定期清理 `sent` / `failed` 的历史行（例如按 `createdAt` 保留 30 天），不要把该表当成长期归档；SMTP 凭据只经配置注入，从不落库。

## 排障

| 现象 | 排查 |
| --- | --- |
| 记录一直是 `pending` | worker 是否启动；`nextAttemptAt` 是否在未来；日志里有 `NotificationSweepFailed` |
| 记录很快变成 `failed` | 看该行 `error`；检查 `Smtp:*`、网络是否允许出站 25/465/587；`attempts` 应等于 `Delivery:MaxAttempts` |
| 401 | 令牌的权限快照里缺少 `nerves:admin`，或 `Authentication:Authority` 指向了错误的 IdP |
| 400 `unknown template` | 使用 `account.welcome` / `account.password-reset` / `security.sign-in` |
| 400 `missing template values` | 按模板要求的占位符补齐 `values` |

日志使用结构化事件：`EmailNoOpDelivered`、`EmailDeliveryFailed`、`EmailDeliveryExhausted`、`NotificationSweep`、`NotificationSweepFailed`。
