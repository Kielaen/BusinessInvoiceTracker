import { ref } from 'vue'
import { defineStore } from 'pinia'

export const useWorkspaceStore = defineStore('workspace', () => {
  const selectedBusinessId = ref<string | null>(null)

  function selectBusiness(businessId: string | null) {
    selectedBusinessId.value = businessId
  }

  return { selectedBusinessId, selectBusiness }
})
