import { describe, expect, it, vi, afterEach } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { RightDrawer } from "./RightDrawer";

// jsdom'da viewport'u ayarla — RightDrawer moda innerWidth üzerinden karar verir.
function setViewport(width: number) {
  Object.defineProperty(window, "innerWidth", { configurable: true, value: width });
  window.dispatchEvent(new Event("resize"));
}

afterEach(() => act(() => setViewport(1024)));

describe("RightDrawer", () => {
  it("kapalıyken hiçbir şey render etmez", () => {
    setViewport(1400);
    const { container } = render(
      <RightDrawer open={false} onClose={() => {}} label="Test panel">
        <p>içerik</p>
      </RightDrawer>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("geniş ekranda push modunda complementary panel olarak açılır", () => {
    setViewport(1400);
    render(
      <RightDrawer open onClose={() => {}} label="İlgili Kararlar" title="İlgili Kararlar" overlayBelow={1200}>
        <p>karar listesi</p>
      </RightDrawer>,
    );
    const panel = screen.getByRole("complementary", { name: "İlgili Kararlar" });
    expect(panel).toBeInTheDocument();
    expect(screen.getByText("karar listesi")).toBeVisible();
    // Push modu dialog değildir — arka plan kilitlenmez, focus hapsedilmez.
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("push modunda Esc drawer'ı KAPATMAZ (içerik Esc'i kendi kullanır, ör. CKEditor balonları)", () => {
    setViewport(1400);
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Panel" title="Panel" overlayBelow={1200}>
        <p>içerik</p>
      </RightDrawer>,
    );
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).not.toHaveBeenCalled();
  });

  it("başlık verilince header ve kapat düğmesi çizilir; kapat onClose çağırır", () => {
    setViewport(1400);
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Panel" title="Panel" overlayBelow={1200}>
        <p>içerik</p>
      </RightDrawer>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Kapat" }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("başlık verilmeyince chrome'suzdur — içerik kendi header'ını getirir", () => {
    setViewport(1400);
    render(
      <RightDrawer open onClose={() => {}} label="Belge editörü" overlayBelow={1200}>
        <p>editör</p>
      </RightDrawer>,
    );
    expect(screen.queryByRole("button", { name: "Kapat" })).not.toBeInTheDocument();
  });

  it("dar ekranda overlay (dialog) modunda açılır ve Esc kapatır", () => {
    setViewport(800);
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Belge editörü" overlayBelow={1200}>
        <button>içerik düğmesi</button>
      </RightDrawer>,
    );
    const dialog = screen.getByRole("dialog", { name: "Belge editörü" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    // Açılışta focus içeri taşınır.
    expect(dialog.contains(document.activeElement)).toBe(true);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("overlay modunda backdrop tıklaması kapatır, panel içi tıklama kapatmaz", () => {
    setViewport(800);
    const onClose = vi.fn();
    render(
      <RightDrawer open onClose={onClose} label="Panel" title="Panel" overlayBelow={1200}>
        <p>içerik</p>
      </RightDrawer>,
    );
    fireEvent.mouseDown(screen.getByText("içerik"));
    expect(onClose).not.toHaveBeenCalled();
    fireEvent.mouseDown(screen.getByRole("dialog").parentElement!);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("viewport daralınca push'tan overlay'e geçer", () => {
    setViewport(1400);
    render(
      <RightDrawer open onClose={() => {}} label="Panel" title="Panel" overlayBelow={1200}>
        <p>içerik</p>
      </RightDrawer>,
    );
    expect(screen.getByRole("complementary")).toBeInTheDocument();
    act(() => setViewport(900));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.queryByRole("complementary")).not.toBeInTheDocument();
  });
});
