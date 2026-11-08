/**
 * Access tokens live in memory only: no localStorage, no cookies for the API.
 * A page reload requires a fresh sign-in (the IdP session may still be alive,
 * so the redirect round trip is usually invisible).
 */
let accessToken: string | null = null

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token
}
