# 权限与授权

> **状态**：🧩 契约已落地（`AuthorizationService` 与 `ListAccountPermissions`）；
> RBAC、决策 API、决策缓存 🧭 v1.0 计划；外挂 PDP 🔭 v1.1 计划。

## 模型

```text
Subject（账户 / 法律实体 / 组 / 服务）
   │ N : M
   ▼
Role（global / app / org 作用域）
   │ N : M
   ▼
Permission（code = app:resource:action）
   │
   └─ 可选：Condition（部门、时间、IP 段、资源归属等属性）
```

- **Subject**：可以是网络账户、法律实体、组或服务账户；
- **Role**：按作用域分层，避免把“组织管理员”误用到全平台；
- **Permission**：稳定权限码，推荐 `app:resource:action` 格式；
- **Condition**：属性表达式，由内置 PDP 在同分片内计算。

示例权限码：

| 权限码 | 含义 |
| --- | --- |
| `crm:invoice:read` | 读取发票 |
| `crm:invoice:write` | 创建 / 修改发票 |
| `admin:account:disable` | 停用账户 |
| `org:member:invite` | 邀请组织成员 |

## 四层判定

| 层 | 位置 | 延迟量级 | 适用 |
| --- | --- | --- | --- |
| L1 令牌内权限 | 资源服务本地（JWT claim） | ~0 | 粗粒度权限 |
| L2 决策缓存 | authorization（本地内存 + Redis） | < 1 ms | 角色最近未变 |
| L3 PDP 计算 | authorization（单分片、纯内存规则） | 2–5 ms | 属性条件、资源归属 |
| L4 外挂 PDP | Casbin.NET / OpenFGA / SpiceDB | 0.01–20 ms | 跨系统统一策略 |

关键约束：**L3 必须单分片内纯计算**，不能查其它服务或跨库，否则授权会成为全局
限流点。L4 默认关闭；开启即意味着部署方接受额外延迟与运维成本。

## 失效：版本号而不是 TTL

每个账户带一个单调递增的 `authz_ver`：

1. 角色 / 权限变更 → `authz_ver + 1`；
2. 通过事件广播（NATS）通知；
3. 各服务缓存按版本淘汰；
4. 令牌里带 `authz_ver`，验签后可判断缓存是否过期。

不用 TTL“猜”失效时间：TTL 要么太短（打爆后端），要么太长（越权窗口）。

## 默认拒绝

- 每个管理端点必须显式声明权限码；
- 未声明的端点应当拒绝启动（编译期 / 启动期检查，目标能力）；
- 令牌校验必须验证签名、`iss`、`aud`、`exp`、`nbf`；
- 错误信息不得区分“用户不存在”与“口令错误”。

## 令牌 claim（目标契约）

| Claim | 含义 |
| --- | --- |
| `sub` | 网络账户 ID |
| `leid` | 法律实体 ID（默认不下发给普通业务应用） |
| `realm` | 品牌 / 域 |
| `amr` / `acr` | 认证方式与强度 |
| `roles` / `perms` | 粗粒度授权 |
| `authz_ver` | 授权版本，用于缓存失效 |
| `token_ver` | 账户级令牌版本，用于吊销 |

## 当前可用性

| 能力 | 状态 |
| --- | --- |
| 授权服务契约（`GetServiceInfo`） | 🧩 已定义 |
| `ListAccountPermissions`（identity 契约） | 🧩 已定义，未实现 |
| 角色 / 权限表、PDP、决策缓存 | 🧭 v1.0 计划 |
| Casbin.NET 可选模块 | 🧭 v1.0 计划（方案为可选） |
| OpenFGA / SpiceDB 连接器 | 🔭 v1.1 计划 |

当前 authorization 服务只返回健康检查与 `/api/v1/meta`，没有决策端点。
