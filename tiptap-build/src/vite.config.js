import { defineConfig } from 'vite'

export default defineConfig({
    build: {
        lib: {
            entry: './src/editor.js',
            name: 'TiptapBundle',
            fileName: 'tiptap-bundle'
        },
        rollupOptions: {
            output: {
                format: 'iife',   // IMPORTANT: WebView2-compatible
                globals: {}
            }
        }
    }
})
