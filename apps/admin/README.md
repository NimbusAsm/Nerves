# Nerves Admin Console

Nerves 账户中心的管理后台：Vue 3 + TypeScript + Vite + Pinia + Vue Router + Tailwind CSS 4，
组件按 shadcn-vue 的"源码即组件"方式内置在 `src/components/ui/`，视觉保持干净的管理后台风格。

> 对应方案：`PLAN.md` v0.3 §6.2 / §9；仓库内路径 `apps/admin/`。许可 Apache-2.0。

## 快速开始

环境：Node `>=22.12`（本仓库实测 Node 24.20）、pnpm 11。

```bash
cd apps/admin
pnpm install
pnpm dev        # http://127.0.0.1:5300
```

其他脚本：

| 命令 | 作用 |
| --- | --- |
| `pnpm dev` | Vite 开发服务器（5300），`/api` 代理到 `NERVES_API_URL` |
| `pnpm build` | `vue-tsc --noEmit` 类型检查 + 生产构建，产物在 `dist/` |
| `pnpm preview` | 预览构建产物 |
| `pnpm test` | Vitest（jsdom），35 个用例 |
| `pnpm typecheck` | 只做类型检查 |

### 演示账号（mock 模式）

| 用户名 | 密码 | 权限 |
| --- | --- | --- |
| `admin` | `nerves2026` | 全部管理权限 |
| `auditor` | `nerves2026` | 只读 + 审计导出（可验证路由守卫的 403 分支） |

## 数据源：mock / 真实后端一键切换

默认使用 `src/api/mock.ts` 的确定性内存数据（种子固定，页面、截图、测试看到的都一样）。
复制 `.env.example` 为 `.env.local` 后：

```dotenv
# 走真实网关；配合 vite dev 代理（NERVES_API_URL）或同源部署
VITE_USE_MOCK=false
VITE_API_BASE_URL=/api/v1

# 仅 vite dev 代理使用
NERVES_API_URL=http://127.0.0.1:5280
```

切换后所有请求都走 `src/api/http.ts`（fetch + Bearer token + ProblemDetails 解析），
组件不需要任何改动。`VITE_USE_MOCK` 未设置时按 mock 处理。

## 页面与路由（hash 路由，便于静态托管）

| 路由 | 页面 | 权限码 |
| --- | --- | --- |
| `#/login` | 登录（`POST /api/v1/auth/login`，错误提示、演示账号提示） | 公开 |
| `#/` | 概览：账户总数 / 今日登录 / 失败登录 / 活跃会话 / 7 天趋势 / 最近审计 | `stats:read` |
| `#/accounts` | 账户列表：搜索、状态与域过滤、游标分页、停用/启用 | `identity:account:read` |
| `#/accounts/:id` | 账户详情：资料、所属法人、凭据状态、会话（可强制下线）、审计时间线 | `identity:account:read` |
| `#/legal-entities` | 法人实体列表（实名状态、证件类型） | `identity:legal-entity:read` |
| `#/legal-entities/:id` | 法人详情：实名核验状态与时间线、名下账户 | `identity:legal-entity:read` |
| `#/organizations` | 组织与成员：成员角色调整、添加/移除成员 | `org:organization:read` |
| `#/roles` | 角色与权限：角色授权编辑 + 权限目录 | `authz:role:read` |
| `#/audit` | 审计日志：时间/操作人/结果/动作过滤、事件详情、CSV 导出 | `audit:event:read` |
| `#/notifications` | 通知中心：投递日志（状态过滤、收件人搜索、游标分页）+ 发送模板邮件、事件详情 | `nerves:admin` |
| `#/applications` | 应用目录：列表、新建（client_id / 回调地址 / Logo / scopes）、详情 | `directory:application:read` |

写操作另有权限门控（如 `identity:account:write`、`authz:role:write`、`audit:event:export`），
路由守卫在进入页面前校验一次，按钮再按权限隐藏。

## 管理 API 契约

前端只依赖 `src/api/contract.ts` 的 `NervesApi`，HTTP 实现映射到 PLAN §9.3 的资源骨架：

