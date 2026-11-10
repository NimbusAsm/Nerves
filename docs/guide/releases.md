# 版本与发布

## 版本语义

- 遵循语义化版本：`MAJOR.MINOR.PATCH`。
- 破坏性 API 变更升 MAJOR；新增能力升 MINOR；修复升 PATCH。
- 程序集版本由 `Directory.Build.props` 的 `Version` 统一声明，当前 **1.0.0**。

## 发布清单

1. **代码冻结**：main 上 CI 全绿（dotnet / e2e / frontend / images / python / browser）。
2. **契约检查**：`buf breaking`（gRPC）与 OpenAPI 差异人工确认；必要时运行
   `scripts/collect-openapi.sh` 归档。
3. **镜像**：CI images 作业构建 9 个镜像并打 tag：
   `ghcr.io/nimbusasm/nerves/<service>:1.0.0`。
4. **数据库**：Helm 的 pre-upgrade 迁移 Job 以 `Database:MigrateOnly=true` 执行；
   升级前按 `backup-restore.md` 备份。
5. **部署**：`helm upgrade --install`（`deploy/helm/nerves`），确认 `/readyz`。
6. **冒烟**：登录 → 换取令牌 → 网关 `/api/v1/stats`；`/api/v1/audit-events/verify`
   返回 `valid: true`。
7. **回滚**：`helm rollback <release> <revision>`；数据层按备份恢复（见运维手册）。

## OpenAPI 归档

```bash
./scripts/collect-openapi.sh artifacts/openapi
```

脚本从本地运行中的服务读取 `/openapi/v1.json`，按服务名落盘，便于随版本归档或
发布到开发者门户。

## 兼容性承诺

- 迁移只做加法，不重命名或删除列。
- gRPC 破坏性变更必须通过 `buf breaking` 门禁并升 MAJOR。
- REST 管理 API 的移除/改语义需在 CHANGELOG 标注并保留一个 MINOR 的弃用期。
