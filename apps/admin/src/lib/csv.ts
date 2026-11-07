export type CsvValue = string | number | boolean | null | undefined

export interface CsvOptions {
  /** Excel on Windows needs the BOM to detect UTF-8; keep it unless you know better. */
  bom?: boolean
}

/** Render rows as RFC 4180 CSV. */
export function toCsv(rows: readonly (readonly CsvValue[])[], options: CsvOptions = {}): string {
  const separator = ','
  const body = rows.map((row) => row.map(escapeCell).join(separator)).join('\r\n')
  return options.bom === false ? body : `\uFEFF${body}`
}

/** Quote a cell when it contains separators, quotes or line breaks. */
export function escapeCell(value: CsvValue): string {
  if (value === null || value === undefined) return ''
  const text = String(value)
  if (/[",\r\n]/.test(text) || /^\s|\s$/.test(text)) {
    return `"${text.replaceAll('"', '""')}"`
  }
  return text
}

/** `nerves-audit-20261107-2015.csv` */
export function csvFilename(prefix: string, at = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, '0')
  const stamp = `${at.getFullYear()}${pad(at.getMonth() + 1)}${pad(at.getDate())}-${pad(at.getHours())}${pad(at.getMinutes())}`
  return `${prefix}-${stamp}.csv`
}

/** Trigger a browser download. No-op outside the browser (e.g. unit tests). */
export function downloadCsv(filename: string, content: string): void {
  if (typeof document === 'undefined' || typeof URL.createObjectURL !== 'function') return

  const blob = new Blob([content], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.rel = 'noopener'
  document.body.append(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}
