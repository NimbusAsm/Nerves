<script setup lang="ts">
import { ref } from 'vue'
import { RouterView } from 'vue-router'

import SidebarNav from './SidebarNav.vue'
import TopBar from './TopBar.vue'

const mobileOpen = ref(false)
</script>

<template>
  <div class="min-h-screen bg-canvas">
    <SidebarNav :open="mobileOpen" @close="mobileOpen = false" />
    <div
      v-if="mobileOpen"
      class="fixed inset-0 z-30 bg-slate-900/30 lg:hidden"
      aria-hidden="true"
      @click="mobileOpen = false"
    />

    <div class="flex min-h-screen flex-col lg:pl-[248px]">
      <TopBar @open-menu="mobileOpen = true" />
      <main class="mx-auto w-full max-w-[1400px] flex-1 px-4 py-6 sm:px-6">
        <RouterView />
      </main>
      <footer class="px-6 pb-5 text-[11px] text-muted">{{ $t('nav.footer') }}</footer>
    </div>
  </div>
</template>
