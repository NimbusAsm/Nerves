# 接入概览

::: warning 端点尚未实现
Nerves 目前没有可用的 OIDC Discovery、令牌端点或管理 API（详见 [实现状态](/guide/status)）。
本组页面按 **v1.0 目标契约**编写，用于提前评估接入方式；示例不能端到端联调。
下面的 issuer、client id、audience 都是占位符。
:::

## 选择接入方式

| 你的应用 | 推荐流程 | 是否需要 client secret | 当前状态 |
| --- | --- | --- | --- |
| 服务端 Web / BFF（.NET、Node、Java…） | Authorization Code | 是 | 🧭 v1.0 计划 |
| 单页应用（SPA） | Authorization Code + PKCE | 否 | 🧭 v1.0 计划 |
| 移动端（iOS / Android） | Authorization Code + PKCE（RFC 8252） | 否 | 🧭 v1.0 计划 |
| 服务 / 定时任务 | Client Credentials | 是 | 🧭 v1.0 计划 |
| 已有企业 IdP | 上游 OIDC / LDAP 联邦 | 由 Nerves 作为 SP / 客户端 | 🧭 v1.0 计划（SAML 为 🔭 v1.1） |

## 目标端点

以 issuer `https://id.example.com` 为例（**占位符，不要照抄**）：

| 端点 | 路径 |
| --- | --- |
| Discovery | `/.well-known/openid-configuration` |
| JWKS | `/.well-known/jwks.json` |
| 授权 | `/connect/authorize` |
| 令牌 | `/connect/token` |
| 用户信息 | `/connect/userinfo` |
| 注销 | `/connect/endsession` / `/connect/logout` |
| 撤销 | `/connect/revoke` |
| 账户切换（扩展） | `/connect/switch-account` |
| 管理 API | `/api/v1/*` |

## 令牌校验清单

资源服务**本地验签**，不要每个请求都回源：

1. 从 Discovery 获取 `jwks_uri`，缓存 JWKS 并按 `kid` 选择密钥；
2. 校验签名算法在允许列表内（目标：`ES256` 为主，`RS256` 兼容）；
3. 校验 `iss`、`aud`、`exp`、`nbf`；允许不超过 60 秒时钟偏移；
4. 不要仅解码 payload 就认为通过；
5. 不要把 introspection 放进热路径（在 10^11 规模下不可行）；
6. 吊销依赖短 TTL + `token_ver` / `authz_ver`，不是逐请求查询。

## Claims 速查

| Claim | 说明 |
| --- | --- |
| `sub` | 网络账户 ID（默认身份） |
| `leid` | 法律实体 ID（默认不下发，需要跨账户能力时申请） |
| `realm` | 品牌 / 产品域 |
| `amr` / `acr` | 认证方式 / 强度 |
| `roles` / `perms` | 粗粒度授权 |
| `authz_ver` | 授权版本 |
| `token_ver` | 账户级令牌版本 |

## 官方 SDK

| 语言 | 状态 |
| --- | --- |
| .NET（`Nerves.Client`） | 🚧 `sdk/dotnet` 源码与测试已有；未发布 NuGet，管理 API 未实现 |
| TypeScript（`@nerves/sdk`） | 🚧 `sdk/typescript` 源码已有；未发布 npm，管理 API 未实现 |
| Python（`nerves-sdk`） | 🚧 `sdk/python` 源码与测试已有；未发布 PyPI，管理 API 未实现 |
| Java / Go / PHP | 🔭 v1.1 计划 |

SDK 只依赖管理 API 的 wire contract；在 `/api/v1` 交付前，它们可以构建和做 mock 测试，
但不能对真实的 Nerves 服务端工作。

在 SDK 交付前，请使用各语言成熟的 OIDC 客户端库；Nerves 遵循标准协议，不要求专用 SDK。

## 多 realm 与多品牌

- 用户名在 realm 内唯一；
- 授权请求需要带上目标 realm（由 client 注册信息决定）；
- 令牌中的 `realm` 用于资源服务做租户隔离，不能只信任请求头。

## 常见错误

| 错误 | 后果 |
| --- | --- |
| SPA 里保存 client secret | secret 泄露；SPA 必须使用 PKCE 公共客户端 |
| 每请求调用 introspection | 热路径多一次 RTT，规模上不可接受 |
| 解 payload 不验签 | 任意伪造令牌 |
| 用 `localhost` 作为移动端 redirect | 真机无法回调；应使用自定义 scheme 或 loopback + PKCE |
| 只看 `role` 名称不看作用域 | 跨组织越权 |
