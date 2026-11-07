# 备份与恢复

> **状态**：🧭 v1.0 计划提供可执行的备份 / 恢复脚本与演练夹具。
> 本文给出目标流程与当前可用的手工命令；按此操作前请先在测试集群演练。

## 备份范围

| 对象 | 是否事实源 | 备份方式 | 频率建议 |
| --- | --- | --- | --- |
| PostgreSQL | ✅ 是 | 逻辑全量 + WAL 归档 / PITR | 每日全量 + 持续 WAL |
| NATS JetStream | ❌ 否（事件可重放，前提是 outbox 已实现） | 流备份 / PVC 快照 | 每日 |
| Kubernetes Secrets | ✅ 是（凭据、签名密钥） | 加密后存 Vault / KMS | 每次轮换 |
| Helm values / 清单 | ✅ 是（部署意图） | Git（不含明文密钥） | 每次变更 |
| 审计对象存储（未来） | ✅ 是 | 对象存储版本化 / WORM | 按合规策略 |
| 容器镜像 | — | registry 保留策略 | 按发布策略 |

> 生产应使用托管 PostgreSQL 或 CloudNativePG / Patroni 的 PITR 能力；
> Chart 内置的单节点 PostgreSQL 只适合开发，**不能**作为生产备份方案。

## PostgreSQL

### 逻辑备份（单库）

在集群内执行，避免把大文件经 `kubectl cp` 中转：

```bash
kubectl -n nerves exec nerves-postgresql-0 -- \
  pg_dump -U nerves -d nerves -Fc -f /tmp/nerves-$(date +%F).dump
```

导出到本机：

```bash
kubectl -n nerves cp \
  nerves-postgresql-0:/tmp/nerves-$(date +%F).dump \
  ./nerves-$(date +%F).dump
```

同时备份角色（`pg_dump` 不含全局角色）：

```bash
kubectl -n nerves exec nerves-postgresql-0 -- \
  pg_dumpall -U nerves --globals-only -f /tmp/globals.sql
kubectl -n nerves cp nerves-postgresql-0:/tmp/globals.sql ./globals.sql
```

### 恢复（逻辑）

```bash
# 恢复到空库或维护窗口内的库；--clean 会先删对象，务必确认目标
kubectl -n nerves exec -i nerves-postgresql-0 -- \
  pg_restore -U nerves -d nerves --clean --if-exists < ./nerves-2026-11-07.dump
```

要点：

- `pg_dump` 在单个事务快照内导出，逻辑备份是一致的；
- 备份文件包含 `identity` schema 与 `__EFMigrationsHistory`；
- 恢复后必须核对迁移历史与表数量，不能只看“命令成功”。

### 物理备份 / PITR（生产）

| 方案 | 说明 |
| --- | --- |
| CloudNativePG | 原生支持 WAL 归档到对象存储、按时间点恢复（PITR）、副本与故障切换 |
| Patroni + pgBackRest | 虚拟机或自管 K8s；pgBackRest 负责全量 / 增量 / WAL |
| 托管 PG | 使用云厂商的自动备份与 PITR |

目标（需按部署实测，不是承诺）：

- RPO：≤ 5 分钟（依赖 WAL 归档间隔）；
- RTO：≤ 1 小时（依赖恢复演练与数据量）。

## NATS JetStream

NATS 不是事实源：在 outbox 与事件重放落地之前，NATS 丢数据意味着审计 / 通知可能
丢失。备份策略：

1. 优先使用 `nats` CLI 的 stream backup / restore（命令随 CLI 版本略有差异，
   执行前 `nats stream --help` 确认）；
2. 或在维护窗口对 PVC 做一致性快照；
3. **不要**在运行中直接 `cp` 数据目录（可能得到撕裂的快照）。

单节点 NATS 的 `/data` 是 PVC；生产应部署 3 节点以上集群并由 NATS 自身复制。

## Secrets 与密钥

```bash
# 只导出、不要提交到 git；尽快导入 Vault / KMS
kubectl -n nerves get secret nerves-database nerves-nats -o yaml > nerves-secrets.yaml
```

- 签名私钥、实名 HMAC 密钥等应用密钥应由 KMS / Vault 管理；
- 备份介质必须加密，访问权限最小化；
- 恢复演练时使用**测试密钥**，不要把生产密钥导入测试环境。

## 恢复演练清单

1. 在隔离 namespace 或独立集群恢复数据库；
2. 校验 schema 与迁移历史：

   ```bash
   psql "$ConnectionStrings__Default" -c '\dt identity.*'
   psql "$ConnectionStrings__Default" -c 'select "MigrationId" from identity."__EFMigrationsHistory" order by 1;'
   psql "$ConnectionStrings__Default" -c 'select count(*) from identity.legal_entity;'
   ```

3. 启动服务，确认 `/healthz`、`/readyz`；
4. 若已有登录链路（v1.0 之后），用测试账户完成登录、刷新、登出；
5. 记录实际 RTO，更新 runbook；
6. 每季度至少一次；恢复失败必须当作事故处理。

## 当前差距

| 项 | 状态 |
| --- | --- |
| 备份 / 恢复脚本入库 | 🧭 v1.0 计划 |
| 升级夹具与恢复演练报告 | 🧭 v1.0 计划 |
| 审计导出到对象存储 | 🔭 v1.1 计划（`Compliance.CN`） |
| 内置 PG 的 HA / PITR | ❌ 不提供；请使用外部 PG |
