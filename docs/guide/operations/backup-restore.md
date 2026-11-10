# 备份与恢复

## 备份内容

| 数据 | 位置 | 说明 |
| --- | --- | --- |
| PostgreSQL | 全部 schema | 账户、凭据、会话、授权、审计、目录、合规 |
| DataProtection 密钥 | identity 与 idp 的 `DataProtection:KeysDirectory` | **丢失后 MFA 秘密与 Cookie 无法解密** |
| 审计归档 | `Archive:Directory`（可选） | 已写入的 NDJSON 与校验和 |

## 执行

```bash
PGHOST=<数据库主机> PGPORT=5432 PGUSER=nerves PGPASSWORD=<口令> PGDATABASE=nerves \
  KEYS_DIR=<密钥目录的父目录> ./scripts/backup.sh /backup/nerves
```

脚本产出 `*.dump`（pg_dump custom）、`*.keys.tar.gz` 与 `*.sha256`。

## Helm 定时备份（可选）

`deploy/helm/nerves` 默认不创建备份任务（`backup.enabled=false`）。生产集群若没有
托管备份或 CloudNativePG 之类的 PITR 方案，可让 Chart 创建一个 CronJob：

```yaml
backup:
  enabled: true
  schedule: "0 2 * * *"   # 集群时区（多数集群为 UTC）
  retentionDays: 7        # 本地与远端同时按此清理
  persistence:
    size: 20Gi            # 或 existingClaim 指向已有 PVC
  # 可选：再用 rclone 上传到 S3 兼容对象存储
  objectStorage:
    enabled: true
    remote: "s3:my-bucket/nerves-backup"
    existingSecret: rclone-config   # 含 rclone.conf 的 Secret
```

行为说明：

- initContainer 用 `postgres:<tag>` 跑 `pg_dump --format=custom --no-owner`，写入
  `/backup/nerves-<UTC 时间戳>.dump` 并生成同名 `.sha256`；
- `backup.retentionDays` 到期后由容器内的 `find -mtime` 清理；启用对象存储时
  rclone 也会 `delete --min-age` 清理远端；
- 使用外部数据库时必须显式给出 `backup.database.host/username/database` 与口令
  Secret（Chart 用 `fail` 报错，不会静默跳过）；
- **密钥包不在 CronJob 范围内**：`DataProtection` 密钥仍由 `scripts/backup.sh`
  单独归档，二者要分开存放。

## 恢复

1. 停止 identity 与 idp。
2. `pg_restore --clean --if-exists --no-owner -d <数据库> nerves-<时间戳>.dump`。
3. 解压密钥包到各服务的 `DataProtection:KeysDirectory`。
4. 启动服务，访问 `/readyz` 确认就绪。
5. 抽查：管理员能登录；已登记 TOTP 的账户仍能完成二次验证。

## 保留与验证

- 每日全量 + 保留策略由备份系统决定；只有显式打开 `backup.enabled` 时 Chart 才会
  创建 CronJob，默认仍由外部备份系统负责调度。
- 每季度做一次恢复演练，并核对 `sha256sum -c`。

## 注意

- 审计表是追加式哈希链，恢复后 `GET /api/v1/audit-events/verify` 应仍为 `valid: true`。
- 密钥包与数据库备份应分开存放，且都需加密。


## 恢复演练

```bash
./scripts/restore-drill.sh /backup/nerves
```

脚本会校验 sha256、把最新 dump 恢复到临时库 `nerves_restore_drill`、检查 7 个 schema
与迁移历史行数，然后删除临时库（`KEEP=1` 可保留以便排查）。主机没有 pg 客户端时，
用 `PSQL_CMD` / `PG_RESTORE_CMD` 指向容器：

```bash
PSQL_CMD="docker compose -f deploy/docker/docker-compose.yml exec -T postgres psql -U nerves" \
PG_RESTORE_CMD="docker compose -f deploy/docker/docker-compose.yml exec -T postgres pg_restore -U nerves --no-owner --clean --if-exists" \
./scripts/restore-drill.sh /backup/nerves
```

建议每月至少演练一次；失败即视为最高优先级事件。
