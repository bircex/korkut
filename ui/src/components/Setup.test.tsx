import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { Setup } from "./Setup";
import { api, ApiError } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, api: { ...mod.api, setup: vi.fn(), login: vi.fn() } };
});

function fill(password = "kurulum-parolasi-2026", confirm = password) {
  fireEvent.change(screen.getByLabelText(/Büro adı/), { target: { value: " Deneme Hukuk Bürosu " } });
  fireEvent.change(screen.getByLabelText(/Ad Soyad/), { target: { value: "Av. Deniz Yılmaz" } });
  fireEvent.change(screen.getByLabelText(/E-posta/), { target: { value: "sahip@ornek.av.tr" } });
  fireEvent.change(screen.getByLabelText(/Parola \(en az/), { target: { value: password } });
  fireEvent.change(screen.getByLabelText(/Parola \(tekrar\)/), { target: { value: confirm } });
}

describe("Setup", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("creates the office, signs the owner in, then hands back", async () => {
    vi.mocked(api.setup).mockResolvedValueOnce({ office_id: "o1" });
    vi.mocked(api.login).mockResolvedValueOnce({ totp_required: false });
    const onDone = vi.fn();
    render(<Setup onDone={onDone} />);

    fill();
    fireEvent.click(screen.getByRole("button", { name: "Kurulumu tamamla" }));

    await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
    expect(api.setup).toHaveBeenCalledWith("Deneme Hukuk Bürosu", "Av. Deniz Yılmaz", "sahip@ornek.av.tr", "kurulum-parolasi-2026");
    expect(api.login).toHaveBeenCalledWith("sahip@ornek.av.tr", "kurulum-parolasi-2026");
  });

  it("does not submit when the passwords differ", async () => {
    render(<Setup onDone={vi.fn()} />);

    fill("kurulum-parolasi-2026", "baska-parola-2026");
    fireEvent.click(screen.getByRole("button", { name: "Kurulumu tamamla" }));

    expect(await screen.findByText("Parolalar eşleşmiyor")).toBeInTheDocument();
    expect(api.setup).not.toHaveBeenCalled();
  });

  it("falls back to sign-in when someone else finished setup first", async () => {
    vi.mocked(api.setup).mockRejectedValueOnce(new ApiError(409, "Bu kurulum zaten tamamlanmış. Giriş yapın."));
    const onDone = vi.fn();
    render(<Setup onDone={onDone} />);

    fill();
    fireEvent.click(screen.getByRole("button", { name: "Kurulumu tamamla" }));

    await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
    expect(api.login).not.toHaveBeenCalled();
  });

  it("shows the server's message when setup is rejected", async () => {
    vi.mocked(api.setup).mockRejectedValueOnce(new ApiError(422, "Geçerli bir e-posta girin."));
    render(<Setup onDone={vi.fn()} />);

    fill();
    fireEvent.click(screen.getByRole("button", { name: "Kurulumu tamamla" }));

    expect(await screen.findByText("Geçerli bir e-posta girin.")).toBeInTheDocument();
  });

  it("has no accessibility violations", async () => {
    render(<Setup onDone={vi.fn()} />);

    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
