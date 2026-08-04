import { ref, watch, type Ref } from 'vue'

export function useDebouncedRef(source: Ref<string>, delay = 300) {
  const debounced = ref(source.value)
  watch(source, (value, _, onCleanup) => {
    const timer = window.setTimeout(() => {
      debounced.value = value.trim()
    }, delay)
    onCleanup(() => window.clearTimeout(timer))
  })
  return debounced
}
