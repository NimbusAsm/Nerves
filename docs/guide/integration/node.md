# Node.js / BFF 接入

::: tip 可运行示例
`samples/node-bff` 是一个可运行的 BFF：浏览器只拿 HttpOnly 会话 Cookie，令牌留在 Node 进程，回调在服务端完成。
本页讲的是同一个思路的通用写法（`express-openid-connect`）。

Nerves 的 OIDC 端点均已实现；若你的部署版本较旧，先核对 `/.well-known/openid-configuration`。
:::

## 场景

Node.js 后端作为 **BFF / 机密客户端**：浏览器只持有 HttpOnly 会话 Cookie，
access / refresh token 留在服务端；后端负责调用 Nerves 管理 API 或下游资源服务。

## 1. 登录（express-openid-connect）

```bash
npm install express express-openid-connect
```

```js
import express from 'express'
import { auth, requiresAuth } from 'express-openid-connect'

const app = express()

app.use(auth({
  issuerBaseURL: process.env.NERVES_ISSUER,        // https://id.example.com
  baseURL: process.env.APP_BASE_URL,               // https://crm.example.com
  clientID: process.env.NERVES_CLIENT_ID,
  clientSecret: process.env.NERVES_CLIENT_SECRET,
  secret: process.env.SESSION_SECRET,              // 至少 32 字节，从 Secret 注入
  authRequired: false,
  auth0Logout: false,                              // 通用 OIDC：不要用 Auth0 专用登出
  routes: {
    callback: '/callback',
    login: '/login',
    logout: '/logout',
  },
  authorizationParams: {
    response_type: 'code',
    scope: 'openid profile email offline_access',
  },
}))

app.get('/', (req, res) => {
  res.json({ authenticated: req.oidc.isAuthenticated(), user: req.oidc.user ?? null })
})

app.get('/account', requiresAuth(), async (req, res) => {
  // req.oidc.accessToken 含 access token；仅在后端使用。
  res.json({ sub: req.oidc.user.sub, realm: req.oidc.user.realm })
})

app.listen(3000)
```

要点：

- `auth0Logout: false`：默认的 Auth0 登出路径不适用于通用 OIDC；
- 回调地址必须在 Nerves 的 client 注册中精确匹配；
- Cookie 建议 `app.set('trust proxy', 1)` 并把会话 Cookie 设为 Secure；
- 会话密钥（`SESSION_SECRET`）不是 OIDC client secret，不能复用。

## 2. 校验资源服务的 JWT（jose）

当 Node 服务作为 API 而非 BFF 时：

```bash
npm install jose
```

```js
import { createRemoteJWKSet, jwtVerify } from 'jose'

const issuer = process.env.NERVES_ISSUER
const audience = process.env.NERVES_AUDIENCE

// 进程级缓存；jose 会按 kid 缓存 JWKS。
const jwks = createRemoteJWKSet(new URL('/.well-known/jwks.json', issuer))

export async function requireToken(req, res, next) {
  const header = req.headers.authorization ?? ''
  const token = header.startsWith('Bearer ') ? header.slice(7) : null
  if (!token) return res.status(401).json({ type: 'about:blank', status: 401 })

  try {
    const { payload } = await jwtVerify(token, jwks, {
      issuer,
      audience,
      algorithms: ['ES256', 'RS256'],
      clockTolerance: 60,
    })
    req.auth = payload
    next()
  } catch {
    res.status(401).json({ type: 'about:blank', status: 401 })
  }
}

export function requirePermission(code) {
  return (req, res, next) => {
    const perms = Array.isArray(req.auth?.perms) ? req.auth.perms : []
    if (!perms.includes(code)) {
      return res.status(403).json({ type: 'about:blank', status: 403 })
    }
    next()
  }
}
```

- `createRemoteJWKSet` 自带缓存与 `kid` 选择，不要每次请求重新拉 JWKS；
- 校验 `iss` / `aud` / `exp` / `nbf`，不要只解码 payload；
- `perms` 是粗粒度权限；细粒度属性判定应调用授权服务（规划中）。

## 3. 调用管理 API（规划）

`/api/v1/*` 尚不存在。目标约定：

```js
const res = await fetch(`${baseUrl}/api/v1/accounts?limit=50`, {
  headers: {
    authorization: `Bearer ${accessToken}`,
    'idempotency-key': crypto.randomUUID(), // 写请求
  },
})
```

- 分页用 `?limit=&cursor=`，不要 offset；
- 错误是 RFC 9457 ProblemDetails，包含 `traceId`；
- 写请求支持 `Idempotency-Key`（24 小时窗口），重试必须复用同一个 key。

## 4. 不要做的事

| 反模式 | 原因 |
| --- | --- |
| 把 access token 放进浏览器 localStorage | XSS 可直接窃取；BFF 应把令牌留在服务端 |
| 在 SPA 中使用 client secret | 公共客户端无法保密 |
| 每个请求调用 introspection | 热路径多一次 RTT |
| 信任前端传来的 `realm` | 必须来自已验证令牌 |