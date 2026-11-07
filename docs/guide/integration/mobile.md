# 移动端接入

::: warning 当前不可联调
Nerves 的 OIDC 端点尚未实现（🧭 v1.0 计划）。示例按 RFC 8252（OAuth 2.0 for
Native Apps）编写；在 Nerves 交付 IdP 之前，可用于对接其它标准 OIDC 提供方。
:::

## 原则

- 原生应用是**公共客户端**，没有 client secret；
- 必须使用 **Authorization Code + PKCE**，不要用隐式流程；
- 回调地址使用 **自定义 scheme**（`com.example.app:/oauth2redirect`）或
  loopback（`http://127.0.0.1:{port}`，仅限本机）；
- 系统浏览器 / 受信任的浏览器组件完成登录，不要在 App 内嵌 WebView 收集口令；
- access token 短寿命，refresh token 必须放入 Keychain / Keystore；
- 启用刷新令牌轮换与重用检测（服务端能力，规划中）。

## Android（AppAuth）

依赖：

```kotlin
// app/build.gradle.kts
implementation("net.openid:appauth:0.11.1")
```

```kotlin
import net.openid.appauth.AuthorizationRequest
import net.openid.appauth.AuthorizationService
import net.openid.appauth.AuthorizationServiceConfiguration
import net.openid.appauth.ResponseTypeValues
import android.net.Uri

val serviceConfig = AuthorizationServiceConfiguration(
    Uri.parse("https://id.example.com/connect/authorize"),
    Uri.parse("https://id.example.com/connect/token")
)

val authRequest = AuthorizationRequest.Builder(
    serviceConfig,
    "nerves-mobile",                       // client_id
    ResponseTypeValues.CODE,
    Uri.parse("com.example.app:/oauth2redirect")
)
    .setScope("openid profile email offline_access")
    .build()

val authService = AuthorizationService(context)
authService.performAuthorizationRequest(
    authRequest,
    PendingIntent.getActivity(context, 0, intent, PendingIntent.FLAG_MUTABLE)
)
```

回调处理（在 Activity 的 `onCreate` / `onNewIntent`）：

```kotlin
val response = AuthorizationResponse.fromIntent(intent)
val ex = AuthorizationException.fromIntent(intent)
if (response != null) {
    authService.performTokenRequest(response.createTokenExchangeRequest()) { tokenResponse, _ ->
        // tokenResponse.accessToken / refreshToken / idToken
    }
}
```

- AppAuth 自动生成 PKCE 的 `code_verifier` / `code_challenge`；
- `PendingIntent` 的 flag 需按 targetSdk 选择；
- 在 `AndroidManifest.xml` 注册 `com.example.app` scheme 的 intent-filter。

## iOS（AppAuth）

```swift
import AppAuth

let configuration = OIDServiceConfiguration(
    authorizationEndpoint: URL(string: "https://id.example.com/connect/authorize")!,
    tokenEndpoint: URL(string: "https://id.example.com/connect/token")!
)

let request = OIDAuthorizationRequest(
    configuration: configuration,
    clientId: "nerves-mobile",
    scopes: ["openid", "profile", "email", "offline_access"],
    redirectURL: URL(string: "com.example.app:/oauth2redirect")!,
    responseType: OIDResponseTypeCode,
    additionalParameters: nil
)

let session = OIDAuthorizationService.present(
    request,
    presenting: viewController
) { response, error in
    guard let response else { return }
    // response.authorizationCode -> 用 token 端点换取令牌（AppAuth 的 token exchange）
}
```

- `OIDAuthorizationRequest` 会生成 PKCE 参数；
- 在 `Info.plist` 注册 URL scheme；
- 使用 Keychain 存储 refresh token，并设置 `kSecAttrAccessibleAfterFirstUnlock`。

## 刷新与登出

```text
登录 → access token（短 TTL）→ 到期前用 refresh token 轮换
                                      │
                                      └─ 重用旧 token → 整族作废（服务端能力，规划中）
```

- 不要长期保存 access token；
- 登出时删除本地令牌并调用 `/connect/endsession`（规划）；
- 设备丢失时由服务端吊销会话（规划），App 侧不能只靠本地删除。

## 当前状态

| 能力 | 状态 |
| --- | --- |
| 授权码 + PKCE | 🧭 v1.0 计划 |
| 自定义 scheme / loopback 回调 | 🧭 v1.0 计划 |
| 刷新令牌轮换与重用检测 | 🧭 v1.0 计划 |
| 原生 SDK | 🧭 v1.0 计划（使用 AppAuth 等标准库即可） |
