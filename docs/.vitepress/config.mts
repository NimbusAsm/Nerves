import { defineConfig } from 'vitepress'

export default defineConfig({
  lang: 'zh-CN',
  title: 'Nerves',
  titleTemplate: ':title · Nerves 文档',
  description:
    'Nerves 是一个开源自托管的账户中心：一个法人一个实名身份，多个网络账户，认证、授权、身份验证与 IdP 四合一。',
  cleanUrls: true,
  lastUpdated: true,
  // Local development URLs intentionally do not resolve during the build.
  ignoreDeadLinks: [/^http:\/\/localhost:\d+(\/.*)?$/],
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }],
    ['meta', { name: 'theme-color', content: '#0f766e' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Nerves 文档' }],
    [
      'meta',
      {
        property: 'og:description',
        content: '开源自托管的账户中心：法人 / 网络账户双层身份模型，OIDC、授权与目录服务。',
      },
    ],
  ],
  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'Nerves',
    nav: [
      { text: '文档', link: '/guide/status', activeMatch: '^/guide/(status|quickstart|roadmap|concepts)' },
      { text: '接入', link: '/guide/integration/', activeMatch: '^/guide/integration/' },
      { text: 'API', link: '/api/overview', activeMatch: '^/api/' },
      { text: '部署', link: '/deploy/helm', activeMatch: '^/deploy/' },
      { text: '运维', link: '/operations/backup', activeMatch: '^/operations/' },
    ],
    sidebar: [
      {
        text: '开始',
        items: [
          { text: '实现状态', link: '/guide/status' },
          { text: '快速开始', link: '/guide/quickstart' },
          { text: '版本与路线图', link: '/guide/roadmap' },
          { text: '发布流程', link: '/guide/releases' },
        ],
      },
      {
        text: '核心概念',
        collapsed: false,
        items: [
          { text: '法人实体（LegalEntity）', link: '/guide/concepts/legal-entity' },
          { text: '网络账户（NetworkAccount）', link: '/guide/concepts/network-account' },
          { text: '组织与成员', link: '/guide/concepts/organization' },
          { text: '权限与授权', link: '/guide/concepts/authorization' },
        ],
      },
      {
        text: '接入指南',
        collapsed: false,
        items: [
        {
          text: '运维手册',
          items: [
            { text: '备份与恢复', link: '/guide/operations/backup-restore' },
            { text: '密钥轮换', link: '/guide/operations/key-rotation' },
            { text: '升级与回滚', link: '/guide/operations/upgrade' },
            { text: 'LDAP 目录同步', link: '/guide/operations/ldap-sync' },
            { text: 'Session 多库分片', link: '/guide/operations/session-sharding' },
            { text: '保留策略与审计 outbox', link: '/guide/operations/retention' },
            { text: '上线安全检查', link: '/guide/operations/security-checklist' },
            { text: '通知服务（Email）', link: '/guide/operations/notification' },
            { text: '控制台聚合 API', link: '/guide/operations/console-api' },
            { text: '运维 CLI（nerves）', link: '/guide/operations/cli' },
          ],
        },
          { text: '接入概览', link: '/guide/integration/' },
          { text: '.NET 服务端', link: '/guide/integration/dotnet' },
          { text: 'Node.js / BFF', link: '/guide/integration/node' },
          { text: '单页应用（SPA）', link: '/guide/integration/spa' },
          { text: '移动端', link: '/guide/integration/mobile' },
        ],
      },
      {
        text: 'API',
        collapsed: false,
        items: [
          { text: 'API 概览', link: '/api/overview' },
          { text: 'gRPC 契约', link: '/api/grpc' },
        ],
      },
      {
        text: '部署',
        collapsed: false,
        items: [
          { text: 'Helm（生产）', link: '/deploy/helm' },
          { text: 'Docker Compose（开发）', link: '/deploy/compose' },
        ],
      },
      {
        text: '运维',
        collapsed: false,
        items: [
          { text: '备份与恢复', link: '/operations/backup' },
          { text: '升级与回滚', link: '/operations/upgrade' },
          { text: '密钥轮换', link: '/operations/key-rotation' },
        ],
      },
      {
        text: '其他',
        items: [{ text: '常见问题（FAQ）', link: '/faq' }],
      },
    ],
    search: {
      provider: 'local',
      options: {
        translations: {
          button: {
            buttonText: '搜索文档',
            buttonAriaLabel: '搜索文档',
          },
          modal: {
            noResultsText: '没有找到结果',
            resetButtonTitle: '清除条件',
            footer: {
              selectText: '选择',
              navigateText: '切换',
              closeText: '关闭',
            },
          },
        },
      },
    },
    editLink: {
      pattern: 'https://github.com/NimbusAsm/Nerves/edit/main/docs/:path',
      text: '在 GitHub 上编辑此页',
    },
    lastUpdatedText: '最后更新',
    outline: {
      level: [2, 3],
      label: '本页目录',
    },
    docFooter: {
      prev: '上一页',
      next: '下一页',
    },
    returnToTopLabel: '回到顶部',
    darkModeSwitchLabel: '主题',
    sidebarMenuLabel: '菜单',
    socialLinks: [
      { icon: 'github', link: 'https://github.com/NimbusAsm/Nerves' },
    ],
    footer: {
      message: '以 Apache-2.0 许可发布 · 贡献需 DCO 签署',
      copyright: 'Nerves contributors',
    },
  },
})
