# Nerves Helm Chart

把 Nerves 的六个核心服务（gateway / idp / identity / session / authorization / directory）
部署到 Kubernetes。Chart 同时内置**可选**的单节点 PostgreSQL 18 与 NATS JetStream，
默认值面向 Kind / Minikube；生产环境应改用外部 PostgreSQL / NATS。

> 当前仓库处于 M0 骨架阶段：服务已提供 `/healthz`、`/readyz`、`/api/v1/meta`
> 与 `/openapi/v1.json`，登录 / OIDC / 管理 API 尚未实现。Chart 可以安装，但
> 除健康检查外的业务能力仍待后续里程碑交付。详见仓库 `docs/`。

## 验证范围

本 Chart 在交付时执行过：

| 检查 | 结果 |
| --- | --- |
| `helm lint --strict`（Helm 3.22.1 与 Helm 4.3.1） | 通过（仅 `icon is recommended` 提示） |
| `helm template` 默认 values | 21 个资源全部渲染成功 |
| `kubeconform -strict`（Kubernetes 官方 JSON Schema） | 0 invalid / 0 error |
| 外部数据库 + 外部 NATS、existingSecret、Ingress、HPA、PDB、digest、关闭持久化等组合 | 均通过渲染与 schema 校验 |

另外，本地 Docker 冒烟验证了依赖镜像：`postgres:18.6-alpine` 的 `PGDATA=/var/lib/postgresql/18/docker`
与 `/var/lib/postgresql` 卷挂载一致；`nats:2.15.2-alpine` 可以以 UID 1000 运行，并支持
`-js` / `-sd` / `--auth` 参数。

**未验证项**（本机没有可用的 Kubernetes 集群，也没有已发布的镜像）：

- 未在真实 Kind / Minikube 上执行 `helm install`，也未验证 Pod 启动、就绪与滚动升级；
- 默认镜像 `ghcr.io/nimbusasm/nerves/<service>:0.1.0` **尚不存在**，需要按下方步骤自行构建并加载；
- 内置 PostgreSQL / NATS 的数据持久化、PVC 绑定与探针行为未做集群实测；
- Ingress 未绑定具体 Ingress Controller，TLS 由集群侧（如 cert-manager）提供；
- `helm test` 的 smoke-test 镜像 tag 已通过 registry manifest 确认存在，但未在集群中实际执行。

## 前置条件

- Kubernetes **1.27+**，Helm **3.10+** 或 Helm 4；
- 若启用内置 PostgreSQL / NATS 的持久化，需要集群有默认 StorageClass
  （Kind、Minikube 现代版本均自带 `standard`；没有时见下方“无 StorageClass”做法）；
- 各服务镜像可被节点拉取（或使用 `kind load` / `minikube image load`）。

## 必填项

默认 values 不需要额外输入即可渲染（数据库口令与 NATS token 自动生成）。以下场景**必须**显式提供：

- **生产镜像**：`image.repository` 与 `image.tag`（或 `image.digest`）。默认镜像
  `ghcr.io/nimbusasm/nerves/<service>:0.1.0` 尚未发布；
- **生产数据库**：`database.existingSecret` 或 `database.url`，并设置
  `postgresql.enabled=false`；内置 PostgreSQL 只适合开发；
- **生产消息**：`nats.existingSecret` 或 `nats.url`，并设置 `nats.enabled=false`；
- **启用的 Ingress**：每个 `ingress.hosts[]` 必须有 `host` 与至少一个 `paths`；
- **GitOps / Argo CD**：必须使用 existingSecret 或固定 `postgresql.auth.password` /
  `nats.auth.token`，不要依赖随机生成（无集群查询的渲染每次都会变）。

## 安装

### 1. 构建并加载镜像（Kind / Minikube）

Chart 不含镜像构建。仓库根目录执行：

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

# Kind（集群名按实际修改）
for s in $SERVICES; do
  kind load docker-image "ghcr.io/nimbusasm/nerves/${s}:${TAG}" --name nerves
done

# 或 Minikube
for s in $SERVICES; do
  minikube image load "ghcr.io/nimbusasm/nerves/${s}:${TAG}"
done
```

> 生产环境应把同样命名的镜像推送到自己的 registry，并通过 `image.repository`
> 或各服务的 `services.<name>.image.repository` 覆盖。

### 2. 安装

默认 values（单副本、内置 PostgreSQL + NATS、无 Ingress）：

```bash
helm install nerves ./deploy/helm/nerves \
  --namespace nerves --create-namespace \
  --set image.tag=0.1.0
