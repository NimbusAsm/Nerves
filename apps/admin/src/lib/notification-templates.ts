/**
 * Client-side view of the templates the notification service understands.
 *
 * The server is still the authority: an unknown template or a missing
 * placeholder comes back as HTTP 400. This module exists so the console can
 * render the placeholder inputs and pre-validate before queueing anything.
 */

export interface NotificationTemplateDefinition {
  /** Template code on the wire, e.g. `account.welcome`. */
  code: string
  /** i18n key of the human readable name. */
  labelKey: string
  /** i18n key of the one-line description. */
  descriptionKey: string
  /** Placeholders the template expects, in display order. */
  placeholders: readonly NotificationTemplatePlaceholder[]
}

export interface NotificationTemplatePlaceholder {
  /** Placeholder name without braces, e.g. `displayName`. */
  name: string
  /** i18n key of the field label. */
  labelKey: string
  /** i18n key of the input hint. */
  hintKey: string
  /** A value the form may pre-fill for convenience. */
  example: string
}

export const NOTIFICATION_TEMPLATES: readonly NotificationTemplateDefinition[] = [
  {
    code: 'account.welcome',
    labelKey: 'notifications.templates.welcome',
    descriptionKey: 'notifications.templates.welcomeHint',
    placeholders: [
      { name: 'displayName', labelKey: 'notifications.placeholders.displayName', hintKey: 'notifications.placeholders.displayNameHint', example: '张伟' },
      { name: 'username', labelKey: 'notifications.placeholders.username', hintKey: 'notifications.placeholders.usernameHint', example: 'zhang.wei' },
      { name: 'issuer', labelKey: 'notifications.placeholders.issuer', hintKey: 'notifications.placeholders.issuerHint', example: 'https://nerves.example.com/' },
    ],
  },
  {
    code: 'account.password-reset',
    labelKey: 'notifications.templates.passwordReset',
    descriptionKey: 'notifications.templates.passwordResetHint',
    placeholders: [
      { name: 'displayName', labelKey: 'notifications.placeholders.displayName', hintKey: 'notifications.placeholders.displayNameHint', example: '张伟' },
      { name: 'minutes', labelKey: 'notifications.placeholders.minutes', hintKey: 'notifications.placeholders.minutesHint', example: '30' },
      { name: 'resetUrl', labelKey: 'notifications.placeholders.resetUrl', hintKey: 'notifications.placeholders.resetUrlHint', example: 'https://nerves.example.com/reset?token=…' },
    ],
  },
  {
    code: 'security.sign-in',
    labelKey: 'notifications.templates.signIn',
    descriptionKey: 'notifications.templates.signInHint',
    placeholders: [
      { name: 'displayName', labelKey: 'notifications.placeholders.displayName', hintKey: 'notifications.placeholders.displayNameHint', example: '张伟' },
      { name: 'device', labelKey: 'notifications.placeholders.device', hintKey: 'notifications.placeholders.deviceHint', example: 'Chrome 141 · macOS' },
      { name: 'occurredAt', labelKey: 'notifications.placeholders.occurredAt', hintKey: 'notifications.placeholders.occurredAtHint', example: '2026-11-10 18:30' },
    ],
  },
]

export function findNotificationTemplate(code: string): NotificationTemplateDefinition | undefined {
  return NOTIFICATION_TEMPLATES.find((template) => template.code === code)
}

/** Placeholder names a template requires, in order. */
export function templatePlaceholders(code: string): string[] {
  return findNotificationTemplate(code)?.placeholders.map((placeholder) => placeholder.name) ?? []
}

/**
 * Placeholder names that are still empty. The server rejects those requests;
 * checking locally turns a round trip into an immediate inline hint.
 */
export function missingTemplateValues(
  code: string,
  values: Readonly<Record<string, string>>,
): string[] {
  return templatePlaceholders(code).filter((name) => !(values[name] ?? '').trim())
}

/**
 * Trim values and drop empty ones so the request body only carries what the
 * operator actually filled in.
 */
export function normalizeTemplateValues(
  values: Readonly<Record<string, string>>,
): Record<string, string> {
  const normalized: Record<string, string> = {}
  for (const [key, value] of Object.entries(values)) {
    const trimmed = value.trim()
    if (trimmed) normalized[key] = trimmed
  }
  return normalized
}

/** Placeholder tokens found in a rendered subject, used by tests and previews. */
export function extractPlaceholders(text: string): string[] {
  const names: string[] = []
  const seen = new Set<string>()
  for (const match of text.matchAll(/\{\{\s*([\w.-]+)\s*\}\}/g)) {
    const name = match[1]
    if (name && !seen.has(name)) {
      seen.add(name)
      names.push(name)
    }
  }
  return names
}
