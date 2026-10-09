import { describe, it, expect } from "vitest";
import { uiStatus, capabilities } from "./status";

// The security-critical invariant (KDM-031 acceptance 1): pending/rejected documents
// can never be previewed. Capabilities mirror the backend's presign gate (KDM-026).

describe("uiStatus", () => {
  it("maps backend status + extraction state to a UI status", () => {
    expect(uiStatus({ status: "quarantine" })).toBe("quarantine");
    expect(uiStatus({ status: "rejected" })).toBe("rejected");
    expect(uiStatus({ status: "tombstoned" })).toBe("tombstoned");
    expect(uiStatus({ status: "clean", extraction_ready: true })).toBe("clean");
    expect(uiStatus({ status: "clean", extraction_ready: false })).toBe("processing");
    expect(uiStatus({ status: "clean", extraction_failed: true })).toBe("failed");
  });
});

describe("capabilities", () => {
  it("never allows previewing a pending or rejected document", () => {
    expect(capabilities("quarantine").canPreview).toBe(false);
    expect(capabilities("rejected").canPreview).toBe(false);
    expect(capabilities("uploading").canPreview).toBe(false);
    expect(capabilities("tombstoned").canPreview).toBe(false);
  });

  it("never allows downloading anything that is not scanned-clean", () => {
    expect(capabilities("quarantine").canDownload).toBe(false);
    expect(capabilities("rejected").canDownload).toBe(false);
    expect(capabilities("clean").canDownload).toBe(true);
    expect(capabilities("processing").canDownload).toBe(true);
  });

  it("blocks link/delete on terminal or in-flight states", () => {
    expect(capabilities("tombstoned").canLink).toBe(false);
    expect(capabilities("rejected").canLink).toBe(false);
    expect(capabilities("uploading").canDelete).toBe(false);
    expect(capabilities("clean").canDelete).toBe(true);
  });
});
