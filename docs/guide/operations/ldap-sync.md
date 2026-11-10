# LDAP 目录同步

> 状态：🧭 v1.0 计划能力，本页随 PR 一起评审。
> LDAP **登录**（联邦）与 LDAP **目录同步**（账户供给）是两条独立链路：前者只校验口令，后者把目录条目变成账户。

## 它做什么

IdP 按固定间隔读取企业目录，调用 Identity 的 `UpsertDirectoryAccount` 完成供给：

- 目录里新出现的条目 → 在 Identity 中创建账户（`AccountType.Managed`）；
- 已有账户的显示名、邮箱变化 → 更新这两个字段；
- 目录中消失的账户 → **不删除、不锁定**（同步是单向增量，不是镜像）；
- 任何情况下都**不写入口令**：本地口令、TOTP、Passkey 全部保留。

## 配置

连接参数复用现有的 `Ldap` 段（登录也在用），同步开关放在 `Ldap:Sync` 子段：

```json
{
  "Ldap": {
    "Enabled": true,
    "Host": "ldap.example.com",
    "Port": 389,
    "UseSsl": true,
    "BindDn": "cn=nerves,ou=services,dc=example,dc=com",
    "BindPassword": "<从 Secret 注入>",
    "BaseDn": "dc=example,dc=com",
    "Sync": {
      "Enabled": true,
      "IntervalMinutes": 15,
      "StartDelaySeconds": 5,
      "Realm": "nerves",
      "BaseDn": "ou=people,dc=example,dc=com",
      "Filter": "(objectClass=inetOrgPerson)",
      "PageSize": 200,
      "AttributeMap": {
        "Username": "uid",
        "Email": "mail",
        "DisplayName": "cn"
      }
    }
  }
}
```

| 键 | 说明 |
| --- | --- |
| `Ldap:Sync:Enabled` | 总开关；关闭时后台服务不启动，状态端点仍可读 |
| `IntervalMinutes` | 两次同步的间隔，运行期夹在 1–1440 分钟 |
| `StartDelaySeconds` | 首个周期的等待时间，给依赖服务留出启动余量 |
| `Realm` | 新建账户落入的 realm |
| `BaseDn` | 检索根；留空时回落到 `Ldap:BaseDn` |
| `Filter` | 检索过滤器，默认 `(objectClass=inetOrgPerson)` |
| `PageSize` | 简单分页每页条数（1–1000） |
| `AttributeMap` | 用户名 / 邮箱 / 显示名分别取哪个属性 |

> 服务账号（`BindDn`）只需要目录的**读**权限；不要用域管理员账号。

## 幂等与冲突

- 先按 `realm + username` 匹配，再按 `email` 匹配；
- 匹配到已存在账户时只更新显示名与邮箱，用户名不变；
- 邮箱已被**另一个**账户占用时，该条目标记为 `failed`，其余条目继续；
- 目录属性为空的条目不创建账户，计入 `skipped`。

## 账户类型与法人实体

目录供给的账户类型是 `Managed`（由组织代管）。按不变量 I2，这类账户必须挂在法人实体下，
因此首次创建时会同时建立一个占位法人实体（`Type=Other`，实名哈希由目录标识计算）。
后续如果要把这些账户并入真实法人，需要单独的数据合并流程，不在本同步的范围内。

## 状态端点

`GET /api/v1/admin/directory-sync/status`（需要 `nerves:admin` 权限）：

```json
{
  "enabled": true,
  "intervalMinutes": 15,
  "realm": "nerves",
  "baseDn": "ou=people,dc=example,dc=com",
  "filter": "(objectClass=inetOrgPerson)",
  "attributeMap": { "username": "uid", "email": "mail", "displayName": "cn" },
  "lastRun": {
    "startedAt": "2026-11-10T18:06:51Z",
    "completedAt": "2026-11-10T18:06:51Z",
    "durationSeconds": 0.02,
    "running": false,
    "error": null
  },
  "counters": {
    "scanned": 6,
    "created": 0,
    "updated": 1,
    "unchanged": 5,
    "skipped": 0,
    "failed": 0
  }
}
```

每次同步结束都会向审计服务写入一条 `directory.sync.completed`（成功或 partial）。

## 验证记录（开发环境）

在本地 OpenLDAP（`osixia/openldap:1.5.0`，`dc=example,dc=com`，`ou=people` 下 6 个用户）上实测：

1. 首次运行：`created=3 updated=0 unchanged=0`，Identity 出现 3 个账户；
2. 再跑一次：`created=0 updated=0 unchanged=3`，账户数不增加（幂等）；
3. 目录新增 2 个用户：`created=2`，并各自生成占位法人实体；
4. 修改某个用户的 `cn`：下一轮 `updated=1`，显示名同步，口令哈希逐字节不变；
5. 从目录删除一个用户：该账户保持原状（不删除）。

## 排障

| 现象 | 排查 |
| --- | --- |
| `LastError=InvalidOperationException` | `Ldap:BindDn` 为空或绑定失败 |
| 全部条目 `skipped` | `AttributeMap:Username` 指向的属性在条目上不存在 |
| 全部 `failed=email_conflict` | 邮箱已被其他账户占用，先合并账户 |
| 状态一直 `running=true` | 目录检索卡住；检查 `TimeoutSeconds` 与网络 |
