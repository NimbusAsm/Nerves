import { defineStore } from 'pinia'
import { ref } from 'vue'

export type ToastTone = 'success' | 'error' | 'info'

export interface ToastMessage {
  id: number
  tone: ToastTone
  title: string
  description?: string
}

export const useToastStore = defineStore('toast', () => {
  const items = ref<ToastMessage[]>([])
  const timers = new Map<number, ReturnType<typeof setTimeout>>()
  let nextId = 1

  function dismiss(id: number): void {
    const timer = timers.get(id)
    if (timer) clearTimeout(timer)
    timers.delete(id)
    items.value = items.value.filter((item) => item.id !== id)
  }

  function push(tone: ToastTone, title: string, description?: string, ttl = 4500): number {
    const id = nextId
    nextId += 1
    items.value = [...items.value, { id, tone, title, description }]
    timers.set(
      id,
      setTimeout(() => dismiss(id), ttl),
    )
    return id
  }

  const success = (title: string, description?: string) => push('success', title, description)
  const error = (title: string, description?: string) => push('error', title, description)
  const info = (title: string, description?: string) => push('info', title, description)

  return { items, push, dismiss, success, error, info }
})