```

确认状态：

```bash
kubectl -n nerves get pods
kubectl -n nerves port-forward svc/nerves-gateway 8080:80
curl http://127.0.0.1:8080/healthz
curl http://127.0.0.1:8080/api/v1/meta
```

> 没有默认 StorageClass 的集群：加 `--set postgresql.persistence.enabled=false
> --set nats.persistence.enabled=false`。这样 Pod 重建后数据会丢失，只适合演示。

### 3. 生产示例

`values.prod.yaml`：

```yaml
image:
  tag: "0.1.0"
  repository: registry.example.internal/nerves

services:
  gateway: { replicaCount: 3 }
  idp: { replicaCount: 2 }
  identity: { replicaCount: 2 }
  session: { replicaCount: 2 }
  authorization: { replicaCount: 2 }
  directory: { replicaCount: 2 }

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

podDisruptionBudget:
  enabled: true
  maxUnavailable: 1

# 外部 PostgreSQL：推荐 existingSecret（GitOps 友好）
database:
  existingSecret: nerves-database
  secretKey: connection-string
postgresql:
  enabled: false

# 外部 NATS
nats:
  existingSecret: nerves-nats
  urlSecretKey: url
  enabled: false
```

```bash
kubectl create namespace nerves --dry-run=client -o yaml | kubectl apply -f -

kubectl -n nerves create secret generic nerves-database \
  --from-literal=connection-string='Host=pg.example.internal;Port=5432;Database=nerves;Username=nerves;Password=...'
kubectl -n nerves create secret generic nerves-nats \
  --from-literal=url='nats://user:password@nats.example.internal:4222'

helm upgrade --install nerves ./deploy/helm/nerves \
  --namespace nerves --create-namespace \
  -f values.prod.yaml --atomic --wait --timeout 10m
```

### 关键配置

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `image.repository` | `ghcr.io/nimbusasm/nerves` | 服务镜像前缀；实际镜像为 `<前缀>/<service>:<tag>` |
| `image.tag` | `""`（用 Chart.appVersion） | 发布版本 |
| `image.digest` | `""` | 非空时用 `repo@digest`，忽略 tag |
| `database.url` | `""` | 外部 Npgsql 连接串；要求 `postgresql.enabled=false` |
| `database.existingSecret` / `secretKey` | `""` / `connection-string` | 外部连接串 Secret；要求关闭内置 PG |
| `nats.url` / `nats.existingSecret` | `""` / `""` | 外部 NATS；要求关闭内置 NATS |
| `postgresql.enabled` | `true` | 内置单节点 PostgreSQL 18.6 |
| `postgresql.auth.password` | `""` | 空则随机生成并跨升级保留 |
| `nats.enabled` | `true` | 内置单节点 NATS JetStream |
| `ingress.enabled` | `false` | 所有 path 都路由到 gateway |
| `autoscaling.enabled` | `false` | 为 `autoscaling.services` 中的服务创建 HPA |
| `podDisruptionBudget.enabled` | `false` | 为每个启用的服务创建 PDB |
| `services.<name>.replicaCount` | `1` | 启用 HPA 时忽略 |
| `config.otelExporterOtlpEndpoint` | `""` | 空则关闭 OTLP exporter |
| `tests.enabled` | `true` | 生成 `helm test` smoke-test Pod |

完整可调项见 [`values.yaml`](values.yaml)。

## 依赖与优先级

- **数据库**：`database.existingSecret` > `database.url` > 内置 `postgresql.enabled`。
  三者互斥，模板会用 `fail` 给出明确报错。
- **消息**：`nats.existingSecret` > `nats.url` > 内置 `nats.enabled`。
- 内置 PostgreSQL / NATS 都是**单副本**，仅适合开发与小规模部署。生产应使用
  CloudNativePG / Patroni / 托管 PG，以及 3 节点以上的 NATS JetStream 集群。
- Chart **不执行数据库迁移**：迁移必须在发布窗口显式执行（见“升级”）。

## 升级与回滚

```bash
# 1) 先执行 expand 兼容的数据库迁移（当前仓库只有 identity 首个迁移）
export PATH="$HOME/.dotnet:$PATH"
export ConnectionStrings__Default='Host=...;Database=nerves;Username=nerves;Password=...'
dotnet tool restore
dotnet ef database update \
  --project src/services/Nerves.Identity \
  --startup-project src/services/Nerves.Identity

# 2) 滚动升级
helm upgrade nerves ./deploy/helm/nerves -n nerves \
  -f values.prod.yaml --set image.tag=0.2.0 \
  --atomic --wait --timeout 10m

