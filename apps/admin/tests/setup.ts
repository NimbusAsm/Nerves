// jsdom does not implement these browser APIs that vue-router and the console use.
if (typeof window !== 'undefined') {
  Object.defineProperty(window, 'scrollTo', { value: () => {}, writable: true })
}
