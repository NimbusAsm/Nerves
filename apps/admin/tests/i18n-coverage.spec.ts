import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'

import { describe, expect, it } from 'vitest'

import { NAV_GROUPS } from '@/components/layout/navigation'
import enUS from '@/i18n/en-US'
import zhCN from '@/i18n/zh-CN'
import { routes } from '@/router'

const root = resolve(process.cwd(), 'src')

function walk(directory: string, files: string[] = []): string[] {
  for (const entry of readdirSync(directory)) {
    const full = join(directory, entry)
    if (statSync(full).isDirectory()) walk(full, files)
    else if (/\.(ts|vue)$/.test(entry)) files.push(full)
  }
  return files
}

function flatten(source: Record<string, unknown>, prefix = ''): Map<string, unknown> {
  const result = new Map<string, unknown>()
  for (const [key, value] of Object.entries(source)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      for (const [nestedKey, nestedValue] of flatten(value as Record<string, unknown>, path)) {
        result.set(nestedKey, nestedValue)
      }
    } else {
      result.set(path, value)
    }
  }
  return result
}

const zhKeys = flatten(zhCN as unknown as Record<string, unknown>)
const enKeys = flatten(enUS as unknown as Record<string, unknown>)

describe('i18n catalogs', () => {
  it('use the exact same key set in both locales', () => {
    expect([...enKeys.keys()].sort()).toEqual([...zhKeys.keys()].sort())
  })

  it('never ship an empty message', () => {
    for (const [key, value] of zhKeys) expect(value, key).toBeTruthy()
    for (const [key, value] of enKeys) expect(value, key).toBeTruthy()
  })

  it('covers navigation labels and route metadata keys', () => {
    const missing: string[] = []

    for (const group of NAV_GROUPS) {
      const keys = [group.labelKey, ...group.items.map((item) => item.labelKey)]
      for (const key of keys) {
        if (key && (!zhKeys.has(key) || !enKeys.has(key))) missing.push(key)
      }
    }

    const visit = (records: typeof routes): void => {
      for (const record of records) {
        for (const key of [record.meta?.titleKey, record.meta?.sectionKey]) {
          if (typeof key === 'string' && (!zhKeys.has(key) || !enKeys.has(key))) missing.push(key)
        }
        if (record.children) visit(record.children as typeof routes)
      }
    }
    visit(routes)

    expect(missing).toEqual([])
  })

  it('reference every literal t() key from the catalogs', () => {
    const used = new Set<string>()
    for (const file of walk(root)) {
      const source = readFileSync(file, 'utf8')
      for (const match of source.matchAll(/\$?\bt\(\s*(['"\`])([\w.-]+)\1/g)) {
        if (match[2]) used.add(match[2])
      }
    }

    expect(used.size).toBeGreaterThan(50)
    const missing = [...used].filter((key) => !zhKeys.has(key) || !enKeys.has(key))
    expect(missing).toEqual([])
  })
})
