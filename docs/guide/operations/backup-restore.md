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

## 恢复

1. 停止 identity 与 idp。
2. `pg_restore --clean --if-exists --no-owner -d <数据库> nerves-<时间戳>.dump`。
3. 解压密钥包到各服务的 `DataProtection:KeysDirectory`。
4. 启动服务，访问 `/readyz` 确认就绪。
5. 抽查：管理员能登录；已登记 TOTP 的账户仍能完成二次验证。

## 保留与验证

- 每日全量 + 保留策略由备份系统决定，Nerves 不内置调度。
- 每季度做一次恢复演练，并核对 `sha256sum -c`。

## 注意

- 审计表是追加式哈希链，恢复后 `GET /api/v1/audit-events/verify` 应仍为 `valid: true`。
- 密钥包与数据库备份应分开存放，且都需加密。