```text
POST   /api/v1/auth/login                        -> { token, user }
POST   /api/v1/auth/logout
GET    /api/v1/stats                             -> 概览占位数据源
GET    /api/v1/accounts?limit&cursor&q&filter[status]&filter[realm]
GET    /api/v1/accounts/{id}
PATCH  /api/v1/accounts/{id}/status              { status, reason }
GET    /api/v1/accounts/{id}/sessions
DELETE /api/v1/sessions/{id}
GET    /api/v1/accounts/{id}/audit-events
GET    /api/v1/legal-entities?limit&cursor&q&filter[verificationState]
GET    /api/v1/legal-entities/{id}
GET    /api/v1/legal-entities/{id}/accounts
GET    /api/v1/organizations
GET    /api/v1/organizations/{id}/members
POST   /api/v1/organizations/{id}/members        { accountId, orgRole }
PATCH  /api/v1/organizations/{id}/members/{mid}  { orgRole }
DELETE /api/v1/organizations/{id}/members/{mid}
GET    /api/v1/roles | /api/v1/permissions
PATCH  /api/v1/roles/{id}                        { name, description, permissions[] }
GET    /api/v1/applications
POST   /api/v1/applications                      { name, clientId, redirectUris[], scopes[], logoUrl }
GET    /api/v1/audit-events?limit&cursor&filter[from]&filter[to]&filter[result]&actor&action
GET    /api/v1/notifications?limit&cursor&status&to   投递日志（status: pending|sent|failed，to 精确匹配）
GET    /api/v1/notifications/{id}                     单条投递详情
POST   /api/v1/notifications/email                    { to, template, values }（模板见下）
```

- 列表统一返回 `{ items, nextCursor }`；游标对前端不透明，客户端保存游标栈实现"上一页"，不向服务端要 offset。
- 错误按 RFC 9457 ProblemDetails 解析（`title` / `detail` / `status` / `traceId` / `errors`），
  页面直接展示 `detail`。
- 时间均为 ISO-8601，界面按浏览器时区显示。
- 枚举在 wire 上是小写 slug：账户 `active|locked|disabled`、实名 `pending|verified|rejected|expired`、
  结果 `success|failure|denied` 等，与 `src/api/types.ts` 一致。

### 与 PLAN §9.2 的差异（需要 Lead 确认）

- 登录走 IdP 的授权码 + PKCE，访问令牌**只存内存**；HTTP 层带 `Authorization: Bearer`
  并使用 `credentials: 'omit'`。PLAN §9.2 要求管理面板走 **会话 Cookie + CSRF**；当前网关的
  CORS 策略只放行 `Access-Control-Allow-Origin`、不返回 `Access-Control-Allow-Credentials`，
  浏览器端 `credentials: 'include'` 会被 CORS 直接拒绝（实测 `TypeError: Failed to fetch`），
  而控制台本来也没有会话 Cookie。切生产时应改为 BFF：登录态进 HttpOnly Cookie、
  网关补发 `Access-Control-Allow-Credentials: true` 并加 CSRF 校验与刷新机制。
  改动仍只涉及 `src/api/http.ts` 与会话存储，页面不受影响。
- PLAN §6.2 指定 TypeScript 7.0.2；实测 **vue-tsc 3.3.14（Volar）要求 typescript <7**，
  TS 7 的原生编译器不再导出 `lib/tsc`，`vue-tsc` 直接崩溃。因此本包锁 **TypeScript 5.9.3**
  以保留 `.vue` 文件类型检查，待上游支持 TS 7 再升级（这是目前唯一与 PLAN 版本表不一致的依赖）。
- 图标库用 **`@lucide/vue` 1.70.0**（`lucide-vue-next` 1.0.0 已标记 deprecated，官方指向新包），
  其余版本与 PLAN 一致：Vue 3.5.45 / Vite 8.3.6 / Pinia 4.1.0 / Vue Router 5.4.0 / Tailwind 4.3.5 / Vitest 5.1.0。

### 通知模板

前端只认识通知服务内置的三个模板，占位符契约与后端 `EmailTemplateCatalog` 保持一致
（`src/lib/notification-templates.ts`，单测会校验它与 i18n 键集同步）：

| 模板 | 占位符 | 用途 |
| --- | --- | --- |
| `account.welcome` | `displayName` / `username` / `issuer` | 账户创建后的欢迎邮件 |
| `account.password-reset` | `displayName` / `minutes` / `resetUrl` | 重置口令链接 |
| `security.sign-in` | `displayName` / `device` / `occurredAt` | 新设备登录提醒 |

表单在提交前会校验必填占位符并做去空格处理；服务端仍会再校验一次，
未知模板或缺参数返回 400（界面直接展示 `detail`）。

## 目录结构

