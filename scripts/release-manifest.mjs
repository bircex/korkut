// Build-once release manifest (KDM-032). After `astro build`, records a hash of every
// file in dist/ so the release pipeline (KDM-035) can promote the exact artifact it
// tested — build once, deploy the same bytes. Emits dist/release-manifest.json.
import { createHash } from "node:crypto";
import { readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";

const DIST = "dist";

function walk(dir) {
  const out = [];
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    if (statSync(p).isDirectory()) out.push(...walk(p));
    else out.push(p);
  }
  return out;
}

const files = walk(DIST)
  .filter((f) => relative(DIST, f) !== "release-manifest.json")
  .sort()
  .map((f) => {
    const bytes = readFileSync(f);
    return {
      path: relative(DIST, f),
      bytes: bytes.length,
      sha256: createHash("sha256").update(bytes).digest("hex"),
    };
  });

// A single digest over the per-file hashes uniquely identifies this build.
const digest = createHash("sha256").update(files.map((f) => `${f.path}:${f.sha256}`).join("\n")).digest("hex");

const manifest = {
  artifact: "korkut-website",
  fileCount: files.length,
  totalBytes: files.reduce((n, f) => n + f.bytes, 0),
  digest,
  files,
};

writeFileSync(join(DIST, "release-manifest.json"), JSON.stringify(manifest, null, 2));
console.log(`release-manifest: ${files.length} files, digest ${digest.slice(0, 16)}…`);
