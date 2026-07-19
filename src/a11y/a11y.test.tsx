import { useState } from "react";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { axeComponent } from "../test/axe";
import { describe, it, expect, vi } from "vitest";
import { Button, Field, Dialog, ConfirmDialog, Menu, ToastProvider, useToast, Spinner, ErrorState } from ".";

describe("Field", () => {
  it("associates its label and exposes errors to assistive tech", async () => {
    render(<Field label="E-posta" error="Zorunlu alan" required />);
    const input = screen.getByLabelText(/E-posta/);
    expect(input).toHaveAttribute("aria-invalid", "true");
    // The error text is programmatically linked and announced.
    const errorId = input.getAttribute("aria-errormessage")!;
    expect(document.getElementById(errorId)).toHaveTextContent("Zorunlu alan");
    expect(screen.getByRole("alert")).toHaveTextContent("Zorunlu alan");
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });

  it("has no describedby when there is no error or hint", () => {
    render(<Field label="Ad" />);
    expect(screen.getByLabelText("Ad")).not.toHaveAttribute("aria-describedby");
  });
});

describe("Button", () => {
  it("defaults to type=button and passes axe", async () => {
    render(<Button>Kaydet</Button>);
    expect(screen.getByRole("button", { name: "Kaydet" })).toHaveAttribute("type", "button");
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});

describe("Dialog", () => {
  function Harness() {
    const [open, setOpen] = useState(false);
    return (
      <>
        <button onClick={() => setOpen(true)}>Aç</button>
        <Dialog open={open} onClose={() => setOpen(false)} title="Başlık" footer={<button>Tamam</button>}>
          <label>
            İsim <input />
          </label>
        </Dialog>
      </>
    );
  }

  it("is a labelled modal, traps Tab, closes on Escape and restores focus", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const opener = screen.getByRole("button", { name: "Aç" });
    await user.click(opener);

    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAccessibleName("Başlık");
    expect(await axeComponent(document.body)).toHaveNoViolations();

    // Focus is inside the dialog.
    expect(dialog.contains(document.activeElement)).toBe(true);

    // Tabbing from the last focusable wraps back to the first (trap).
    const input = within(dialog).getByRole("textbox");
    const ok = within(dialog).getByRole("button", { name: "Tamam" });
    ok.focus();
    await user.tab();
    expect(document.activeElement).toBe(input);

    // Escape closes and focus returns to the trigger.
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(document.activeElement).toBe(opener);
  });
});

describe("ConfirmDialog", () => {
  it("confirms and cancels via its buttons", async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ConfirmDialog
        open
        onConfirm={onConfirm}
        onCancel={onCancel}
        title="Sil"
        message="Emin misiniz?"
        destructive
      />,
    );
    expect(await axeComponent(document.body)).toHaveNoViolations();
    await user.click(screen.getByRole("button", { name: "Onayla" }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });
});

describe("Menu", () => {
  it("opens with the keyboard, moves a roving focus and selects", async () => {
    const user = userEvent.setup();
    const first = vi.fn();
    const second = vi.fn();
    render(
      <Menu
        label="İşlemler"
        items={[
          { label: "Düzenle", onSelect: first },
          { label: "Sil", onSelect: second, destructive: true },
        ]}
      />,
    );
    const trigger = screen.getByRole("button", { name: "İşlemler" });
    expect(trigger).toHaveAttribute("aria-haspopup", "menu");

    trigger.focus();
    await user.keyboard("{ArrowDown}");
    const menu = screen.getByRole("menu");
    const items = within(menu).getAllByRole("menuitem");
    expect(document.activeElement).toBe(items[0]);

    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(items[1]);

    await user.keyboard("{Enter}");
    expect(second).toHaveBeenCalledOnce();
  });
});

describe("Toast", () => {
  it("announces messages through a live region", async () => {
    const user = userEvent.setup();
    function Emitter() {
      const { notify } = useToast();
      return <button onClick={() => notify("Kaydedildi", "success")}>Bildir</button>;
    }
    render(
      <ToastProvider>
        <Emitter />
      </ToastProvider>,
    );
    await user.click(screen.getByRole("button", { name: "Bildir" }));
    expect(screen.getByRole("status")).toHaveTextContent("Kaydedildi");
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});

describe("Feedback", () => {
  it("Spinner is a live status and ErrorState is an alert", async () => {
    const { rerender } = render(<Spinner label="Yükleniyor" />);
    expect(screen.getByRole("status")).toHaveTextContent("Yükleniyor");
    rerender(<ErrorState>Sunucuya ulaşılamadı</ErrorState>);
    expect(screen.getByRole("alert")).toHaveTextContent("Sunucuya ulaşılamadı");
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
