# gRPC 契约

> **状态**：契约 🧩 已定义并参与构建；**服务端 gRPC 尚未实现**，没有可调用的
> gRPC 端点。当前服务只提供 HTTP 健康检查与 OpenAPI。

内部服务之间使用 gRPC，契约以 `proto/` 为唯一事实源。生成代码由
`Grpc.Tools` 在构建时产出，C# 命名空间为 `Nerves.Contracts.*.V1`。

## 服务清单

| 服务 | 包 | 状态 |
| --- | --- | --- |
| `IdentityService` | `nerves.identity.v1` | 契约已定义 |
| `SessionService` | `nerves.session.v1` | 契约已定义（仅 `GetServiceInfo`） |
| `AuthorizationService` | `nerves.authorization.v1` | 契约已定义（仅 `GetServiceInfo`） |

## IdentityService

```protobuf
service IdentityService {
  rpc GetServiceInfo(GetServiceInfoRequest) returns (GetServiceInfoResponse);
  rpc VerifyCredential(VerifyCredentialRequest) returns (VerifyCredentialResponse);
  rpc GetAccount(GetAccountRequest) returns (GetAccountResponse);
  rpc ListAccountPermissions(ListAccountPermissionsRequest) returns (ListAccountPermissionsResponse);
}
```

### VerifyCredential

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `identifier` | string | realm 作用域内的用户名、邮箱或手机号 |
| `password` | string | 口令（仅内部链路，不落日志） |
| `realm` | string | 品牌 / 产品域 |

响应：

| 字段 | 说明 |
| --- | --- |
| `verified` | 是否通过 |
| `account_id` | 通过时填充 |
| `legal_entity_id` | 通过时填充 |
| `display_name` | 展示名 |
| `failure_code` | 失败原因码（稳定，不含敏感信息） |

### GetAccount

| 字段 | 说明 |
| --- | --- |
| `account_id` | 账户 ID |
| `legal_entity_id` | 法律实体 ID |
| `realm` / `username` / `display_name` | 账户信息 |
| `status` | 账户状态 |
| `email_verified` | 邮箱是否已验证 |

### ListAccountPermissions

| 字段 | 说明 |
| --- | --- |
| `permissions` | 权限码列表 |
| `authorization_version` | `authz_ver`，用于决策缓存失效 |

### GetServiceInfo

所有服务都有的元数据端点，返回 `name`、`version`、`contract_version`。

## SessionService / AuthorizationService

当前各只有 `GetServiceInfo`：

```protobuf
service SessionService {
  rpc GetServiceInfo(GetServiceInfoRequest) returns (GetServiceInfoResponse);
}

service AuthorizationService {
  rpc GetServiceInfo(GetServiceInfoRequest) returns (GetServiceInfoResponse);
}
```

会话创建、刷新令牌轮换、吊销、授权决策等 RPC 属于 v1.0 计划，契约定稿后才会加入。
按照 `buf breaking` 规则，已有字段只增不改、不复用编号。

## 契约治理

| 门禁 | 命令 | 作用 |
| --- | --- | --- |
| Lint | `buf lint` | 包名、命名、目录结构 |
| 兼容性 | `buf breaking --against '.git#branch=main'` | PR 禁止破坏性修改 |
| 构建 | `dotnet build` | 生成 `Nerves.Contracts.Grpc` |

规则：

- 包名 `nerves.<domain>.v<N>`；破坏性变更新建 `v2` 包；
- 不复用已删除的字段编号，使用 `reserved`；
- 内部调用带 deadline 与重试预算，登录链路同步调用 ≤ 3 次 RPC、总 deadline ≤ 800 ms；
- 服务之间只通过 `Nerves.Contracts.*` 通信，不引用彼此实现。

## 生成客户端

.NET 的 gRPC 类型由 `Grpc.Tools` 在 `dotnet build` 时生成到
`Nerves.Contracts.Grpc`，无需额外步骤。

其它语言的客户端暂未提供生成配置：仓库当前没有 `buf.gen.yaml`。需要时应先新增
生成模板（例如 `buf generate`），并把生成物入库，再在 CI 校验与契约一致；
这属于后续工作，不应假设现在可以直接生成。
