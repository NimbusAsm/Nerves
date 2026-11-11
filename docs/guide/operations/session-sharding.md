# Session 多库分片

session 服务支持把会话数据按 `accountId` 分散到多个数据库。默认关闭，单库部署行为完全不变。

## 配置

```json
{
  "Sharding": {
    "Enabled": true,
    "ShardCount": 2,
    "ConnectionStrings": {
      "0": "Host=<host>;Database=nerves_session_0;Username=<user>;Password=<pw>",
      "1": "Host=<host>;Database=nerves_session_1;Username=<user>;Password=<pw>"
    }
  }
}
```

`Sharding:Enabled=false` 时使用 `ConnectionStrings:Default`，与 1.0 行为一致。

## 路由规则

| 操作 | 路由 |
| --- | --- |
| 创建会话 | `SHA-256(accountId) % ShardCount` 对应分片 |
| 按账户列出 / 撤销全部会话 | 该账户的分片 |
| 按 sessionId 撤销 | 依次查各分片（id 不携带分片信息） |
| 过期清扫 / outbox 投递 | 遍历全部分片 |

## 迁移

`Database:MigrateOnStart=true` 或 `Database:MigrateOnly=true` 会对**每个分片**执行 EF 迁移，逐库建表。

## 扩容与数据迁移

扩容时用一次性重平衡把旧分片库的会话搬到新分片：

```bash
Sharding__Enabled=true Sharding__ShardCount=N \
Sharding__ConnectionStrings__0=... \
Sharding__PreviousShardCount=M \
Sharding__PreviousConnectionStrings__0=... \
Sharding__Rebalance=true Sharding__RebalanceDryRun=true \
  dotnet Nerves.Session.dll    # 先看 scanned/moved 再退出
```

去掉 `RebalanceDryRun` 再执行一次；复跑应为 `moved=0`。旧库的 outbox 行进留在原库投递，等排空后再下线。

## 验证

- 集成测试 `tests/Nerves.Session.IntegrationTests`：双库落库、按账户列表、跨分片按 id 撤销、2→4 扩容重平衡。

## 边界

- 跨分片没有分布式事务：session 数据按账户独立，不需要跨分片一致性。
