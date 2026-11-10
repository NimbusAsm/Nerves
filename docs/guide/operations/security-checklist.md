# 上线安全检查

## 传输与边界

- [ ] 入口由反向代理/负载均衡终止 TLS，转发 `X-Forwarded-Proto/For`。
- [ ] IdP 的 `OpenIddict:Issuer` 与外部访问 URL 完全一致（含结尾斜杠）。
- [ ] 资源服务 `Authentication:Issuer` 固定为同一值，`Authority` 指向集群内可达地址。
- [ ] 管理 API 不暴露在公网；网关仅开放必要前缀。

## 密钥与凭据

- [ ] `OpenIddict:SigningCertificatePath` 指向真实证书，且私钥未入库。
- [ ] `DataProtection:KeysDirectory` 已配置并持久化（PVC）。
- [ ] 数据库口令来自 Secret，不使用默认值。
- [ ] `DevSeed:Enabled` 与 `OpenIddictSeed:Enabled` 在 Production 为 false（服务会拒绝启动）。

## 运行时

- [ ] `/readyz` 与 `/healthz` 探针已配置。
- [ ] `/metrics` 可被抓取，告警覆盖 5xx 比例与令牌端点 429。
- [ ] 登录限流 10/min、token 30/min、authorize 120/min 符合业务预期。
- [ ] 审计归档目录已挂载并定期备份。

## 合规

- [ ] 同意书在收集前登记；`Compliance.CN` 留存策略与实际数据一致。
- [ ] 数据主体导出/删除流程可由 `compliance/subjects/{id}/export|erase` 完成。
- [ ] 审计保留期已确认，`GET /api/v1/audit-events/export` 可用于交付。

## 演练

- [ ] 恢复演练通过（数据库 + 密钥）。
- [ ] 轮换签名证书后旧令牌行为符合预期。


## 供应链

- [ ] CI 的 Check for vulnerable packages 步骤保持开启（NuGet 传递依赖也会扫描）。
- [ ] 镜像扫描（Trivy，HIGH/CRITICAL 阻断）保持开启。CI 用
  `--ignore-unfixed` + `deploy/security/trivyignore`（已复核的接受风险清单）：
  **只有新增的可修复漏洞会阻断**。修改该清单必须写理由，且**至少每季度复核一次**。
- [ ] 基础镜像不要长期不动：当前 `apps/admin/Dockerfile` 用 `nginx:1.27-alpine`
  （Alpine 3.21 已过厂商支持期），升级基础镜像后应同步删除清单中对应条目——
  清单只应缩小，不应增长。
- [ ] 每次 CI 产出 SBOM（Syft，CycloneDX）并作为 artifact 保留 30 天，
  发布前与镜像 digest 一起归档。
- [ ] Dependabot 每周提交 NuGet / npm / pip / Actions 更新 PR，合并前看 CI。
- [ ] 升级大版本时先读迁移说明，再按 upgrade.md 演练。

## 契约与性能门禁

- [ ] OpenAPI 兼容门（oasdiff）在 PR 上运行：与 main 归档的 `artifacts/openapi`
  对比，**破坏性变更**阻断；main 尚无归档时只收集不比对（首跑不误伤）。
- [ ] 负载冒烟（k6，默认 30s）只报告 P95 与错误率，**不设门槛**；
  它用来发现明显回归，不替代容量规划。
- [ ] 本地复现：

```bash
./scripts/collect-openapi.sh artifacts/openapi            # 需要服务在 5100-5108
./scripts/openapi-diff.sh artifacts/openapi-main artifacts/openapi
./scripts/load-smoke.sh http://127.0.0.1:5100/healthz 30s
./scripts/scan-image.sh ghcr.io/nimbusasm/nerves/gateway:0.1.0
./scripts/sbom.sh ghcr.io/nimbusasm/nerves/gateway:0.1.0 artifacts/sbom
```

  未安装对应工具时脚本会打印安装链接并跳过（退出码 0），不会误报失败。


## 会话

- [ ] `Session:IdleMinutes` / `Session:AbsoluteHours` 与合规要求一致（IdP Cookie 与 session 服务共用）。
- [ ] `Session:ExpirySweepSeconds` 已配置，过期会话会被标记为 expired。
