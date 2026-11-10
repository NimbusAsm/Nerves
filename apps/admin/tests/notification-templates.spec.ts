import { describe, expect, it } from 'vitest'

import {
  extractPlaceholders,
  findNotificationTemplate,
  missingTemplateValues,
  NOTIFICATION_TEMPLATES,
  normalizeTemplateValues,
  templatePlaceholders,
} from '@/lib/notification-templates'

describe('notification templates', () => {
  it('lists exactly the three templates the service ships', () => {
    expect(NOTIFICATION_TEMPLATES.map((template) => template.code)).toEqual([
      'account.welcome',
      'account.password-reset',
      'security.sign-in',
    ])
  })

  it('keeps the placeholder contract in sync with the service', () => {
    expect(templatePlaceholders('account.welcome')).toEqual(['displayName', 'username', 'issuer'])
    expect(templatePlaceholders('account.password-reset')).toEqual(['displayName', 'minutes', 'resetUrl'])
    expect(templatePlaceholders('security.sign-in')).toEqual(['displayName', 'device', 'occurredAt'])
    expect(templatePlaceholders('unknown.template')).toEqual([])
  })

  it('looks templates up by code', () => {
    expect(findNotificationTemplate('security.sign-in')?.code).toBe('security.sign-in')
    expect(findNotificationTemplate('missing')).toBeUndefined()
  })

  it('reports missing values and trims whitespace', () => {
    expect(missingTemplateValues('account.welcome', {})).toEqual(['displayName', 'username', 'issuer'])
    expect(
      missingTemplateValues('account.welcome', { displayName: 'Ada', username: '  ', issuer: 'https://x' }),
    ).toEqual(['username'])

    const normalized = normalizeTemplateValues({ displayName: '  Ada  ', username: '', issuer: 'https://x' })
    expect(normalized).toEqual({ displayName: 'Ada', issuer: 'https://x' })
  })

  it('extracts unique placeholders from rendered text', () => {
    expect(extractPlaceholders('hi {{name}}, reset at {{ url }} / {{name}}')).toEqual(['name', 'url'])
    expect(extractPlaceholders('no placeholders')).toEqual([])
  })

  it('every template i18n key exists in both locales', async () => {
    const zh = (await import('@/i18n/zh-CN')).default as Record<string, unknown>
    const en = (await import('@/i18n/en-US')).default as Record<string, unknown>

    const read = (catalog: Record<string, unknown>, key: string): unknown =>
      key.split('.').reduce<unknown>((node, part) => {
        if (node === null || typeof node !== 'object') return undefined
        return (node as Record<string, unknown>)[part]
      }, catalog)

    for (const template of NOTIFICATION_TEMPLATES) {
      expect(read(zh, template.labelKey), template.labelKey).toBeTruthy()
      expect(read(en, template.labelKey), template.labelKey).toBeTruthy()
      expect(read(zh, template.descriptionKey), template.descriptionKey).toBeTruthy()
      expect(read(en, template.descriptionKey), template.descriptionKey).toBeTruthy()

      for (const placeholder of template.placeholders) {
        expect(read(zh, placeholder.labelKey), placeholder.labelKey).toBeTruthy()
        expect(read(en, placeholder.labelKey), placeholder.labelKey).toBeTruthy()
      }
    }
  })
})
