# Korkut — Landing Page

[Astro](https://astro.build) ile yazılmış, statik olarak derlenen tanıtım
sayfası. Tamamen Türkçe, beyaz temalı ve SEO uyumludur.

## Geliştirme

```bash
cd landing
npm install
npm run dev        # http://localhost:4321
npm run build      # dist/ altına statik çıktı
npm run preview    # derlenmiş çıktıyı önizle
```

## Yapı

| Yol | Açıklama |
|---|---|
| `src/consts.ts` | Site adresi, başlık, açıklama, bağlantılar — **tek yerden içerik.** |
| `src/layouts/Layout.astro` | `<head>`, meta etiketleri, Open Graph/Twitter, JSON-LD. |
| `src/components/` | Bölümler: `Nav`, `Hero`, `Features`, `Security`, `HowItWorks`, `Sources`, `OpenSource`, `CTA`, `Footer`. |
| `src/pages/index.astro` | Bölümleri birleştiren tek sayfa. |
| `public/` | `korkut-logo.svg`, `og.png`/`og.svg`, `robots.txt`. |

## SEO

- Kanonik URL, `robots`, anahtar kelimeler, `hreflang`/`lang="tr"`
- Open Graph + Twitter Card (`og.png`, 1200×630)
- `schema.org` JSON-LD (`SoftwareApplication` + `WebSite`)
- `@astrojs/sitemap` ile otomatik `sitemap-index.xml`, `robots.txt`

> **Yayına almadan önce:** `src/consts.ts` içindeki `SITE.url` ve
> `public/robots.txt` içindeki `Sitemap:` satırını gerçek alan adınızla
> güncelleyin. OG görselini değiştirirseniz `public/og.svg`'yi düzenleyip
> `npx astro` ortamındaki `sharp` ile yeniden PNG'ye çevirin.

## Dağıtım

Statik çıktıdır; `dist/` klasörünü herhangi bir statik barındırmaya
(GitHub Pages, Netlify, Vercel, Cloudflare Pages, Nginx) koyabilirsiniz.
