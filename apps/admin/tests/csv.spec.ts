import { describe, expect, it } from 'vitest'

import { csvFilename, escapeCell, toCsv } from '@/lib/csv'

describe('csv', () => {
  it('escapes separators, quotes and newlines', () => {
    expect(escapeCell('plain')).toBe('plain')
    expect(escapeCell('a,b')).toBe('"a,b"')
    expect(escapeCell('say "hi"')).toBe('"say ""hi"""')
    expect(escapeCell('line1\nline2')).toBe('"line1\nline2"')
    expect(escapeCell(null)).toBe('')
    expect(escapeCell(undefined)).toBe('')
    expect(escapeCell(0)).toBe('0')
  })

  it('adds a BOM by default so Excel reads UTF-8', () => {
    const csv = toCsv([['时间', '动作'], ['2026-11-07', '登录成功']])
    expect(csv.startsWith('\uFEFF')).toBe(true)
    expect(csv).toContain('时间,动作')
    expect(toCsv([['a']], { bom: false })).toBe('a')
  })

  it('builds a stable filename', () => {
    expect(csvFilename('nerves-audit', new Date(2026, 10, 7, 20, 5))).toBe('nerves-audit-20261107-2005.csv')
  })
})
