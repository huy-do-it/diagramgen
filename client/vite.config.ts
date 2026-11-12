import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// Chưa có Gateway: proxy thẳng theo tiền tố route (TASKS §1.1) tới port host của từng service.
const directTargets: Record<string, number> = {
  '/api/auth': 5001,
  '/api/users': 5001,
  '/api/admin/identity': 5001,
  '/api/projects': 5002,
  '/api/invitations': 5002,
  '/api/directory': 5002,
  '/api/admin/workspace': 5002,
  '/api/diagrams': 5003,
  '/api/reviews': 5003,
  '/api/admin/diagram': 5003,
  '/api/render': 5004,
  '/api/admin/render': 5004,
  '/api/notifications': 5005,
  '/api/admin/notification': 5005,
};

const localServer = { host: '127.0.0.1', port: 5173, strictPort: true };

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '');
  // Có Gateway: VITE_USE_GATEWAY=true → mọi /api về Gateway :5000
  const proxy = env.VITE_USE_GATEWAY === 'true'
    ? { '/api': 'http://localhost:5000' }
    : Object.fromEntries(Object.entries(directTargets).map(([prefix, port]) => [prefix, `http://localhost:${port}`]));

  return {
    plugins: [react()],
    server: { ...localServer, proxy },
    preview: { ...localServer, proxy },
  };
});
