import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "../styles.css";
import { DocumentsPanel } from "./DocumentsPanel";

// e2e-only harness: mounts the documents feature in isolation so Playwright can
// exercise upload/preview/link states against stubbed /api/documents without the
// full authenticated workspace. Never included in the production bundle.
const matterId = new URLSearchParams(window.location.search).get("matter") ?? undefined;

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <DocumentsPanel matterId={matterId} />
  </StrictMode>,
);
