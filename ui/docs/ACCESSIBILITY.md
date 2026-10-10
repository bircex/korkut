# Korkut UI — Accessibility & Responsive Baseline (KDM-029)

These quality gates are enforced by `.github/workflows/migration-ci.yml` on every PR
into `dotnet-migration`, **before** the domain screens are migrated, so the WCAG 2.2 AA
bar holds by construction.

## Gates (merge-blocking)

| Gate | Where | Fails the build when |
| --- | --- | --- |
| Base-branch policy | `ci/lib/check-migration-pr.sh` | a PR targets anything but `dotnet-migration` |
| Secret scan | `ci/lib/secret-scan.sh` | a secret-shaped value is committed |
| Lint / typecheck | `eslint`, `tsc` | lint error or type error |
| Unit + component a11y | Vitest + `vitest-axe` (`src/**/*.test.tsx`) | any axe violation or broken keyboard behaviour on a primitive |
| Build | `vite build` | the bundle fails to build |
| SCA | `npm audit --audit-level=high` | a high/critical dependency CVE |
| E2E a11y + responsive | Playwright + `@axe-core/playwright` (`e2e/`) | a **critical/serious** axe violation, a keyboard blocker, or horizontal overflow |

## Responsive / WCAG matrix

- **Viewports:** desktop (1280), tablet (768), mobile (390) — every e2e spec runs on all three.
- **Reflow (1.4.10):** no horizontal scrolling at 320 CSS px, including at 200% zoom.
- **Keyboard (2.1.1):** all interactive controls reachable and operable by keyboard; dialogs trap focus and restore it; menus use roving focus.
- **Focus visible (2.4.7):** every primitive uses a `focus-visible` ring.
- **Contrast (1.4.3):** enforced by the axe `color-contrast` rule.
- **Reduced motion (2.3.3):** animations are gated behind `motion-reduce:animate-none`.
- **Name/role/value (4.1.2):** labels, live regions and ARIA states asserted in component tests.

## Reusable accessible primitives (`src/a11y/`)

`Button`, `Field` / `TextArea`, `Dialog` / `ConfirmDialog`, `Menu`, `ToastProvider` /
`useToast`, `Spinner` / `LoadingOverlay` / `ErrorState`. Domain screens compose these
rather than hand-rolling markup.

## Running locally

```bash
npm run lint && npm run typecheck && npm run test   # fast gates
npm run test:e2e                                     # Playwright (installs Chromium once)
```
