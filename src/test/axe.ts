import { axe } from "vitest-axe";

// Component-scope axe run (KDM-029). Page-level landmark/heading rules (region,
// landmark-one-main, page-has-heading-one) don't apply to an isolated component
// rendered without its page shell — those are verified end-to-end by the Playwright
// axe scan against the full app. Everything else (labels, roles, contrast, ARIA
// validity) is asserted here.
const PAGE_LEVEL_RULES = {
  region: { enabled: false },
  "landmark-one-main": { enabled: false },
  "page-has-heading-one": { enabled: false },
} as const;

export function axeComponent(node: Element | Document) {
  return axe(node, { rules: PAGE_LEVEL_RULES });
}
