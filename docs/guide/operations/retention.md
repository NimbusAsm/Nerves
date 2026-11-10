# 保留策略与审计 outbox

> 状态：🧭 v1.0 计划能力，本页随 PR 一起评审。

## 保留策略执行（A3）

`Compliance.CN` 现在会按 `retention_policy` 表里的 `RetentionDays` 清理到期数据。

```json
{
  "Retention": {
    "Enabled": false,
    "IntervalMinutes": 60,
    "StartDelaySeconds": 10,
    "BatchSize": 500,
    "ConsentCategory": "consent",
    "ErasureCategory": "erasure-request"
  }
}
```

| 键 | 说明 |
| --- | --- |
| `Enabled` | 总开关，**默认 false**；未开启时后台任务直接退出 |
| `IntervalMinutes` | 扫描间隔，运行期夹在 1–1440 |
| `BatchSize` | 每类每轮最多处理的记录数，夹在 1–5000 |
| `ConsentCategory` | 对应 `retention_policy.category`，缺失则跳过该类 |
| `ErasureCategory` | 同上，用于 erasure 请求 |

行为：

1. 同意记录：`GrantedAt` 早于 `now - RetentionDays` 的记录被删除；
2. erasure 请求：完成时间超过保留期的请求先**匿名化**（`SubjectId` 变为 `anonymized`），
   再等一个完整保留期后才删除，避免审计锚点被瞬间抹掉；
3. 没有对应策略时整轮安全跳过，不会误删；
4. 每轮结束写一条 outbox 事件 `compliance.retention.swept`（带计数），由 dispatcher 投递到审计服务。

## 审计 outbox（B15）

`session` / `authz` / `directory` / `compliance` 四个 schema 各有一张 `outbox_event` 表：

| 列 | 说明 |
| --- | --- |
| `Type` | 审计动作名，例如 `session.revoked` |
| `TargetType` / `TargetId` | 被操作对象 |
| `Payload` | JSON 细节 |
| `DispatchedAt` / `Attempts` | 投递状态与尝试次数 |

业务写入和 outbox 行在**同一个事务**里提交；后台 `OutboxDispatcher` 每 5 秒批量投递，
成功后标记 `DispatchedAt`，失败累加 `Attempts`，达到 10 次后停止重试（行保留供排查）。
审计服务短暂不可用不再丢事件，这是对原先 best-effort 写法的替换。

迁移只做加法：四个服务各新增一张 `outbox_event` 表与一个 `(DispatchedAt, OccurredAt)` 索引。
