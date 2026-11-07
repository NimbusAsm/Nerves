const dateTimeFormatter = new Intl.DateTimeFormat('zh-CN', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const dateFormatter = new Intl.DateTimeFormat('zh-CN', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

const numberFormatter = new Intl.NumberFormat('zh-CN')

const DAY_MS = 24 * 60 * 60 * 1000

/** Format an ISO instant as `YYYY-MM-DD HH:mm` in the viewer's time zone. */
export function formatDateTime(value: string | null | undefined): string {
  const date = toDate(value)
  if (!date) return '—'
  return dateTimeFormatter.format(date).replaceAll('/', '-')
}

/** Format an ISO instant as `YYYY-MM-DD`. */
export function formatDate(value: string | null | undefined): string {
  const date = toDate(value)
  if (!date) return '—'
  return dateFormatter.format(date).replaceAll('/', '-')
}

/** Human readable "3 分钟前" style timestamp. */
export function formatRelative(value: string | null | undefined, now = Date.now()): string {
  const date = toDate(value)
  if (!date) return '—'

  const diff = now - date.getTime()
  if (diff < 0) return '刚刚'
  if (diff < 60_000) return '刚刚'

  const minutes = Math.floor(diff / 60_000)
  if (minutes < 60) return `${minutes} 分钟前`

  const hours = Math.floor(diff / 3_600_000)
  if (hours < 24) return `${hours} 小时前`

  const days = Math.floor(diff / DAY_MS)
  if (days < 30) return `${days} 天前`

  return formatDate(date.toISOString())
}

/** Thousands separators for dashboard counters. */
export function formatNumber(value: number): string {
  return numberFormatter.format(value)
}

/** `0.417` -> `41.7%`, guarding against a zero denominator. */
export function formatPercent(part: number, total: number, digits = 1): string {
  if (total <= 0) return '0%'
  return `${((part / total) * 100).toFixed(digits)}%`
}

/** Local-time `YYYY-MM-DD` key, used by date filters. */
export function localDateKey(value: string | Date = new Date()): string {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

/** `OpenAuth` / `张伟` -> avatar initials. */
export function initials(name: string): string {
  const trimmed = name.trim()
  if (!trimmed) return '?'
  if (/[\u4e00-\u9fa5]/.test(trimmed)) return trimmed.slice(0, 2)
  const words = trimmed.split(/[\s._-]+/).filter(Boolean)
  const first = words[0]
  if (!first) return '?'
  if (words.length === 1) return first.slice(0, 2).toUpperCase()
  return `${first[0] ?? ''}${words[1]?.[0] ?? ''}`.toUpperCase()
}

/** Only a digest is ever persisted for a real-name document; shorten it for display. */
export function maskDigest(digest: string, head = 12, tail = 4): string {
  if (digest.length <= head + tail) return digest
  return `${digest.slice(0, head)}…${digest.slice(-tail)}`
}

/** Normalise any date-like input, returning `null` for missing/invalid values. */
export function toDate(value: string | null | undefined): Date | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}
