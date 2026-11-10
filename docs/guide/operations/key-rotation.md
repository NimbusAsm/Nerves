# 密钥轮换

## 需要轮换的密钥

| 密钥 | 用途 | 轮换影响 |
| --- | --- | --- |
| OpenIddict 签名证书 / 私钥 | ID Token、访问令牌、logout_token | 旧令牌在校验方刷新 JWKS 前可能被拒绝 |
| DataProtection 环 | TOTP 种子、Passkey 私钥、Cookie | 不可删除旧环，否则历史密文无法解密 |
| HMAC 标识哈希键 | 登录标识索引 | **不可轮换**，否则所有标识索引失效 |
| 管理员令牌（如启用） | 服务间调用 | 同步更新所有调用方 |

## 签名密钥轮换

1. 准备新的 PFX，更新 `OpenIddict:SigningCertificatePath`（滚动发布）。
2. 旧证书保留至少一个令牌生命周期，便于校验方缓存过期。
3. 观察 JWKS 端点包含新 kid，且资源服务 `/api/v1/stats` 返回 200。

## DataProtection 轮换

1. 生成新密钥环但**不要删除旧环**。
2. 逐步让服务加载目录中全部密钥（默认行为）。
3. 通过管理 API 重新登记 TOTP/Passkey 后，旧条目自然淘汰。

## 禁止事项

- 不要清空 `DataProtection` 目录。
- 不要在没有迁移工具的情况下更换标识哈希键。
