import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const apiPort = process.env.TB_API_PORT ?? "5179";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": `http://127.0.0.1:${apiPort}`,
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
