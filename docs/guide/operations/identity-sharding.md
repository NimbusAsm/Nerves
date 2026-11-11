# Identity 多库分片

identity 的账户数据默认在单个数据库里。打开分片后，账户表按 `accountId` 分布到多个数据库，
全局数据（法人实体、登录标识路由索引）仍在一个库中。

> 分片是**部署形态**，不是 API 变化：同一个账户无论落在哪个库，登录、管理台与 SDK 调用方式都不变。

## 拓扑

```text
ConnectionStrings:Default         全局库  legal_entity, routing_index, verification_attempt
Sharding:ConnectionStrings:0      分片 0  network_account, account_credential,
Sharding:ConnectionStrings:1      分片 1  passkey_challenge, password_reset_token, outbox_event
...                               ...
```

- 账户 → 分片的映射是账户 id 的纯函数（SHA-256 取模），不需要查表。
- `routing_index` 同时记录 `Shard`，供运维核对与扩容前重算。
- 只有全局库参与唯一性判定（用户名按 realm、邮箱/手机全局唯一），分片库之间没有外键。

## 开启方式

```json
{
  "Sharding": {
    "Enabled": true,
    "ShardCount": 2,
    "ConnectionStrings": {
      "0": "Host=db0;Database=nerves;Username=nerves;Password=...",
      "1": "Host=db1;Database=nerves;Username=nerves;Password=..."
    }
  }
}
```

- `Enabled` 默认为 `false`，此时所有读写都走 `ConnectionStrings:Default`，行为与单库部署完全一致。
- 开启后每个 `Sharding:ConnectionStrings:i` 都是必填项：缺失会让服务启动失败，而不是把账户悄悄留在全局库。
- `ShardCount` 与连接串数量必须匹配；改变分片数需要用 `nerves shard reconcile` 重算路由索引。

## 迁移

不新增迁移文件：全局库与每个分片库各自执行同一套 identity 迁移，未使用的表无害。
设置 `Database:MigrateOnStart=true`（开发）或 `Database:MigrateOnly=true`（Kubernetes Job）时，
`IdentityMigrator` 会依次迁移全局库和每一个分片；任一库失败即整体失败。

## 创建补偿

创建账户是两阶段写入：

1. 生成账户 id，按映射算出分片；
2. 在分片库写入 `network_account` 与 `account_credential`；
3. 在全局库写入路由索引。

第 3 步失败（例如标识已被占用）时，服务删除第 2 步写入的行再返回冲突错误。
补偿本身失败只记错误日志：没有路由索引的账户无法被登录解析到，属于不可达数据。

## 读写路径

| 操作 | 路径 |
| --- | --- |
| 按 id 读账户 | `ShardFor(id)` 单库查询 |
| 按用户名 / 邮箱 / 手机号 | 先查全局 `routing_index`，再按 accountId 查分片 |
| 账户列表、统计 | 遍历所有分片后合并（游标仍按 id 排序） |
| 法人实体下账户 | 遍历所有分片按 `LegalEntityId` 过滤 |
| 停用 / 锁定 / 改口令 | 分片内完成，随后撤销会话 |
| 领域事件 outbox | 写入账户所属分片；投递任务逐库清扫 |
| SCIM / 目录同步 / gRPC 契约 | 按标识 fan-out 解析，命中后按 id 路由 |
| 健康检查 | `/healthz` 逐个检查全局库与所有分片 |

## 运维要点

- **备份**：全局库与每个分片库都要备份；只恢复其中一个会产生「有账户没路由」或「有路由没账户」。
- **扩容**：先把新分片库建好并迁移，再调整 `ShardCount` 与连接串，最后运行 `nerves shard reconcile`。
  重算期间写入仍使用新映射，因此建议在低峰期执行。
- **缩容**：先把要移除分片上的账户按新映射迁到目标分片，再改配置；v1 不提供自动搬迁工具。
- **可观测性**：`/api/v1/stats` 的账户数已经跨分片求和，便于与各库的 `count(*)` 对账。

## 限制

- 账户表与全局表在同一个 PostgreSQL 实例时仍共享连接池；真正的多实例部署需要给每个分片配置独立的连接串。
- 跨分片的事务不支持：创建补偿是最终一致，不是分布式事务。
- 分片数变更不是在线操作；`RoutingIndexEntry.Shard` 与映射函数必须同时更新。
