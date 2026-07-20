// Security-headers gate (KDM-032). Fails the build if public/_headers is missing a
// required directive, so a release can never ship without the baseline headers. This
// is a fast, dependency-free check that runs before the browser tests.
import { readFileSync } from "node:fs";

const headers = readFileSync("public/_headers", "utf8");

const REQUIRED = [
  "Content-Security-Policy:",
  "Strict-Transport-Security:",
  "X-Frame-Options:",
  "X-Content-Type-Options:",
  "Referrer-Policy:",
  "Permissions-Policy:",
];

// CSP directives that must be present (strict baseline).
const CSP_MUST = ["default-src 'self'", "object-src 'none'", "frame-ancestors 'none'", "script-src 'self'"];

const missing = REQUIRED.filter((h) => !headers.includes(h));
const cspLine = headers.split("\n").find((l) => l.includes("Content-Security-Policy:")) ?? "";
const cspMissing = CSP_MUST.filter((d) => !cspLine.includes(d));

if (missing.length || cspMissing.length) {
  if (missing.length) console.error("check-headers: missing headers:", missing.join(", "));
  if (cspMissing.length) console.error("check-headers: CSP missing directives:", cspMissing.join(", "));
  process.exit(1);
}

// A weak CSP is a fail (belt and suspenders).
if (cspLine.includes("'unsafe-inline'") && cspLine.match(/script-src[^;]*'unsafe-inline'/)) {
  console.error("check-headers: script-src must not allow 'unsafe-inline'.");
  process.exit(1);
}

console.log("check-headers: OK — required security headers present with a strict CSP.");