# 3) 查看历史 / 回滚
helm history nerves -n nerves
helm rollback nerves 1 -n nerves --wait
```

约束：

- 迁移遵循 expand / contract（D18）：先加列/表，新版本双写，下个版本再删旧列；
  因此**应用可以回滚，数据库迁移不回滚**。
- 当前仓库没有 `nerves-cli migrate`，迁移工具是 `dotnet ef`；CLI 交付后应改用它。
- `helm rollback` 只回滚 Kubernetes 资源；已执行的数据库迁移不会撤销。
- 内置 PostgreSQL 的 `POSTGRES_PASSWORD` 只在数据目录初始化时生效，改 values
  不会修改已有数据库口令；轮换需先 `ALTER ROLE` 再更新 Secret（见文档站“密钥轮换”）。

### 关于自动生成的凭据

未提供 `postgresql.auth.password` / `nats.auth.token` 时，模板用 `randAlphaNum`
生成随机值，并用 `lookup` 从已有 Secret 读回，因此 `helm upgrade` 不会轮换。
但 **`helm template` / Argo CD 等无集群查询的渲染每次都会得到新随机值**。
GitOps 场景请使用 `database.existingSecret` / `nats.existingSecret`
（或由 External Secrets 注入固定 Secret），不要依赖随机生成。

## 卸载

```bash
helm uninstall nerves -n nerves
```

默认会保留 StatefulSet 的 PVC（PostgreSQL / NATS 数据），需要彻底清理时：

```bash
kubectl -n nerves get pvc
kubectl -n nerves delete pvc data-nerves-postgresql-0 data-nerves-nats-0
```

## 排障

```bash
helm status nerves -n nerves
kubectl -n nerves get deploy,sts,pod,svc,ingress
kubectl -n nerves logs deploy/nerves-gateway
kubectl -n nerves describe pod -l app.kubernetes.io/component=identity
helm test nerves -n nerves
```

服务未就绪时先看 `/healthz`；identity 的数据库连接失败通常是因为外部连接串
不可达或 Secret 键名不匹配。

## 安全说明

- .NET 服务以 UID 1654 非 root 运行，只读根文件系统，`/tmp` 与
  `/home/app/.aspnet` 使用 emptyDir；默认 drop 全部 capabilities。
- 未内置 NetworkPolicy。生产环境请自行限制 ingress → gateway、服务间 gRPC、
  服务 → PostgreSQL/NATS 的流量。
- 未内置 ServiceMonitor / PodMonitor；`config.otelExporterOtlpEndpoint` 可接 OTel Collector。
- 签名私钥、实名 HMAC 密钥等应用密钥不属于本 Chart 的默认 values；
  通过 `services.<name>.extraEnv` + `extraEnvFrom` 从外部 Secret 注入。

## What the chart renders

| Resource | Count | Notes |
| --- | --- | --- |
| Deployments | 8 | gateway, idp, identity, session, authorization, directory, audit, compliance |
| Services | 8 | plus PostgreSQL/NATS when bundled |
| gRPC service ports | 5 | identity, session, authorization, directory, audit (8081) |
| Migration Jobs | 7 | pre-install/pre-upgrade, Database__MigrateOnly=true |
| PVCs | 2 | data protection key rings for identity and idp |

Wiring uses environment variables:

* Gateway__Services__<cluster> points the gateway at each service.
* Identity__GrpcAddress, Session__GrpcAddress, Authorization__GrpcAddress and
  Audit__GrpcAddress point the IdP at the gRPC listeners.
* Authentication__Authority, Authentication__Issuer and Authentication__Audience
  are set on every resource service from config.publicIssuer and config.audience.
* DataProtection__KeysDirectory is set for identity and idp, backed by the two
  PVCs (or dataProtection.existingClaim).


## Multiple replicas

identity and idp own the DataProtection key ring that encrypts MFA secrets and
cookies. More than one replica therefore needs a shared key ring: provide
dataProtection.existingClaim pointing at a ReadWriteMany claim, or run those
two services with a single replica. The chart refuses to render
replicaCount > 1 for them without such a claim.


## First install

OpenIddict clients only exist after the seeder runs, and production refuses the
seeder by default. For the first installation:

```bash
helm upgrade --install nerves deploy/helm/nerves \
  --set seed.enabled=true \
  --set seed.allowInProduction=true \
  --set 'seed.adminRedirectUris[0]=https://admin.example.com/oidc-callback.html' \
  --set 'seed.redirectUris[0]=https://nerves.example.com/signin-oidc' \
  --set 'seed.postLogoutRedirectUris[0]=https://nerves.example.com/signout-callback-oidc'
```

Then set `seed.enabled=false` and upgrade again. `config.adminOrigins` lists the
browser origins allowed to call the IdP and the gateway (the admin console).
