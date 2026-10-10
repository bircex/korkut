import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { TelegramLinkSection, TelegramSettings } from "./TelegramSettings";
import { ApiError, telegramApi, type TelegramSettings as Settings } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return {
    ...mod,
    telegramApi: {
      settings: vi.fn(),
      saveSettings: vi.fn(),
      removeBot: vi.fn(),
      unlinkMember: vi.fn(),
      link: vi.fn(),
      createCode: vi.fn(),
      unlink: vi.fn(),
    },
  };
});

const OFF: Settings = { configured: false, enabled: false, bot_username: null, consent_at: null, members: [] };
const ON: Settings = {
  configured: true,
  enabled: true,
  bot_username: "buro_bot",
  consent_at: "2026-10-10T10:00:00Z",
  members: [{ user_id: "u1", name: "Elif Korkmaz", telegram_username: "elif", linked_at: "2026-10-10T11:00:00Z" }],
};

describe("TelegramSettings", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("turns the bot on only after the owner consents", async () => {
    vi.mocked(telegramApi.settings).mockResolvedValue(OFF);
    vi.mocked(telegramApi.saveSettings).mockResolvedValue(ON);
    render(<TelegramSettings />);

    fireEvent.change(await screen.findByLabelText(/Bot anahtarı/), { target: { value: " 123:abc " } });
    const turnOn = screen.getByRole("button", { name: "Botu aç" });
    expect(turnOn).toBeDisabled();

    fireEvent.click(screen.getByLabelText(/Telegram sunucularından geçeceğini/));
    fireEvent.click(turnOn);

    await waitFor(() => expect(telegramApi.saveSettings).toHaveBeenCalledWith({ token: "123:abc", enabled: true, consent: true }));
    expect(await screen.findByText("Elif Korkmaz")).toBeInTheDocument();
  });

  it("shows why Telegram refused the token", async () => {
    vi.mocked(telegramApi.settings).mockResolvedValue(OFF);
    vi.mocked(telegramApi.saveSettings).mockRejectedValue(new ApiError(422, "Telegram bu bot anahtarını tanımadı."));
    render(<TelegramSettings />);

    fireEvent.change(await screen.findByLabelText(/Bot anahtarı/), { target: { value: "1:x" } });
    fireEvent.click(screen.getByLabelText(/Telegram sunucularından geçeceğini/));
    fireEvent.click(screen.getByRole("button", { name: "Botu aç" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("tanımadı");
  });

  it("unlinks a member", async () => {
    vi.mocked(telegramApi.settings).mockResolvedValue(ON);
    vi.mocked(telegramApi.unlinkMember).mockResolvedValue({ ...ON, members: [] });
    render(<TelegramSettings />);

    fireEvent.click(await screen.findByRole("button", { name: "Bağlantıyı kaldır" }));

    await waitFor(() => expect(telegramApi.unlinkMember).toHaveBeenCalledWith("u1"));
    expect(await screen.findByText(/Henüz bağlanan üye yok/)).toBeInTheDocument();
  });

  it("has no accessibility violations", async () => {
    vi.mocked(telegramApi.settings).mockResolvedValue(ON);
    render(<TelegramSettings />);
    await screen.findByText("Elif Korkmaz");

    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});

describe("TelegramLinkSection", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("stays hidden while the office has no bot", async () => {
    vi.mocked(telegramApi.link).mockResolvedValue({ available: false, bot_username: null, linked: false, telegram_username: null, linked_at: null });
    const { container } = render(<TelegramLinkSection />);

    await waitFor(() => expect(telegramApi.link).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });

  it("gives the member a one-tap link to the bot", async () => {
    vi.mocked(telegramApi.link).mockResolvedValue({ available: true, bot_username: "buro_bot", linked: false, telegram_username: null, linked_at: null });
    vi.mocked(telegramApi.createCode).mockResolvedValue({ code: "abc", url: "https://t.me/buro_bot?start=abc", expires_at: "2026-10-10T12:00:00Z" });
    render(<TelegramLinkSection />);

    fireEvent.click(await screen.findByRole("button", { name: "Telegram'ı bağla" }));

    expect(await screen.findByRole("link", { name: "Telegram'da aç" })).toHaveAttribute("href", "https://t.me/buro_bot?start=abc");
    expect(screen.getByText("/start abc")).toBeInTheDocument();
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
