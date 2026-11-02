import { resolve } from 'path';
import { cpSync, existsSync, readdirSync } from 'node:fs';
import { defineConfig } from 'vite';

const clientRoot = __dirname;
const htmlPages = [resolve(clientRoot, 'index.html'), ...readdirSync(clientRoot, { withFileTypes: true })
  .filter(entry => entry.isDirectory() && /^[A-Z]/.test(entry.name))
  .flatMap(entry => readdirSync(resolve(clientRoot, entry.name))
    .filter(file => file.endsWith('.html')).map(file => resolve(clientRoot, entry.name, file)))];

let clientOutput;
export default defineConfig({
  plugins: [{
    name: 'copy-shared-client-scripts',
    apply: 'build',
    configResolved(config) { clientOutput = resolve(config.root, config.build.outDir); },
    closeBundle() {
      // Classic scripts are shared globals; Vite only bundles module scripts.
      for (const directory of ['js', 'messages', 'css']) {
        const source = resolve(clientRoot, directory);
        if (existsSync(source)) cpSync(source, resolve(clientOutput, directory), { recursive: true });
      }
    },
  }],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
      '/media': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
    },
  },
  build: {
    rollupOptions: {
      input: htmlPages,
    },
  },
});
