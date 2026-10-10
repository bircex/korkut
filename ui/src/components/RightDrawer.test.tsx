import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { RightDrawer } from "./RightDrawer";

describe("RightDrawer", () => {
  it("kapalıyken hiçbir şey render etmez", () => {
    const { container } = render(
      <RightDrawer open={false} onClose={() => {}} label="Test panel">
        <p>içerik</p>
      </RightDrawer>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("açıkken dialog semantiğiyle tam ekran (sol menü hariç) açılır", () => {
    render(
      <RightDrawer open onClose={() => {}} label="İlgili Kararlar" title="İlgili Kararlar">
        <p>karar listesi</p>
      </RightDrawer>,
    );
    const dialog = screen.getByRole("dialog", { name: "İlgili Kararlar" });
    expect(dialog).toBeInTheDocument();
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(screen.getByText("karar listesi")).toBeVisible();
  });

  it("açılışta focus içeri taşınır", () => {
    render(
      <RightDrawer open onClose={() => {}} label="Panel" title="Panel">
        <button>içerik düğmesi</button>
      </RightDrawer>,
    );
    const dialog = screen.getByRole("dialog", { name: "Panel" });
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it("Esc kapatır", () => {
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Panel" title="Panel">
        <p>içerik</p>
      </RightDrawer>,
    );
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("başlık verilince header ve kapat düğmesi çizilir; kapat onClose çağırır", () => {
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Panel" title="Panel">
        <p>içerik</p>
      </RightDrawer>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Kapat" }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("başlık verilmeyince chrome'suzdur — içerik kendi header'ını getirir", () => {
    render(
      <RightDrawer open onClose={() => {}} label="Belge editörü">
        <p>editör</p>
      </RightDrawer>,
    );
    expect(screen.queryByRole("button", { name: "Kapat" })).not.toBeInTheDocument();
  });
});
