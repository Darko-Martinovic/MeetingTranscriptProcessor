import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Only this machine by default; npm run dev -- --host serves other machines too
    port: 5175
  }
})
