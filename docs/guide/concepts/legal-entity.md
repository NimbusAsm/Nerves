# 法人实体（LegalEntity）

> **状态**：领域模型与 EF 持久化 ✅ 已实现；管理 API 🧭 v1.0 计划。
> 实名核验 Provider 与通道适配 🧭 v1.0 / 🔭 v1.1 计划。

## 为什么需要这一层

大多数系统里“用户”既是身份又是账号：改用户名、换密码、注销都作用在同一个对象上。
Nerves 把两件事拆开：

- **法律实体（LegalEntity）** 承载实名信息与法律责任；
- **网络账户（NetworkAccount）** 承载登录标识、凭据与业务身份。

这样可以回答“这个自然人 / 企业在系统里一共有多少账户”，也能让注销收敛到实体级别。
一个法律实体对应**一个实名身份**，但可以有**任意多个网络账户**（跨 realm 也可以）。

```text
LegalEntity（1）
   │ 1 : N
   ▼
NetworkAccount（0..N）
```

## 实体类型

| 值 | 含义 | 典型证件 |
| --- | --- | --- |
| `NaturalPerson` | 自然人 | 居民身份证、护照 |
| `LegalPerson` | 法人 / 企业 | 统一社会信用代码 |
| `Government` | 政府机构 | 机构代码 |
| `Other` | 其他组织 | 由部署方定义 |

类型来自 `LegalEntityType` 枚举；证件类型来自 `IdDocumentType`
（`ResidentIdentityCard` / `Passport` / `UnifiedSocialCreditCode` / `Other`）。

## 字段（已落地的模型）

| 字段 | 说明 |
| --- | --- |
| `Id` | UUIDv7，时间有序；不编码分片号 |
| `Type` | 实体类型，存为整数 |
| `DisplayName` | 展示名，1–200 字符，去除首尾空白 |
| `RealName.DocumentType` | 证件类型 |
| `RealName.Hash` | 证件号的 **HMAC 摘要**，64 位十六进制；全局唯一 |
| `RealName.Country` | ISO 3166-1 alpha-2 国家码，大写 |
| `VerificationState` | `Pending` / `Verified` / `Rejected` / `Expired` |
| `VerifiedAt` | 通过核验的时间 |
| `ProviderReference` | 核验通道返回的引用（不存明文证件） |
| `CreatedAt` / `UpdatedAt` | UTC 时间 |
| `xmin` | PostgreSQL 行版本，用作并发令牌 |

**明文证件号从不入库**：`RealNameId.Create` 只接受调用方用服务端密钥算好的
HMAC 摘要（64 位十六进制）+ 证件类型 + 国家。数据库泄漏不会直接暴露证件号。
HMAC 密钥的生命周期由部署方管理，见 [密钥轮换](/operations/key-rotation)。

## 实名状态机

```text
Pending ──MarkVerified──▶ Verified ──Expire──▶ Expired
   │
   └────MarkRejected────▶ Rejected
```

约束（由领域方法强制）：

- 已 `Verified` 的实体再次 `MarkVerified` → 冲突；`Expired` 的实体不能重新核验，
  需要新建实体；
- 只有 `Pending` 可以 `MarkRejected`；
- 只有 `Verified` 可以 `Expire`。

## 不变量

| 编号 | 规则 | 落地方式 |
| --- | --- | --- |
| I1 | 一个法律实体只有一个实名 ID | `id_doc_hash` 唯一索引 |
| I2 | 一个网络账户只属于一个法律实体（服务账户除外） | 领域方法 + 外键/索引 |
| I5 | 服务账户不绑定法律实体 | `NetworkAccount.Create` 校验 |
| I7 | 实名状态变化产生领域事件，所有账户可感知 | 目标能力；事件模型已有基元，业务事件待实现 |

## 与账户、组织的关系

- 一个实体 → 多个网络账户（见 [网络账户](/guide/concepts/network-account)）。
- 组织（[组织与成员](/guide/concepts/organization)）是 directory 服务的概念，
  表示“单位 / 租户 / 部门”，**不是**法律实体本身；成员关系绑定的是网络账户。
- 授权中的 `leid` claim 表示法律实体；默认不下发给普通业务应用，避免把
  “同一自然人”的关联关系扩散到所有产品。

## 当前可用性

| 能力 | 状态 |
| --- | --- |
| 领域模型、EF 配置、迁移 | ✅ 已实现 |
| 通过 HTTP 创建 / 查询法人 | 🧭 v1.0 计划（当前无端点） |
| 实名核验 Provider 契约 | 🧭 v1.0 计划 |
| `Compliance.CN` 生产通道、日志归档、国密 | 🔭 v1.1 计划 |
