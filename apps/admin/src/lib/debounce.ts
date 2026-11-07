/** Delay a callback; useful for search inputs that trigger a request per keystroke. */
export function debounce(callback: () => void, delay = 250): () => void {
  let timer: ReturnType<typeof setTimeout> | undefined

  return () => {
    if (timer !== undefined) clearTimeout(timer)
    timer = setTimeout(() => {
      timer = undefined
      callback()
    }, delay)
  }
}
