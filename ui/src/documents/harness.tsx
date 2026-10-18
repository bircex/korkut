import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "../styles.css";
import { DocumentsPanel } from "./DocumentsPanel";
import { P, PermissionsContext } from "../permissions";

// e2e-only harness: mounts the documents feature in isolation so Playwright can
// exercise upload/preview/link states against stubbed /api/documents without the
// full authenticated workspace. Never included in the production bundle.
const matterId = new URLSearchParams(window.location.search).get("matter") ?? undefined;

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    {/* The harness acts as a member who may do everything with documents. */}
    <PermissionsContext.Provider value={[P.documentsRead, P.documentsWrite, P.documentsDelete]}>
      <DocumentsPanel matterId={matterId} />
    </PermissionsContext.Provider>
  </StrictMode>,
);
