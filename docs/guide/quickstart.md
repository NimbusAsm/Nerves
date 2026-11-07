# 快速开始

::: warning 先看清现在能做什么
当前是 **M0 骨架**：能启动服务、调用健康检查、查看 OpenAPI、执行 identity 的数据库迁移。
**还不能登录、拿令牌或调用管理 API**。完整能力状态见 [实现状态](/guide/status)。
:::

## 前置条件

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| .NET SDK | **10.0.403**（见 `global.json`） | 构建与运行服务 |
| Docker + Compose v2 | 任意近期版本 | PostgreSQL / NATS 与整套 Compose |
| Node.js / pnpm | Node ≥ 20.19，pnpm 11 | 只用于构建文档站（`docs/`） |

本机若把 SDK 装在用户目录，先设置：

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
dotnet --version   # 期望 10.0.403
```

## 方式 A：整套 Docker Compose

仓库根目录：

```bash
docker compose -f deploy/docker/docker-compose.yml up --build
```

首次构建会为六个服务分别 `dotnet publish`，耗时较长。启动后：

| 服务 | 地址 |
| --- | --- |
| gateway | http://localhost:5100 |
| idp | http://localhost:5101 |
| identity | http://localhost:5102 |
| session | http://localhost:5103 |
| authorization | http://localhost:5104 |
| directory | http://localhost:5105 |
| PostgreSQL | `127.0.0.1:55440`（用户 / 库 / 口令均为 `nerves`） |
| NATS | `4222`，监控 `8222` |

验证：

```bash
for port in 5100 5101 5102 5103 5104 5105; do
  echo "$port: $(curl -fsS http://localhost:$port/healthz)"
done
```

预期每个端口返回 `{"status":"ok"}`。

> Compose 的 PostgreSQL 18 镜像把卷挂在 `/var/lib/postgresql`（不是 `/var/lib/postgresql/data`）。
> 这是镜像 18+ 的要求，改回旧路径会导致容器直接退出。

## 方式 B：只起依赖，本地跑服务

```bash
# 1) 只启动 PostgreSQL 与 NATS
docker compose -f deploy/docker/docker-compose.yml up -d postgres nats

# 2) 启动网关（默认 profile 端口 5006）
dotnet run --project src/services/Nerves.Gateway

# 3) 另开终端验证
curl http://localhost:5006/healthz
curl http://localhost:5006/readyz
curl http://localhost:5006/api/v1/meta
```

各服务 `launchSettings.json` 的默认 HTTP 端口：

| 服务 | 端口 |
| --- | --- |
| gateway | 5006 |
| idp | 5265 |
| identity | 5296 |
| session | 5254 |
| authorization | 5067 |
| directory | 5259 |

identity 需要数据库连接串（本地默认端口 5432 与 Compose 的 55440 不同）：

```bash
export ConnectionStrings__Default='Host=localhost;Port=55440;Database=nerves;Username=nerves;Password=nerves'
dotnet run --project src/services/Nerves.Identity
```

## 执行 identity 数据库迁移

服务**启动时不会自动迁移**（D18：迁移与发布解耦）。在需要数据库能力前显式执行：

```bash
export PATH="$HOME/.dotnet:$PATH"
export ConnectionStrings__Default='Host=localhost;Port=55440;Database=nerves;Username=nerves;Password=nerves'

dotnet tool restore
dotnet ef database update \
  --project src/services/Nerves.Identity \
  --startup-project src/services/Nerves.Identity
```

迁移会创建 `identity` schema 下的 `legal_entity`、`network_account` 等表。
该迁移在仓库记录中已于 PostgreSQL 18.6 上验证通过。

## 文档站

```bash
cd docs
pnpm install
pnpm dev      # 本地预览
pnpm build    # 构建到 docs/.vitepress/dist
```

## 常见问题

| 现象 | 处理 |
| --- | --- |
| `dotnet` 找不到 | `export PATH="$HOME/.dotnet:$PATH"`，apphost 直启还需要 `DOTNET_ROOT` |
| 5432 端口被占用 | 本机开发依赖固定在 `127.0.0.1:55440`；用 Compose 的 PG 或改连接串 |
| PostgreSQL 容器反复退出 | 确认卷挂载点是 `/var/lib/postgresql` |
| `dotnet test` 找不到测试 | `global.json` 已配置 Microsoft.Testing.Platform；测试项目不要安装 `Microsoft.NET.Test.Sdk` |
| `dotnet ef` 不存在 | 先 `dotnet tool restore`（本地工具清单 `dotnet-tools.json`） |

## 下一步

- 读 [核心概念](/guide/concepts/legal-entity)，理解法人 / 账户双层模型。
- 看 [API 概览](/api/overview)，确认当前真正可用的端点与规划中的端点。
- 计划上 Kubernetes：读 [Helm 部署](/deploy/helm)。
