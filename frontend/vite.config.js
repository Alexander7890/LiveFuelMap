import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, "..", "");
  const backend = env.VITE_API_BASE_URL || "http://localhost:5000";

  return {
    envDir: "..",
    plugins: [react()],
    server: {
      proxy: {
        "/api": {
          target: backend,
          changeOrigin: true
        },
        "/hubs": {
          target: backend,
          changeOrigin: true,
          ws: true
        },
        "/uploads": {
          target: backend,
          changeOrigin: true
        }
      }
    },
    build: {
      outDir: "dist",
      emptyOutDir: true
    }
  };
});
