import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [tailwindcss(), sveltekit()],
	server: {
		port: 5173,
		strictPort: true,
		proxy: {
			'/api/v1': {
				target: 'http://localhost:8090',
				changeOrigin: true
			},
			'/v1': {
				target: 'http://localhost:8080',
				changeOrigin: true
			},
			'/healthz': {
				target: 'http://localhost:8080',
				changeOrigin: true
			}
		}
	}
});
