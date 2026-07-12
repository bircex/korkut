// @ts-check
import { defineConfig } from "astro/config";
import sitemap from "@astrojs/sitemap";
import { SITE } from "./src/consts";

// https://astro.build/config
export default defineConfig({
  // Kanonik site adresi (SEO için önemli); src/consts.ts'teki SITE.url.
  site: SITE.url,
  // Apex alan adında (korkutai.com) site kökte sunulur, alt yol yok.
  base: "/",
  integrations: [sitemap()],
  build: {
    inlineStylesheets: "auto",
  },
});
