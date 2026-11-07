# 升级与回滚

> 迁移策略遵循 **expand / contract**（D18）：先加后删、跨两个版本。
> 应用可以回滚；数据库迁移不回滚。

## 升级前检查

- [ ] 已阅读版本变更与迁移说明；
- [ ] 已做数据库备份（见 [备份与恢复](/operations/backup)）；
- [ ] 已确认当前版本与目标版本的兼容矩阵（v1.0 起支持 N-1 小版本直接升级）；
- [ ] 已确认迁移是 expand 兼容（只加列 / 表，不删不改语义）；
- [ ] 镜像已推送并由目标集群可拉取；
- [ ] 有回滚窗口与负责人；
- [ ] 监控与告警可用（至少能看健康检查与错误率）。

## 发布顺序

服务之间只通过契约通信，理论上可独立发布；但涉及契约变更时按依赖方向：

```text
contracts → core → gateway → identity → session → authorization → directory → idp → console-api → admin
```

规则：**先发布被调用方，再发布调用方**；破坏性契约变更必须两个版本并存。

## 1. 执行数据库迁移

当前仓库没有 `nerves-cli migrate`，使用 `dotnet ef`：

```bash
export PATH="$HOME/.dotnet:$PATH"
export ConnectionStrings__Default='Host=...;Database=nerves;Username=nerves;Password=...'

dotnet tool restore
dotnet ef database update \
  --project src/services/Nerves.Identity \
  --startup-project src/services/Nerves.Identity
```

约束：

- 服务启动时**不会自动迁移**；
- 迁移只允许加法（`add column if not exists` / `create table if not exists`）；
- 不得在同一个 PR 里“加列 + 用列 + 删列”；
- 语义变更要在同一条迁移里回填历史数据；
- 已发布的迁移永不修改，修正靠新增一条。

## 2. 滚动升级

```bash
helm upgrade nerves ./deploy/helm/nerves -n nerves \
  -f values.prod.yaml \
  --set image.tag=0.2.0 \
  --atomic --wait --timeout 10m
```

- `--atomic`：失败自动回滚本次 Helm 变更；
- `--wait`：等待 Deployment / StatefulSet 就绪；
- 默认滚动策略 `maxUnavailable: 0`、`maxSurge: 1`，单副本时仍会短暂中断；
- 生产每个服务 ≥ 2 副本并配置 PDB。

## 3. 升级后验证

```bash
kubectl -n nerves rollout status deploy/nerves-gateway
kubectl -n nerves get pods
kubectl -n nerves logs deploy/nerves-gateway --tail=100

curl -fsS https://id.example.com/healthz
curl -fsS https://id.example.com/api/v1/meta
```

若已有业务链路，还要验证：登录、令牌刷新、登出、一次授权拒绝用例。
保留至少一个旧副本一段时间，便于对比错误率。

## 回滚

### 应用回滚（安全）

```bash
helm history nerves -n nerves
helm rollback nerves <REVISION> -n nerves --wait
```

前提：数据库已完成的是 **expand** 迁移（新增列 / 表），旧版本代码不依赖新结构。

### 数据库不自动回滚

- `helm rollback` 只回滚 Kubernetes 资源；
- 已执行的迁移不会撤销；
- 若迁移包含 contract（删列 / 改类型），应用回滚可能不兼容；
- 最后手段是从备份恢复（会丢失恢复点之后的数据），必须走事故流程。

### 决策树

```text
升级后异常
  ├─ 健康检查失败 / 启动崩溃 → helm rollback
  ├─ 错误率升高但数据库兼容 → helm rollback，保留迁移
  ├─ 迁移是 contract 且旧代码不兼容 → 评估前滚修复；必要时从备份恢复
  └─ 数据损坏 → 停止写入 → 从备份 / PITR 恢复 → 校验 → 恢复流量
```

## 密钥与配置变更

- 修改 `values.yaml` 后用同一 `helm upgrade` 应用；
- Secret 变更后需要重启 Pod 才会被读取（环境变量不会热更新）：

  ```bash
  kubectl -n nerves rollout restart deploy/nerves-identity
  ```

- 签名密钥轮换必须重叠发布（旧 `kid` 保留到旧令牌过期），见
  [密钥轮换](/operations/key-rotation)。

## 当前差距

| 项 | 状态 |
| --- | --- |
| 自动化升级夹具（上一版数据库 → 升级） | 🧭 v1.0 计划 |
| `nerves-cli migrate` | 🧭 v1.0 计划 |
| 金丝雀 / 按百分比灰度 | 🧭 v1.0 计划（B8 特性开关） |
| 蓝绿部署脚本 | 🧭 v1.0 计划 |
