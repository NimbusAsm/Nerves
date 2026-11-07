<script setup lang="ts">
withDefaults(defineProps<{ dense?: boolean }>(), { dense: false })
</script>

<template>
  <div class="w-full overflow-x-auto">
    <table class="admin-table" :class="{ 'admin-table--dense': dense }">
      <slot />
    </table>
  </div>
</template>
