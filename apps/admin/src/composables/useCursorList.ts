import { computed, ref, shallowRef } from 'vue'

import { describeError } from '@/api'
import type { CursorPage } from '@/api/types'

export interface CursorPageRequest {
  limit: number
  cursor: string | null
}

/**
 * Cursor pagination with a client-side stack so "previous page" works without
 * asking the API for offsets (PLAN §9.2 forbids deep offset paging).
 */
export function useCursorList<T>(
  load: (request: CursorPageRequest) => Promise<CursorPage<T>>,
  options: { limit?: number } = {},
) {
  const limit = options.limit ?? 20
  const items = shallowRef<T[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const cursors = ref<Array<string | null>>([null])
  const pageIndex = ref(0)
  const nextCursor = ref<string | null>(null)
  let requestId = 0

  async function loadPage(index: number): Promise<void> {
    const cursor = cursors.value[index] ?? null
    const ticket = requestId + 1
    requestId = ticket
    loading.value = true
    error.value = null
    try {
      const page = await load({ limit, cursor })
      if (ticket !== requestId) return
      items.value = page.items
      nextCursor.value = page.nextCursor
      pageIndex.value = index
    } catch (cause) {
      if (ticket !== requestId) return
      items.value = []
      nextCursor.value = null
      error.value = describeError(cause)
    } finally {
      if (ticket === requestId) loading.value = false
    }
  }

  /** Drop every remembered cursor and load the first page again. */
  async function reloadFresh(): Promise<void> {
    cursors.value = [null]
    await loadPage(0)
  }

  /** Refresh the current page in place. */
  async function reload(): Promise<void> {
    await loadPage(pageIndex.value)
  }

  async function next(): Promise<void> {
    const cursor = nextCursor.value
    if (!cursor) return
    cursors.value = [...cursors.value.slice(0, pageIndex.value + 1), cursor]
    await loadPage(pageIndex.value + 1)
  }

  async function prev(): Promise<void> {
    if (pageIndex.value === 0) return
    await loadPage(pageIndex.value - 1)
  }

  return {
    items,
    loading,
    error,
    pageIndex,
    hasNext: computed(() => Boolean(nextCursor.value)),
    hasPrev: computed(() => pageIndex.value > 0),
    isEmpty: computed(() => !loading.value && error.value === null && items.value.length === 0),
    limit,
    reloadFresh,
    reload,
    next,
    prev,
  }
}
