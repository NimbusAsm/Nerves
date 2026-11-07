# 常见问题（FAQ）

## 项目与定位

### Nerves 是什么？

一个**开源自托管的账户中心**：一个法律实体对应一个实名身份，可以拥有多个网络账户；
同时提供认证、授权、身份验证与 IdP 四类能力。技术栈是 .NET 10 微服务 + PostgreSQL，
生产用 Kubernetes / Helm 部署，Apache-2.0 许可。

### 和 Elixir 的 Nerves 有关系吗？

没有。Elixir 生态的 [Nerves](https://nerves-project.org) 是嵌入式框架；本项目只是同名。
包名统一使用 `Nerves.*`、`@nerves/*`、`nerves-sdk` 前缀以便区分。

### 和 Keycloak / Casdoor / Zitadel 有什么不同？

差异点是**法人 / 网络账户双层模型**（一个实名实体对应多个账户，跨产品复用身份），
以及把授权、身份验证与 IdP 放进同一套微服务。诚实的短板：通用协议覆盖打不过
Keycloak，轻量部署打不过 Casdoor。Nerves 不在这两个方向竞争。

## 现在能用吗？

**不能用于真实登录。** 当前是 M0 骨架：服务能启动、有健康检查与 OpenAPI，
identity 有领域模型和首个迁移；但登录、OIDC、会话、管理 API、RBAC、目录与 SDK
都还没有实现。逐项状态见 [实现状态](/guide/status)。

## 为什么强制 PostgreSQL？

为了分片、分区、行级安全与并发写能力，也为了避免维护两套 SQL 方言。代价是放弃
“单文件数据库十分钟跑起来”的轻量位（D02）。MySQL 在 EF Core 10 上没有可用驱动，
客观上也做不了。

## 为什么是微服务？小团队会不会太重？

拆分依据是数据所有权与独立扩容：会话写热点、凭据读热点、授权读 QPS 差异很大。
代价是本地开发与部署复杂度上升，用 Compose 一键起全套 + Helm 默认值来对冲。
小团队也能用，只是要多跑几个容器。

## 能单机部署吗？

开发可以（Compose 起 PG + NATS + 六个服务）。生产建议至少 Kubernetes + 外部
PostgreSQL；单机 Compose 不具备滚动发布、健康摘除与密钥管理能力（D16）。

## 支持 SQLite / MySQL 吗？

不支持，也不打算支持。强制 PostgreSQL 是明确的架构决策，不是临时限制。

## 支持多租户 / 多品牌吗？

设计上有 **realm**：用户名在 realm 内唯一，令牌带 realm，资源服务据此隔离。
组织（directory）是另一种维度，表示单位 / 空间。两者都在 v1.0 计划中，当前未实现。

## v1.1 才有的能力有哪些？

以下能力**当前不可用**，不要按已实现集成：

| 能力 | 状态 |
| --- | --- |
| Java / Go / PHP SDK | 🔭 v1.1 计划 |
| 多分片启用与再平衡 | 🔭 v1.1 计划（v1 只有“分片就绪”） |
| 外挂 PDP（OpenFGA / SpiceDB） | 🔭 v1.1 计划 |
| `Compliance.CN` 通道适配、日志归档、国密 | 🔭 v1.1 计划 |
| 上游 SAML（SP）、企业微信 / 钉钉联邦 | 🔭 v1.1 计划 |

## 接入与 API

### 现在怎么接入？

还没有可接入的认证端点。可以先用任意标准 OIDC 客户端库把代码写好，等 IdP 交付后
把 issuer / client 配置指过来；接入方式见 [接入概览](/guide/integration/)。
管理 API 与 SDK 都还没有交付。

### 有 SDK 吗？

仓库 `sdk/` 下已有 .NET / TypeScript / Python 三门客户端的源码与测试，但**还没有发布包**，
而且它们依赖的管理 API 尚未实现，所以目前不能对真实服务端使用。Java / Go / PHP 是
🔭 v1.1 计划。认证接入不依赖专用 SDK，使用标准 OIDC 库即可。

### API 版本策略是什么？

URL 主版本 `/api/v1` 保持 12 个月兼容；破坏性变更走 `/api/v2`，两个版本并存一个周期。
内部 gRPC 契约用 `buf breaking` 阻止破坏性修改。

### 为什么管理 API 用游标分页？

offset 深翻页在大表上会退化，且无法由单分片完成；游标分页符合 O(1) 请求约束。
游标是不透明字符串，客户端不要解析。

## 部署

### Helm Chart 默认值能装到 Kind / Minikube 吗？

Chart 的目标是“默认值能装”：单副本、内置单节点 PG / NATS、无 Ingress。
本次交付已通过 `helm lint --strict` 与 `helm template` + `kubeconform` 校验，
但**没有在真实集群执行安装**（本机无可用集群），且默认镜像尚未发布。
按 [Helm 部署](/deploy/helm) 构建并加载镜像后可安装；未验证项已在 Chart README 列出。

### 为什么安装后 Pod 拉不到镜像？

默认镜像 `ghcr.io/nimbusasm/nerves/<service>:0.1.0` 尚未发布。需要自行构建并用
`kind load docker-image` / `minikube image load` 导入，或推到自己的 registry 后
覆盖 `image.repository`。

### 需要 Redis 吗？

v1.0 的会话服务计划使用分片内 Redis 作为热数据层，但 Helm Chart 目前**不包含 Redis**，
session 服务也还是空壳。没有 Redis 时不要指望会话能力可用。

### 迁移会自动执行吗？

不会。Chart 与 .NET 服务都不会在启动时自动迁移；必须在发布窗口显式执行
`dotnet ef database update`（当前）或未来的 `nerves-cli migrate`。
这是为了保证 expand / contract 与可回滚。

## 安全与隐私

### 实名证件号会明文入库吗？

不会。领域模型只保存证件号的 **HMAC 摘要**（64 位十六进制）+ 证件类型 + 国家。
HMAC 密钥由部署方通过 KMS / Vault 管理，不在代码或镜像里。

### 为什么不能直接轮换实名 HMAC 密钥？

当前模型没有 key version 列，换密钥会让旧摘要无法匹配，可能产生重复实体。
需要先做 schema 演进（keyring + rehash-on-verify），详见
[密钥轮换](/operations/key-rotation)。在此之前按长期密钥管理。

### 审计能追溯吗？

审计服务与哈希链是 🧭 v1.0 计划；当前没有审计表或审计 API。不要把“文档里写了
审计”当成已经可用。

## 许可与贡献

### 许可证是什么？

Apache-2.0。贡献需要 DCO 签署（`git commit -s`），不要求 CLA。

### 可以商用吗？

可以。Apache-2.0 允许商用与二次分发；项目方可能提供企业模块与支持合同，但不做
按人头订阅。

## 文档本身

### 文档中的 ✅ / 🧩 / 🧭 / 🔭 是什么意思？

- ✅ 已实现：当前有可运行代码或交付物已通过构建 / 校验；
- 🧩 契约 / 模型已落地：proto 或 EF 模型存在，但没有对外实现；
- 🧭 v1.0 计划：方案承诺 v1.0，但当前未实现；
- 🔭 v1.1 计划：明确排到 v1.1，当前不可用。

完整定义与逐项状态见 [实现状态](/guide/status)。

### 发现文档与代码不一致怎么办？

以代码为准。欢迎提 issue 或 PR 修正文档；文档与代码同 PR 更新（B10）。
