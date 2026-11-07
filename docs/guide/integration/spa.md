# 单页应用（SPA）接入

::: warning 当前不可联调
Nerves 的 OIDC 端点尚未实现（🧭 v1.0 计划）。以下为 v1.0 目标形态；
现在只能把 issuer 指向其它标准 OIDC 提供方来验证前端代码。
:::

## 场景

Vue / React 等 SPA 作为**公共客户端**，使用 Authorization Code + PKCE，
没有 client secret。令牌可以放内存或 `sessionStorage`，不要放 `localStorage`
（XSS 面更大），并且必须配合严格的 CSP。

## 1. 依赖

```bash
npm install oidc-client-ts
```

## 2. 配置

```ts
// src/auth.ts
import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

export const userManager = new UserManager({
  authority: import.meta.env.VITE_NERVES_ISSUER,   // https://id.example.com
  client_id: import.meta.env.VITE_NERVES_CLIENT_ID,
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',                            // oidc-client-ts 默认启用 PKCE
  scope: 'openid profile email offline_access',
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: true,
  monitorSession: false,                            // 依赖 refresh token 时关闭 iframe 轮询
})
```

```ts
// 登录
await userManager.signinRedirect()

// /auth/callback 路由
await userManager.signinRedirectCallback()
window.location.replace('/')

// 取当前用户
const user = await userManager.getUser()
if (user && !user.expired) {
  console.log(user.profile.sub, user.profile.realm)
}
```

## 3. 调用 API

```ts
async function apiFetch(path: string, init: RequestInit = {}) {
  const user = await userManager.getUser()
  if (!user || user.expired) throw new Error('not authenticated')

  const headers = new Headers(init.headers)
  headers.set('authorization', `Bearer ${user.access_token}`)
  if (init.method && init.method !== 'GET') {
    headers.set('idempotency-key', crypto.randomUUID())
  }
  return fetch(`${import.meta.env.VITE_NERVES_API_BASE}${path}`, { ...init, headers })
}
```

## 4. CORS 与安全

Nerves 的令牌端点必须允许 SPA 的 origin（规划）；此外：

- SPA 不能保存 client secret；
- 必须启用 PKCE（`code_challenge_method=S256`）；
- redirect URI 必须精确注册，禁止通配符；
- 使用严格 CSP，避免第三方脚本读取令牌；
- 退出登录时同时清理本地用户对象并调用 `/connect/endsession`（规划）；
- 刷新令牌只在支持轮换与重用检测时使用；公共客户端长期保存 refresh token 风险更高，
  推荐短 access token + 静默重认证（若会话仍有效）。

## 5. 当前状态

| 能力 | 状态 |
| --- | --- |
| `/connect/authorize`、`/connect/token` | 🧭 v1.0 计划 |
| CORS 允许列表 | 🧭 v1.0 计划 |
| `/connect/endsession` | 🧭 v1.0 计划 |
| SPA SDK | 🧭 v1.0 计划（TS SDK）；标准 OIDC 库已足够 |
