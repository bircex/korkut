import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { ChangePassword } from "./Settings";
import { api, ApiError } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, api: { ...mod.api, changePassword: vi.fn() } };
});

function fill(current: string, next: string, confirm = next) {
  fireEvent.change(screen.getByLabelText(/Mevcut parola/), { target: { value: current } });
  fireEvent.change(screen.getByLabelText(/^Yeni parola\b(?! \(tekrar\))/), { target: { value: next } });
  fireEvent.change(screen.getByLabelText(/Yeni parola \(tekrar\)/), { target: { value: confirm } });
}

describe("ChangePassword", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("changes the password and says the other sessions ended", async () => {
    vi.mocked(api.changePassword).mockResolvedValueOnce(undefined);
    render(<ChangePassword />);

    fill("kurulum-parolasi", "yeni-guclu-parola-1");
    fireEvent.click(screen.getByRole("button", { name: "Parolayı değiştir" }));

    await waitFor(() => expect(api.changePassword).toHaveBeenCalledWith("kurulum-parolasi", "yeni-guclu-parola-1"));
    expect(await screen.findByRole("status")).toHaveTextContent("Diğer cihazlardaki oturumlar kapatıldı");
  });

  it("will not send two different new passwords", () => {
    render(<ChangePassword />);

    fill("kurulum-parolasi", "yeni-guclu-parola-1", "yeni-guclu-parola-2");

    expect(screen.getByText("Parolalar aynı değil.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Parolayı değiştir" })).toBeDisabled();
  });

  it("shows why the change was refused", async () => {
    vi.mocked(api.changePassword).mockRejectedValueOnce(new ApiError(400, "Mevcut parola yanlış."));
    render(<ChangePassword />);

    fill("yanlis-parola", "yeni-guclu-parola-1");
    fireEvent.click(screen.getByRole("button", { name: "Parolayı değiştir" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Mevcut parola yanlış.");
  });

  it("has no accessibility violations", async () => {
    render(<ChangePassword />);

    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});

describe("Settings tabs", () => {
  const base = { username: "u", email: "u@ornek.av.tr", full_name: "Üye", bar_registry_no: null, office: "Büro" };

  it("shows a reader only what a reader may use", async () => {
    const { Settings } = await import("./Settings");
    render(<Settings user={{ ...base, role: "reader", role_name: "Okuyucu", permissions: ["matters.read", "chat.use"] }} />);

    const labels = screen.getAllByRole("button").map((b) => b.textContent);
    expect(labels).toContain("Hesap");
    expect(labels).toContain("Kişilik");
    expect(labels).not.toContain("Üyeler");
    expect(labels).not.toContain("Roller");
    expect(labels).not.toContain("Yapay zeka");
    expect(screen.getByText("Okuyucu")).toBeInTheDocument();
  });

  it("shows the owner every tab", async () => {
    const { Settings } = await import("./Settings");
    render(
      <Settings
        user={{ ...base, role: "owner", role_name: "Yönetici", permissions: ["members.manage", "roles.manage", "chat.use", "integrations.manage", "office.settings"] }}
      />,
    );

    const labels = screen.getAllByRole("button").map((b) => b.textContent);
    for (const tab of ["Üyeler", "Roller", "Yapay zeka", "Telegram", "Bildirimler"]) expect(labels).toContain(tab);
  });
});