```text
apps/admin/
├─ src/
│  ├─ api/            types / contract / http(真实) / mock(内存) / session-storage / index(开关)
│  ├─ components/
│  │  ├─ layout/      AdminShell · SidebarNav · TopBar · navigation
│  │  └─ ui/          Button / Input / Select / Textarea / Badge / Card / Table / TableMessage / Modal
│  ├─ composables/    useCursorList · useAccountStatusAction · useLabels
│  ├─ i18n/           zh-CN（完整）· en-US（完整）· locale 持久化
│  ├─ lib/            cn(class 合并) · format · csv · url · debounce
│  ├─ router/         routes + 登录/权限守卫
│  ├─ stores/         auth · toast
│  ├─ styles/         Tailwind 4 主题令牌 + 表格基础样式
│  └─ views/          login / dashboard / accounts / legal-entities / organizations / roles / audit / applications
└─ tests/             守卫 · auth store · mock API · CSV · i18n 覆盖率 · 页面挂载
```

设计取舍：

- **数据源集中在 API 层**：页面不出现任何 fixture；`mock.ts` 实现了与 HTTP 版完全相同的契约、
  校验与审计副作用（例如停用账户必须填原因，mock 会抛 400 并记录审计）。
- **表格与状态组件内置**：不引 UI 框架运行时，样式与交互可审计，Tailwind 4 用 `@theme static`
  定义语义色令牌（canvas/surface/line/ink/muted/brand），避免令牌被摇树后 `var()` 失效。
- **游标分页**：`useCursorList` 维护游标栈，实现上一页/下一页、请求竞态丢弃（快速翻页只认最后一次响应）。
- **i18n**：`vue-i18n` 组合式 API，`zh-CN` 与 `en-US` 键集合必须一致；`tests/i18n-coverage.spec.ts`
  会扫描源码里所有 `t('...')` 字面量，缺键即失败。顶栏可切语言，选择存 `localStorage`。
- **表单错误**：服务端 ProblemDetails 的 `detail` 直接展示在弹窗/页面的 `role="alert"` 区域；
  mock 对停用原因、client_id 重复、非法回调地址等返回 400/409。

## 测试

```bash
pnpm test
```

覆盖：

1. **路由守卫**：匿名访问受保护页重定向到登录并保留 `redirect`；已登录访问 `/login` 回概览；
   权限不足进 403；真实 `createAppRouter` 接线；`safeRedirect` 拒绝 `//evil.example` 与外部 URL。
2. **auth store**：登录后落盘、刷新后恢复、错误密码不建会话、审计员只读、登出与 401 清理。
3. **mock API**：游标分页无重叠、状态/搜索过滤、停用需原因且写审计、应用创建校验（重复 client_id / 非法回调）、
   组织成员角色更新、统计与数据集一致、坏游标 400。
4. **CSV**：逗号/引号/换行转义、BOM、文件名。
5. **i18n**：中英键一致、无空文案、源码引用的键都存在。
6. **页面挂载**：登录页错误提示与成功跳转、账户列表从 API 渲染并打开停用弹窗、
   整体外壳在概览/审计/应用目录之间导航、组织成员角色下拉即时生效。
7. **数据源开关**：`VITE_USE_MOCK` 缺省为 mock，设为 `false` 时实例是真的 HTTP 客户端。

## 已知限制 / 下一步

- 没有接入 OIDC 授权码流程（PLAN 的 `/connect/*`），管理后台按任务约定使用 `/auth/login`；
  生产化时按上文的 BFF + CSRF 方案收敛。
- 概览的 `GET /api/v1/stats` 是占位数据源；后端就绪后换成聚合查询或异步物化视图。
- 审计导出在前端生成 CSV（单次最多翻 20 页 / 2000 条），服务端导出接口（含链式哈希校验）留待 §9.3 的
  `audit-events/export` 落地。
- 未做 E2E（Playwright），当前以 Vitest + 页面挂载为回归门槛；CI 接入见根目录 `.github/workflows`。
- 未引入 orval/OpenAPI 生成：契约还没冻结，`types.ts` 手写并在接入后替换为生成物。

## License

Apache-2.0，见仓库根目录 `LICENSE`。


## Production image

```bash
docker build -f apps/admin/Dockerfile -t nerves-admin \
  --build-arg VITE_API_BASE_URL=https://nerves.example.com/api/v1 \
  --build-arg VITE_IDP_URL=https://auth.example.com \
  --build-arg VITE_USE_MOCK=false \
  apps/admin
```

The image serves the SPA on port 8080 with /healthz, SPA fallback and the
static /oidc-callback.html. The redirect URI is derived from the browser origin,
so register https://<admin-host>/oidc-callback.html on the nerves-admin client.


## Browser end-to-end

```bash
# backend + console running (see deploy/docker/README.md)
pnpm exec playwright install chromium
pnpm test:e2e
```

The spec signs in through the real IdP (PKCE) and asserts the console loads and
that no token is left in localStorage. CI runs it in the admin-e2e workflow.
