# Helm 部署（生产）

Chart 位置：[`deploy/helm/nerves`](https://github.com/NimbusAsm/Nerves/tree/main/deploy/helm/nerves)。
生产默认部署形态是 Kubernetes + Helm（D16）；Docker Compose 只用于开发与演示。

::: warning 当前 Chart 的验证边界
Chart 已通过 `helm lint --strict`（Helm 3.22 与 4.3）与 `helm template` +
`kubeconform` 的 schema 校验；但**没有在真实 Kind / Minikube 上执行过 `helm install`**，
默认镜像也尚未发布。完整清单见 Chart 的
[README](https://github.com/NimbusAsm/Nerves/blob/main/deploy/helm/nerves/README.md#验证范围)。
:::

## 前置条件

| 项 | 要求 |
| --- | --- |
| Kubernetes | 1.27+ |
| Helm | 3.10+ 或 Helm 4 |
| StorageClass | 启用内置 PostgreSQL / NATS 持久化时需要默认 StorageClass；没有则关闭持久化 |
| 镜像 | 六个服务镜像可被节点拉取，或通过 `kind load` / `minikube image load` 导入 |
| Ingress | 可选；Chart 不安装 Ingress Controller |

## 默认值会创建什么

默认 values 面向本地集群：

- 每个服务 1 个副本（gateway / idp / identity / session / authorization / directory）；
- 内置单节点 **PostgreSQL 18.6** 与 **NATS 2.15.2 JetStream**，各 8Gi PVC；
- 无 Ingress（ClusterIP），通过 `port-forward` 访问；
- 随机生成数据库口令与 NATS token，并用 `lookup` 在升级时保留；
- 服务以 UID 1654 非 root 运行、只读根文件系统、drop 全部 capabilities。

```bash
helm install nerves ./deploy/helm/nerves \
  --namespace nerves --create-namespace \
  --set image.tag=0.1.0
```

```bash
kubectl -n nerves get pods
kubectl -n nerves port-forward svc/nerves-gateway 8080:80
curl -fsS http://127.0.0.1:8080/healthz
```

> 没有默认 StorageClass 时加
> `--set postgresql.persistence.enabled=false --set nats.persistence.enabled=false`；
> 代价是 Pod 重建后数据丢失，只适合演示。

## 构建并加载镜像

默认镜像命名是 `ghcr.io/nimbusasm/nerves/<service>:<tag>`。仓库根目录：

```bash
SERVICES="gateway idp identity session authorization directory"
TAG=0.1.0
for s in $SERVICES; do
  dotnet="Nerves.${s^}"
  docker build -f deploy/docker/Dockerfile \
    --build-arg PROJECT="src/services/${dotnet}/${dotnet}.csproj" \
    --build-arg ENTRY_DLL="${dotnet}.dll" \
    -t "ghcr.io/nimbusasm/nerves/${s}:${TAG}" .
done

# Kind
for s in $SERVICES; do
  kind load docker-image "ghcr.io/nimbusasm/nerves/${s}:${TAG}" --name nerves
done

# 或 Minikube
for s in $SERVICES; do
  minikube image load "ghcr.io/nimbusasm/nerves/${s}:${TAG}"
done
```

生产请推送到自己的 registry，并覆盖：

```yaml
image:
  repository: registry.example.internal/nerves
  tag: "0.1.0"
  # 或使用 digest（优先级更高）
  # digest: sha256:...
```

## 外部 PostgreSQL / NATS

优先级：`existingSecret` > `url` > 内置组件；内置组件与外部配置**互斥**，
同时设置会被模板拒绝并给出明确错误。

### 外部数据库（推荐 existingSecret）

```bash
kubectl create namespace nerves --dry-run=client -o yaml | kubectl apply -f -

kubectl -n nerves create secret generic nerves-database \
  --from-literal=connection-string='Host=pg.example.internal;Port=5432;Database=nerves;Username=nerves;Password=...'
```

```yaml
database:
  existingSecret: nerves-database
  secretKey: connection-string
postgresql:
  enabled: false
```

也可以用 `database.url` 直接给连接串，Chart 会把它写进自己管理的 Secret；
但这样连接串会出现在 values 里，不推荐用于生产。

### 外部 NATS

```bash
kubectl -n nerves create secret generic nerves-nats \
  --from-literal=url='nats://user:password@nats.example.internal:4222'
```

```yaml
nats:
  existingSecret: nerves-nats
  urlSecretKey: url
  enabled: false
```

生产建议 3 节点以上 NATS JetStream（或托管 NATS）；单节点只适合开发。

## Ingress、HPA 与 PDB

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt
  hosts:
    - host: id.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: nerves-tls
      hosts:
        - id.example.com

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  services: [gateway, idp, identity, session, authorization, directory]

podDisruptionBudget:
  enabled: true
  maxUnavailable: 1
```

- Ingress 的所有 path 都路由到 gateway；不支持按服务拆 host。
- HPA 只对 `autoscaling.services` 中列出的启用服务创建；启用后忽略该服务的
  `replicaCount`。
- PDB 的 `minAvailable` 与 `maxUnavailable` 只能设置一个；都为空时使用
  `maxUnavailable: 1`。

## 迁移

Chart **不会**自动执行数据库迁移（迁移与发布解耦，D18）。发布前在构建机或
迁移 Job 中执行：

```bash
export PATH="$HOME/.dotnet:$PATH"
export ConnectionStrings__Default='Host=...;Database=nerves;Username=nerves;Password=...'
dotnet tool restore
dotnet ef database update \
  --project src/services/Nerves.Identity \
  --startup-project src/services/Nerves.Identity
```

当前仓库只有 identity 的迁移；其它服务还没有 schema。v1.0 会引入
`nerves-cli migrate`，仍遵循 expand / contract。

## 升级与回滚

```bash
helm upgrade nerves ./deploy/helm/nerves -n nerves \
  -f values.prod.yaml --set image.tag=0.2.0 \
  --atomic --wait --timeout 10m

helm history nerves -n nerves
helm rollback nerves 1 -n nerves --wait
```

- `--atomic` 失败自动回滚 Kubernetes 资源；不回滚数据库迁移。
- 迁移必须 expand 兼容：先加列/表，新版本双写，下个版本再删旧列。
- 详细流程与回滚决策见 [升级与回滚](/operations/upgrade)。

## 关键 values

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `image.repository` | `ghcr.io/nimbusasm/nerves` | 镜像前缀 |
| `image.tag` | `""` | 空则用 Chart.appVersion |
| `image.digest` | `""` | 非空则忽略 tag |
| `database.existingSecret` / `secretKey` | `""` / `connection-string` | 外部数据库 Secret |
| `postgresql.enabled` | `true` | 内置单节点 PG |
| `postgresql.persistence.size` | `8Gi` | 数据卷大小 |
| `nats.enabled` / `nats.url` / `nats.existingSecret` | `true` / `""` / `""` | 消息 |
| `ingress.enabled` | `false` | 所有 path → gateway |
| `autoscaling.enabled` | `false` | 为列出服务创建 HPA |
| `podDisruptionBudget.enabled` | `false` | 为每个启用服务创建 PDB |
| `config.otelExporterOtlpEndpoint` | `""` | 空则关闭 OTLP 导出 |
| `services.<name>.extraEnv` / `extraEnvFrom` | `[]` | 注入应用密钥（签名私钥、HMAC 密钥等） |

## 生产检查清单

- [ ] 镜像固定 tag 或 digest，并使用自己的 registry；
- [ ] 外部 PostgreSQL 有 HA、PITR / WAL 归档与恢复演练；
- [ ] NATS 3 节点以上或托管服务；
- [ ] 数据库 / NATS 凭据由 External Secrets / Vault 注入，不用 Chart 随机生成；
- [ ] Ingress 终止 TLS，限制请求体大小与超时；
- [ ] 每个服务 ≥ 2 副本（先确认无状态），配置 PDB 与 HPA；
- [ ] 配置 OTel Collector 与告警（登录成功率、P95、错误率）；
- [ ] 备份计划与恢复演练（见 [备份与恢复](/operations/backup)）；
- [ ] NetworkPolicy 限制 ingress → gateway、服务间 gRPC、服务 → PG/NATS；
- [ ] 若启用内置 PostgreSQL，确认集群 Pod Security 允许其 entrypoint；
  更推荐直接使用外部 PG。

## 卸载

```bash
helm uninstall nerves -n nerves
```

PVC 默认保留；需要彻底删除时先确认数据已备份：

```bash
kubectl -n nerves get pvc
kubectl -n nerves delete pvc data-nerves-postgresql-0 data-nerves-nats-0
```
