// @ts-check
import { defineConfig } from "astro/config";
import sitemap from "@astrojs/sitemap";
import { SITE } from "./src/consts";

// https://astro.build/config
export default defineConfig({
  // Kanonik site adresi. Kendi alan adınızla değiştirin (SEO için önemli).
  site: SITE.url,
  integrations: [sitemap()],
  build: {
    inlineStylesheets: "auto",
  },
});
