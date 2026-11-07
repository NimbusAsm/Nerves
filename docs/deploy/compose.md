# Docker Compose（开发）

> Compose **只用于本地开发与演示**。生产使用 [Helm](/deploy/helm)（D16）。

## 组成

文件：[`deploy/docker/docker-compose.yml`](https://github.com/NimbusAsm/Nerves/blob/main/deploy/docker/docker-compose.yml)。

| 服务 | 端口 | 说明 |
| --- | --- | --- |
| gateway | 5100 → 8080 | 边缘入口 |
| idp | 5101 → 8080 | OIDC（当前空壳） |
| identity | 5102 → 8080 | 身份（当前空壳） |
| session | 5103 → 8080 | 会话（当前空壳） |
| authorization | 5104 → 8080 | 授权（当前空壳） |
| directory | 5105 → 8080 | 目录（当前空壳） |
| postgres | `127.0.0.1:55440` | PostgreSQL 18.6-alpine |
| nats | 4222 / 8222 | NATS 2.15.2-alpine + JetStream |

所有服务共用同一份 Dockerfile，通过构建参数选择项目：

```bash
docker build -f deploy/docker/Dockerfile \
  --build-arg PROJECT=src/services/Nerves.Identity/Nerves.Identity.csproj \
  --build-arg ENTRY_DLL=Nerves.Identity.dll .
```

## 启动

```bash
# 全部（首次会构建六个服务，耗时较长）
docker compose -f deploy/docker/docker-compose.yml up --build

# 只起依赖
docker compose -f deploy/docker/docker-compose.yml up -d postgres nats
```

验证：

```bash
curl -fsS http://localhost:5100/healthz
curl -fsS http://localhost:5102/api/v1/meta
```

## 凭据与配置

Compose 里的默认凭据仅用于本地：

```text
POSTGRES_USER=nerves
POSTGRES_PASSWORD=nerves
POSTGRES_DB=nerves
ConnectionStrings__Default=Host=postgres;Port=5432;Database=nerves;Username=nerves;Password=nerves
Nats__Url=nats://nats:4222
```

不要在共享主机或生产环境复用这些值。

## 数据卷与重置

PostgreSQL 数据在命名卷 `nerves-dev_pgdata`；NATS 没有持久卷（容器内 JetStream 数据
随容器重建丢失）。重置数据库：

```bash
# 会删除所有本地开发数据，确认后再执行
docker compose -f deploy/docker/docker-compose.yml down -v
```

> PostgreSQL 18 镜像要求卷挂在 `/var/lib/postgresql`，不是
> `/var/lib/postgresql/data`；挂错会导致容器启动即退出。

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 端口 55440 被占用 | 本机可能已有 PG；改 compose 映射或先停掉占用者 |
| 服务启动但 identity 连不上库 | 确认 `postgres` 健康后再启动；compose 已加 `depends_on: service_healthy` |
| 构建很慢 | 六个服务分别 `dotnet publish`；只起需要的服务可减少构建 |
| 4222 / 8222 暴露到局域网 | Compose 未绑定 `127.0.0.1`，共享主机上请自行加绑定 |

## 与 Helm 的差异

| 项 | Compose | Helm |
| --- | --- | --- |
| 用途 | 开发 / 演示 | 生产 |
| 副本与滚动更新 | 无 | Deployment + HPA + PDB |
| 密钥管理 | 明文环境变量 | Secret / existingSecret |
| 数据库高可用 | 无 | 外部 PG（推荐） |
| 迁移 | 手动 | 手动（发布窗口） |
