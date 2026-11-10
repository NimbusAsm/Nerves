# 接入概览

Nerves 的 IdP 已实现标准的 **Authorization Code + PKCE** 流程，且**不渲染任何登录页面**：
登录 UI 由接入方自己实现，通过登录 API 取到一次性票据后再跳 `/connect/authorize`。
这一页讲清楚三件事：选哪种接入方式、端点在哪、示例代码在哪。

## 可运行示例（推荐从这里开始）

仓库里的 `samples/` 有三个能直接跑起来的工程，每个都自带 README 与 `.env.example`：

| 示例 | 形态 | 客户端类型 | 令牌放在哪 | 用到的 SDK |
| --- | --- | --- | --- | --- |
| `samples/dotnet-console` | 无头客户端 / CI 作业 | public + PKCE | 进程内存 | `Nerves.Client` |
| `samples/node-bff` | BFF（浏览器只拿不透明 Cookie） | public + PKCE，回调在服务端 | Node 进程；浏览器只拿 HttpOnly Cookie | `fetch` |
| `samples/vue-spa` | 单页应用 | public + PKCE | 浏览器内存 | `@nerves/sdk` |

## 登录链路

```text
POST {idp}/api/v1/auth/login      {username, password}      → { loginTicket }
GET  {idp}/connect/authorize      client_id, redirect_uri, scope, state,
                                  code_challenge(S256), login_ticket
                                                            → 302 {redirect_uri}?code=&state=
POST {idp}/connect/token          authorization_code, code, redirect_uri,
                                  client_id, code_verifier   → access / id / refresh
GET  {idp}/connect/userinfo       Bearer access_token        → sub, name, email, leid
GET  {gateway}/api/v1/*           Bearer access_token        → 管理 API
```

两个与多数 IdP 不同的点：

1. **IdP 不渲染页面。** `/connect/authorize` 需要一个来自登录 API 的 `login_ticket`
   （90 秒有效、一次性），因此控制台程序也能完成这个「交互式」流程。
2. **PKCE 是强制的**（`RequireProofKeyForCodeExchange()`），机密客户端也一样；
   不传 `code_verifier` 会在 `/connect/token` 失败。

## 选择接入方式

| 你的应用 | 推荐流程 | client secret | 状态 |
| --- | --- | --- | --- |
| 服务端 Web / BFF（.NET、Node、Java…） | Authorization Code（+ PKCE） | 可选 | ✅ 可用 |
| 单页应用（SPA） | Authorization Code + PKCE | 否 | ✅ 可用（示例） |
| 移动端（iOS / Android） | Authorization Code + PKCE（RFC 8252） | 否 | ✅ 可用（见[移动端](/guide/integration/mobile)） |
| 服务 / 定时任务 | Client Credentials | 是 | 🧭 v1.0 计划 |
| 已有企业 IdP | 上游 OIDC / LDAP 联邦 | Nerves 作为 SP | ✅ 可用 |

## 端点

以 issuer `https://id.example.com` 为例（**占位符**；本地开发是 `http://127.0.0.1:5101`）：

| 端点 | 路径 |
| --- | --- |
| Discovery | `/.well-known/openid-configuration` |
| JWKS | `/.well-known/jwks` |
| 授权 | `/connect/authorize` |
| 令牌 | `/connect/token` |
| 用户信息 | `/connect/userinfo` |
| 注销 | `/connect/logout`（同时撤销该账户全部会话） |
| 登录（JSON） | `/api/v1/auth/login` |
| 二次验证 | `/api/v1/auth/mfa`（TOTP / 恢复码 / Passkey） |
| 管理 API | `/api/v1/*`（经网关） |

能力与实现状态以 [实现状态](/guide/status) 为准。

## 注册回调地址

OIDC 要求 `redirect_uri` **逐字匹配**。开发环境由 `OpenIddictSeed`
（`src/services/Nerves.Idp/appsettings.Development.json`）在启动时登记客户端：

```json
"OpenIddictSeed": {
  "Enabled": true,
  "AdminClientId": "nerves-admin",
  "AdminRedirectUris": [
    "http://127.0.0.1:5300/oidc-callback.html",
    "http://127.0.0.1:4311/auth/callback",
    "http://127.0.0.1:5178/auth/callback"
  ]
}
```

改完重启 IdP 即可（seeder 会 upsert）。生产环境不允许 seed
（`ProductionGuards` 会拒绝），要走你自己的客户端注册流程。

## 令牌校验清单

资源服务**本地验签**，不要每个请求都回源：

1. 从 Discovery 取 `jwks_uri`，缓存 JWKS 并按 `kid` 选密钥；
2. 校验签名算法在允许列表内（当前签发 `RS256`）；
3. 校验 `iss`、`aud`、`exp`、`nbf`；允许不超过 60 秒时钟偏移；
4. 不要仅解码 payload 就认为通过；
5. 不要把 introspection 放进热路径；
6. 吊销依赖短 TTL + `token_ver` / `authz_ver`，不是逐请求查询。

## Claims 速查

| Claim | 说明 |
| --- | --- |
| `sub` | 网络账户 ID（默认身份） |
| `leid` | 法律实体 ID（默认不下发，需要跨账户能力时申请） |
| `realm` | 品牌 / 产品域 |
| `name` / `email` | 展示用资料 |
| `permissions` / `roles` | 授权快照与粗粒度角色 |
| `authz_ver` | 授权版本（角色变更即递增） |
| `token_ver` | 账户级令牌版本 |

## 常见错误

| 现象 | 原因 |
| --- | --- |
| `/connect/authorize` 返回 401 `login_required` | 没带 `login_ticket`，或票据已用过/过期 |
| 400 `invalid_redirect_uri` | 回调地址没有登记到该客户端 |
| 400 `invalid_grant` | PKCE verifier 不匹配，或授权码已被使用 |
| 403 调用管理 API | 账户缺少 `nerves:admin` 权限 |
| 429 | 登录/令牌端点有固定窗口限流（login 10/min、token 30/min、authorize 120/min） |
