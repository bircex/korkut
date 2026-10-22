# Korkut — Landing Page

The marketing/landing page, built with [Astro](https://astro.build) and shipped
as a static site. The page content is in Turkish; the theme is black-and-white
(logos are the only color) and SEO-optimized.

## Development

```bash
cd website
npm install
npm run dev        # http://localhost:4321
npm run build      # static output in dist/
npm run preview    # preview the built output
```

## Structure

| Path | What it is |
|---|---|
| `src/consts.ts` | Site URL, title, description, links, FAQ — single source of content. |
| `src/layouts/Layout.astro` | `<head>`, meta tags, Open Graph/Twitter, JSON-LD. |
| `src/components/` | Sections: `Nav`, `Hero`, `Features`, `Integrations`, `Models`, `DataSources`, `Security`, `HowItWorks`, `Install`, `OpenSource`, `FAQ`, `CTA`, `Footer`. |
| `src/pages/index.astro` | Single page composing the sections. |
| `public/` | `korkut-logo.svg`, `og.png`/`og.svg`, `robots.txt`, and `logos/` (provider + institution logos). |

## SEO

- Canonical URL, `robots`, keywords, `lang="tr"`
- Open Graph + Twitter Card (`og.png`, 1200×630)
- `schema.org` JSON-LD (`SoftwareApplication` + `WebSite` + `FAQPage`)
- Auto-generated `sitemap-index.xml` via `@astrojs/sitemap`, plus `robots.txt`

> **Before going live:** update `SITE.url` in `src/consts.ts` and the `Sitemap:`
> line in `public/robots.txt` with your real domain. If you change the OG image,
> edit `public/og.svg` and re-rasterize it to PNG (e.g. with the `sharp` bundled
> by Astro).

## Deployment

Every push to `main` that touches `website/` builds the site and publishes it to GitHub Pages
(`.github/workflows/pages.yml`); the Release workflow also redeploys it. The custom domain
`korkutai.com` is set in the repository's Settings > Pages, with its DNS at Cloudflare:

| Type | Name | Value |
|---|---|---|
| A | `@` | `185.199.108.153`, `185.199.109.153`, `185.199.110.153`, `185.199.111.153` |
| AAAA | `@` | `2606:50c0:8000::153`, `2606:50c0:8001::153`, `2606:50c0:8002::153`, `2606:50c0:8003::153` |
| CNAME | `www` | `bircex.github.io` |

Keep the records **DNS only** (grey cloud) so GitHub can issue and renew the certificate.
