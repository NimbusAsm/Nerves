# 运维 CLI（nerves）

`tools/Nerves.Cli` 是随仓库发布的 .NET 工具，包名 `Nerves.Cli`，命令名 `nerves`。
它把"迁移、种子、体检、分片重算"四件事从手工命令收敛成可重复的入口。

## 安装与运行

开发时可直接从源码运行：

```bash
dotnet run --project tools/Nerves.Cli -- doctor
```

作为 .NET 工具安装（`PackAsTool`）：

```bash
dotnet pack tools/Nerves.Cli/Nerves.Cli.csproj -c Release -o artifacts/packages
dotnet tool install --global Nerves.Cli --add-source artifacts/packages
nerves --help
```

> 工具引用了各服务的工程，因此需要目标机器安装 **ASP.NET Core 运行时**（.NET 10）。

## 命令

| 命令 | 作用 | 关键选项 |
| --- | --- | --- |
| `nerves db migrate` | 按服务逐个应用 EF Core 迁移（与 K8s 迁移 Job 同一条代码路径） | `--service identity,session`、`--dry-run` |
| `nerves seed` | 运行 identity + authorization 的开发种子（管理员账户与管理角色） | `--realm`、`--username`、`--password`、`--environment`、`--allow-production`、`--shard-count` |
| `nerves doctor` | 只读体检：数据库可达、每个 schema 的待执行迁移、各服务 `/healthz` 与版本 | `--connection-string` |
| `nerves shard reconcile` | 重新计算 `identity.routing_index.shard` | `--shard-count`（必填）、`--dry-run` |
| `nerves version` | 打印 CLI 版本 | — |

全局选项：`--connection-string <cs>`（覆盖 `ConnectionStrings__Default`）、`--json`（单文档 JSON 输出）、`--help`。

## 环境变量

与各服务共用同一套名字，避免为 CLI 再维护一份配置：

| 变量 | 用途 |
| --- | --- |
| `ConnectionStrings__Default` | 目标数据库；默认指向开发 compose 的 `127.0.0.1:55440` |
| `Security__IdentifierHashKey` | `seed` 在非 Development 环境必需（Base64，≥32 字节） |
| `Console__Services__<name>` | `doctor` 探测的服务地址，默认 5102–5108 |
| `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT` | `seed` 使用的环境名（默认 Development） |

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 检查或迁移失败（其余步骤仍会执行完并汇总） |
| 2 | 用法错误（未知命令、缺参数） |

## 示例

```bash
# 体检：数据库 + 迁移 + 服务
nerves doctor

# 只看迁移会做什么
nerves db migrate --dry-run

# 只迁移两个 schema
nerves db migrate --service identity,authorization

# 本地开发种子（等价于服务在 Development 下的 DevSeed）
nerves seed --username admin

# 分片数调整后重算路由索引（先 dry-run）
nerves shard reconcile --shard-count 8 --dry-run
nerves shard reconcile --shard-count 8
```

## 安全约束

- `seed` 默认跑在 **Development**；非 Development 环境会沿用服务的 `ProductionGuards`，
  必须显式 `--allow-production` 才继续。
- 非 Development 环境还要求提供真实的 `Security__IdentifierHashKey`：
  用开发密钥播种会被拒绝。
- `seed` 在发现待执行迁移时**直接失败**，提示先跑 `nerves db migrate`，避免在旧表结构上写数据。
- `doctor` 与 `db migrate --dry-run` 只读；`shard reconcile --dry-run` 不落库。
