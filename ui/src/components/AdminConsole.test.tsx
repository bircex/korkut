import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AdminConsole } from "./AdminConsole";
import { adminApi } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return {
    ...mod,
    adminApi: {
      session: vi.fn(),
      stats: vi.fn(),
      offices: vi.fn(),
      officeDetail: vi.fn(),
      enableOffice: vi.fn(),
      disableOffice: vi.fn(),
      assignModel: vi.fn(),
      models: vi.fn(),
      challenge2fa: vi.fn(),
      logout: vi.fn(),
    },
  };
});

describe("AdminConsole", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("renders login prompt when session is anon", async () => {
    vi.mocked(adminApi.session).mockResolvedValueOnce({ state: "anon" });

    render(<AdminConsole />);

    await waitFor(() => {
      expect(screen.getByText("Yönetici Girişi")).toBeInTheDocument();
    });
    expect(screen.getByText("Google Admin Hesabı ile Giriş Yap")).toBeInTheDocument();
  });

  it("renders 2FA form when session state is totp_required", async () => {
    vi.mocked(adminApi.session).mockResolvedValueOnce({
      state: "totp_required",
      email: "admin@korkut.ai",
    });

    render(<AdminConsole />);

    await waitFor(() => {
      expect(screen.getByText("İki Faktörlü Doğrulama (2FA)")).toBeInTheDocument();
    });
    expect(screen.getByPlaceholderText("000000")).toBeInTheDocument();
  });

  it("renders admin console dashboard with office list when authed", async () => {
    vi.mocked(adminApi.session).mockResolvedValueOnce({
      state: "authed",
      email: "admin@korkut.ai",
    });
    vi.mocked(adminApi.stats).mockResolvedValueOnce({
      offices: 2,
      active: 1,
      pending: 1,
      users: 5,
    });
    vi.mocked(adminApi.offices).mockResolvedValueOnce([
      {
        id: "office-1",
        name: "Test Hukuk Bürosu",
        disabled: false,
        owner_email: "owner@test.com",
        members: 3,
        created_at: "2026-08-01T10:00:00Z",
      },
      {
        id: "office-2",
        name: "Yeni Bürolar A.Ş.",
        disabled: true,
        owner_email: "pending@test.com",
        members: 1,
        created_at: "2026-08-02T12:00:00Z",
      },
    ]);
    vi.mocked(adminApi.models).mockResolvedValueOnce([
      {
        key: "gemini-3.6",
        label: "Gemini 3.6 Flash",
        model: "gemini-3.6-flash",
        base_url: "https://api.google.com",
        is_local: false,
      },
    ]);

    render(<AdminConsole />);

    await waitFor(() => {
      expect(screen.getByText("Korkut Platform Yönetimi")).toBeInTheDocument();
    });

    // Check Stats Cards
    expect(screen.getByText("2")).toBeInTheDocument(); // total offices
    expect(screen.getAllByText("Test Hukuk Bürosu").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Yeni Bürolar A.Ş.").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Onayla & Etkinleştir").length).toBeGreaterThan(0);
  });
});
