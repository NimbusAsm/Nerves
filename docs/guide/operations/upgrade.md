# 升级与回滚

## 升级步骤

1. **备份**：执行 `scripts/backup.sh`。
2. **迁移**：Helm 的 pre-upgrade Job 以 `Database:MigrateOnly=true` 运行七个服务镜像，完成 schema 变更后退出。
3. **发布**：滚动更新各 Deployment；就绪探针使用 `/readyz`，失败不会切流。
4. **验证**：
   - 网关 `/healthz` 200；
   - 登录一次并换取令牌；
   - `/api/v1/audit-events/verify` 返回 `valid: true`；
   - `/metrics` 有新增请求计数。

## 回滚

- 应用层：`helm rollback <release> <revision>`。
- 数据层：迁移只做加法时通常无需回滚；若必须回退，用备份恢复并接受丢失升级后的写入。

## 兼容性约定

- 迁移脚本**只做加法**（新增列/表），不重命名或删除列。
- API 破坏性变更必须通过 `buf breaking` 与 OpenAPI 差异检查。


## Shard count change

1. Back up the database and stop traffic to identity.
2. Dry run: `Sharding__ShardCount=<new> Sharding__Recompute=true Sharding__RecomputeDryRun=true`, and compare the printed distribution.
3. Apply: same command without the dry-run flag; it prints `scanned` / `changed`.
4. Re-run the apply command: `changed` must be 0 (idempotent).
5. Start identity with the new `Sharding:ShardCount` so writes use the same mapping.
