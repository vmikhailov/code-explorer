import { createRoot } from 'react-dom/client';
import { createApp } from 'vue';
import { Component, bootstrapApplication } from '@angular/core';
import { mount } from 'svelte';
import { defineConfig } from 'vite';

// React
function ReactApp() {
  return null;
}
const container = { id: 'root' };
const root = createRoot(container as any);
root.render(ReactApp as any);

// Vue
const VueApp = {};
createApp(VueApp).mount('#app');

// Angular
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AngularApp {}

bootstrapApplication(AngularApp);

// Svelte
class SvelteApp {}
mount(SvelteApp, { target: '#svelte' });

// Vite
function reactPlugin() { return {}; }
export const config = defineConfig({
  plugins: [reactPlugin()]
});
