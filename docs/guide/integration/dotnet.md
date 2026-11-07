# .NET 服务端接入

::: warning 当前不可联调
Nerves 的 OIDC 端点与 `Nerves.Client` SDK 都还未实现（🧭 v1.0 计划）。
下面示例按目标契约编写，能编译的前提是你已有一个符合 OIDC 的 issuer；
现在把它指向 Nerves 会失败。
:::

## 场景

一个 ASP.NET Core 应用（MVC / Razor Pages / Blazor Server / BFF）作为**机密客户端**，
用 Authorization Code 流程登录，并在调用后端 API 时校验 JWT。

## 1. 登录（OIDC + Cookie）

```bash
dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect
```

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie("Cookies", options =>
    {
        options.Cookie.Name = "__Host-nerves";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
    })
    .AddOpenIdConnect("nerves", options =>
    {
        options.Authority = builder.Configuration["Nerves:Issuer"]; // https://id.example.com
        options.ClientId = builder.Configuration["Nerves:ClientId"];
        options.ClientSecret = builder.Configuration["Nerves:ClientSecret"];

        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        // 保留原始 claim 名称（sub / leid / realm / roles …）。
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "roles";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
    });

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext http) =>
    http.User.Identity?.IsAuthenticated == true
        ? $"hello {http.User.Identity.Name}"
        : "anonymous");

app.MapGet("/login", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/" },
    [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
}).RequireAuthorization();

app.Run();
```

要点：

- `MapInboundClaims = false` 避免 `sub` 被改写成 `http://schemas.../nameidentifier`；
- `SaveTokens = true` 才能在后端拿到 access token 调用 API；
- Cookie 名用 `__Host-` 前缀（要求 Secure、Path=/、无 Domain）；本地 http 开发时该前缀不会生效，可临时换名并只在生产启用 Secure。
- 若需要 `leid`（法律实体），在 client 注册时申请该 claim；普通应用默认拿不到。

## 2. 资源服务校验 JWT

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

```csharp
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Nerves:Issuer"];
        options.Audience = builder.Configuration["Nerves:Audience"]; // 例如 crm-api
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidAlgorithms = ["ES256", "RS256"];
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(60);
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("invoice.read", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("perms", "crm:invoice:read"));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/invoices", () => Results.Ok(Array.Empty<object>()))
   .RequireAuthorization("invoice.read");
```

- `Authority` 会自动拉取 Discovery 与 JWKS 并缓存；
- 多副本部署时 JWKS 缓存各自持有，密钥轮换期间多 `kid` 并存即可；
- 不要让每个请求调用 introspection。

## 3. 调用 Nerves 管理 API（规划）

目标形态使用 fluent SDK。`sdk/dotnet/Nerves.Client` 已有源码与测试，但还没有
NuGet 发布版，且管理 API 尚未实现，因此下面的代码目前只能对 mock 服务端运行：

```csharp
var nerves = new NervesClient(baseUrl, token);

var account = await nerves.Accounts
    .Create()
    .ForEntity(entityId)
    .WithUsername("alice")
    .WithEmail("alice@example.com")
    .SendAsync(ct);
```

在 SDK 发布前请直接调用 `/api/v1/*`（尚未实现）。写请求应当带
`Idempotency-Key`；分页使用游标，不用 offset 深翻页。

## 4. 开发与环境

| 配置 | 说明 |
| --- | --- |
| `Nerves:Issuer` | issuer 必须与 Discovery 的 `issuer` 完全一致（含末尾斜杠处理） |
| `Nerves:ClientSecret` | 从 Secret / 环境变量注入，不能入库或进 git |
| `Nerves:Audience` | API 的 audience，不等于 client id |

错误信息里不要区分“用户不存在”和“口令错误”；Nerves 的目标契约同样如此。
