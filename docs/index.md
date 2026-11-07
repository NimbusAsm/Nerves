---
layout: home

hero:
  name: Nerves
  text: 一个法人，一个实名身份，多个网络账户
  tagline: 开源自托管的账户中心 · 认证、授权、身份验证与 IdP 四合一 · 强制 PostgreSQL · .NET 10 微服务
  image:
    src: /logo.svg
    alt: Nerves
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/quickstart
    - theme: alt
      text: 实现状态
      link: /guide/status

features:
  - title: 法人 / 账户双层模型
    details: 法律实体承载实名与责任，网络账户承载登录与业务身份。一个实名实体可以拥有多个账户，跨产品复用同一身份。
  - title: 认证 + 授权 + 身份验证 + IdP
    details: OIDC / OAuth 2.1 提供方、RBAC 与属性条件授权、实名状态机与目录服务，统一在一套部署里。
  - title: 为水平扩展设计
    details: 分片键、路由索引与 O(1) 请求约束写进设计；v1 交付“分片就绪”，多分片在 v1.1 通过配置启用。
  - title: 自托管，Apache-2.0
    details: 没有 SaaS 依赖，数据留在自己的集群里。生产部署使用 Helm，开发用 Docker Compose 一条命令起全套依赖。
---

## 当前状态

::: warning M0 · 骨架阶段，尚不可用于真实登录
仓库正在从零重建：六个服务已可启动并提供健康检查与 OpenAPI，**登录、OIDC、管理 API、
会话、授权与目录能力尚未实现**。各能力的具体状态见 [实现状态](/guide/status)；
v1.1 才有的能力都已显式标注，请不要按“已交付”来规划接入。
:::

## 这不是 Elixir Nerves

本项目与 Elixir 生态的 [Nerves](https://nerves-project.org) 嵌入式框架**没有任何关系**，
只是名称相同。Nerves 的包名统一使用 `Nerves.*`、`@nerves/*` 与 `nerves-sdk` 前缀以便区分。

## 从这里开始

| 你想做的事 | 去哪里 |
| --- | --- |
| 了解现在能跑什么、不能跑什么 | [实现状态](/guide/status) |
| 在本地启动骨架并调用健康检查 | [快速开始](/guide/quickstart) |
| 理解法人、账户、组织与权限 | [核心概念](/guide/concepts/legal-entity) |
| 评估 .NET / Node / SPA / 移动端接入方式 | [接入指南](/guide/integration/) |
| 查看已实现与规划中的 API | [API 概览](/api/overview) |
| 部署到 Kubernetes | [Helm 部署](/deploy/helm) |
| 做备份、升级与密钥轮换 | [运维](/operations/backup) |

## 设计原则（摘要）

- **强制 PostgreSQL**（D02）：不提供 SQLite / MySQL 模式；迁移只针对 PG 方言。
- **契约先行**：内部接口以 `proto/` 为唯一事实源，CI 执行 `buf lint` 与 `buf breaking`。
- **数据所有权**：每个服务独占 schema，跨服务只存软引用，不用跨库外键。
- **expand / contract 迁移**（D18）：先加后删、跨两个版本，保证应用可回滚。
- **默认拒绝**（安全基线）：授权端点必须显式声明权限码；管理 API 与内部 API 分离。
