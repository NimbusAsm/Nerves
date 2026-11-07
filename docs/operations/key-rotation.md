# 密钥轮换

> **状态**：IdP 签名密钥、会话密钥与 PAT 都尚未实现（🧭 v1.0 计划）。
> 本文给出目标流程与当前可执行的数据库 / NATS / Secret 轮换步骤。
> 实名 HMAC 密钥存在一个**必须先解决的 schema 约束**，见下文。

## 密钥清单

| 密钥 | 位置 | 轮换周期 | 当前状态 |
| --- | --- | --- | --- |
| IdP 签名私钥（ES256 / RS256） | KMS / Vault / Secret | 90 天 | 🧭 v1.0 计划 |
| 数据库口令 | K8s Secret / 外部凭据 | 90 天或按合规 | ✅ 可手工轮换 |
| NATS token / 凭据 | K8s Secret / 外部凭据 | 90 天 | ✅ 可手工轮换 |
| 实名 HMAC 密钥 | KMS / Vault | 长周期 | ⚠️ 受 schema 约束 |
| 会话 / 刷新令牌签名密钥 | Secret | 90 天 | 🧭 v1.0 计划 |
| PAT | 数据库（哈希） | 按策略 | 🧭 v1.0 计划 |
| TLS 证书 | cert-manager / Ingress | 自动 | 取决于集群 |

原则：

- **重叠窗口**：新密钥先发布，旧密钥保留到旧令牌 / 旧连接自然过期；
- **带版本**：密钥有 `kid` / `key_version`，不能只有一个“当前密钥”；
- **可审计**：轮换操作记录到审计（事件系统落地后）；
- **可回滚**：保留旧密钥直到确认新密钥工作正常；
- **不入仓库**：密钥只存在于 KMS / Secret，不写入 Git、镜像或日志。

## IdP 签名密钥（规划）

目标：ES256 为主，RS256 兼容；JWKS 同时发布多个 `kid`；签名密钥 90 天轮换。

步骤：

1. 在 KMS 生成新密钥，记录 `kid`；
2. 更新 JWKS，使 **旧 + 新** 公钥同时可查；
3. 切换签发使用新 `kid`；
4. 等待 ≥ 最长令牌 TTL（access 15 分钟，ID 5 分钟）+ 时钟偏移；
5. 确认资源服务的 JWKS 缓存已刷新（后台刷新间隔应小于等待窗口）；
6. 从 JWKS 移除旧公钥，归档旧私钥（按合规保留期）。

注意：refresh token 是不透明串（哈希存储），不依赖签名密钥；但会话 / 令牌版本
（`token_ver`）的吊销语义要在轮换后验证。

## 数据库口令

### 内置 PostgreSQL

`POSTGRES_PASSWORD` **只在数据目录初始化时生效**；直接改 values 不会修改已有库的
口令。正确顺序：

```bash
# 1) 在数据库中改口令
kubectl -n nerves exec -it nerves-postgresql-0 -- \
  psql -U nerves -d nerves -c "ALTER ROLE nerves WITH PASSWORD '新口令';"

# 2) 更新应用 Secret（示例：existingSecret 场景）
kubectl -n nerves create secret generic nerves-database \
  --from-literal=connection-string='Host=...;Database=nerves;Username=nerves;Password=新口令' \
  --dry-run=client -o yaml | kubectl apply -f -

# 3) 滚动重启所有使用该 Secret 的服务
kubectl -n nerves rollout restart deploy/nerves-identity
```

- 步骤 1 与 3 之间会有认证失败窗口；生产应在维护窗口执行，或使用双角色过渡：
  新建角色 → 更新 Secret → 重启 → 删除旧角色；
- 若使用外部 PG，优先用其凭据管理（IAM auth / 自动轮换）；
- 不要同时改口令与连接串格式。

## NATS token

NATS 的 token 认证只有一个 token，轮换会造成短暂重连：

1. 生成新 token；
2. 更新 NATS 配置 / Secret；
3. 滚动重启 NATS（单节点会中断；集群逐节点重启）；
4. 更新应用侧 `Nats__Url` Secret 并滚动重启服务；
5. 验证连接与 JetStream 消费。

生产建议使用 NATS 的 NKeys / JWT 多用户模型，支持用户级吊销与滚动更新，而不是共享 token。

## 实名 HMAC 密钥（重要约束）

现状：`RealNameId.Hash` 只存摘要，**没有 key version 列**。如果直接换密钥：

- 旧记录的摘要无法用新密钥验证 / 匹配；
- 唯一索引仍然存在，但“同一证件”会算出不同摘要，导致重复实体。

因此当前**不要直接轮换**实名 HMAC 密钥。目标流程（需要 schema 演进）：

1. 增加 `id_doc_hash_key_version`（或独立 keyring 表），迁移为可空并回填当前版本；
2. 新密钥发布为 `key_version=N+1`，旧密钥保留在 keyring 中；
3. 新核验使用新版本；旧记录在下次核验 / 登录时按旧版本验证并重算摘要（rehash-on-verify）；
4. 全部重算完成后移除旧密钥；
5. 整个过程需要幂等迁移与回滚预案。

在 keyring 落地前，实名 HMAC 密钥应按“长期密钥”管理：严格访问控制、备份、审计，
不要按季度轮换。

## Kubernetes Secret 更新

```bash
# 生成 / 更新 Secret（不要 kubectl edit 手写 base64）
kubectl -n nerves create secret generic nerves-nats \
  --from-literal=url='nats://user:新token@nats.example.internal:4222' \
  --dry-run=client -o yaml | kubectl apply -f -

# 触发滚动重启（环境变量不会热更新）
kubectl -n nerves rollout restart deploy/nerves-gateway
kubectl -n nerves rollout status deploy/nerves-gateway
```

GitOps 场景用 External Secrets / Sealed Secrets，避免把明文放进 Git。

## 紧急轮换

发生泄露时：

1. **先吊销**：撤销会话 / PAT / 令牌版本（能力落地后），必要时禁用账户；
2. 轮换泄露的密钥，并缩短重叠窗口；
3. 检查审计与访问日志，确定影响范围；
4. 强制所有用户重新认证（`token_ver` 递增）；
5. 记录事故与修复，更新 runbook。

## Chart 自动生成凭据的注意点

Chart 在未提供 `postgresql.auth.password` / `nats.auth.token` 时用 `randAlphaNum`
生成，并通过 `lookup` 在 `helm upgrade` 时复用。但 `helm template` / Argo CD
等无集群查询的渲染每次会得到新随机值——**GitOps 必须使用 existingSecret**，
否则每次同步都可能改掉数据库 / NATS 凭据。
