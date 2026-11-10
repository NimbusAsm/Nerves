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
- [ ] Dependabot 每周提交 NuGet / npm / pip / Actions 更新 PR，合并前看 CI。
- [ ] 升级大版本时先读迁移说明，再按 upgrade.md 演练。
