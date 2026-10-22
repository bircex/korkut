// @ts-check
import { defineConfig } from "astro/config";
import sitemap from "@astrojs/sitemap";
import { SITE } from "./src/consts";

// https://astro.build/config
export default defineConfig({
  // Kanonik adres ve alt yol, GitHub Pages iş akışından gelir (actions/configure-pages): özel
  // alan adı bağlıyken https://korkutai.com ve "/", bağlı değilken https://bircex.github.io ve
  // "/korkut". Yerelde ve PR kontrollerinde src/consts.ts'teki SITE.url ve kök kullanılır.
  site: process.env.SITE_ORIGIN || SITE.url,
  // Sonda hep "/" olur ("/korkut/"), çünkü sayfalar BASE_URL'e yolu doğrudan ekler.
  base: (process.env.SITE_BASE_PATH ?? "").replace(/\/?$/, "/"),
  integrations: [sitemap()],
  build: {
    inlineStylesheets: "auto",
  },
  vite: {
    // Emit hoisted scripts as external /_astro/*.js files (never inline), so a strict
    // `script-src 'self'` CSP holds with no inline-script hashes (KDM-032).
    build: { assetsInlineLimit: 0 },
  },
});
