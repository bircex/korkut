import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { MattersPanel } from "./MattersPanel";
import { api, teamApi, type MatterDetail, type User } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return {
    ...mod,
    api: { ...mod.api, matters: vi.fn(), clients: vi.fn(), getMatter: vi.fn() },
    teamApi: { get: vi.fn(), candidates: vi.fn(), add: vi.fn(), setLead: vi.fn(), remove: vi.fn() },
  };
});

const MATTER: MatterDetail = {
  id: "m1",
  title: "Kira davası",
  client_name: null,
  file_no: null,
  court: null,
  case_type: null,
  counterparty: null,
  status: "open",
  opened_at: null,
  updated_at: null,
  client: null,
  notes: null,
  hearings: [],
  deadlines: [],
  documents: [],
};

const user = (permissions: string[]): User => ({
  username: "u",
  email: "u@example.com",
  full_name: null,
  bar_registry_no: null,
  role: "custom",
  role_name: "Özel",
  office: null,
  permissions,
});

function open(permissions: string[]) {
  window.history.replaceState(null, "", "/dosyalar/m1");
  render(<MattersPanel user={user(permissions)} onOpenSession={vi.fn()} onNewChat={vi.fn()} />);
}

describe("MattersPanel tabs", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(api.matters).mockResolvedValue({ items: [MATTER], total: 1 });
    vi.mocked(api.clients).mockResolvedValue({ items: [], total: 0 } as never);
    vi.mocked(api.getMatter).mockResolvedValue(MATTER);
    vi.mocked(teamApi.get).mockResolvedValue({ members: [], can_manage: false });
  });

  it("shows a manager every section of the matter", async () => {
    open(["matters.read", "chat.use", "documents.read", "agenda.read", "notes.read", "clients.read", "finance.read"]);

    const tabs = await screen.findAllByRole("tab");
    expect(tabs.map((t) => t.textContent)).toEqual([
      "Özet",
      "Sohbetler",
      "Belgeler",
      "Ajanda",
      "Notlar",
      "Müvekkil",
      "Ekip",
      "Mali",
    ]);
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });

  it("keeps finance and client details from a member who may only read and chat", async () => {
    open(["matters.read", "chat.use", "documents.read"]);

    await screen.findAllByRole("tab");
    expect(screen.queryByRole("tab", { name: "Mali" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Müvekkil" })).not.toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sohbetler" })).toBeInTheDocument();
  });

  it("falls back to the summary when the asked tab is not allowed", async () => {
    window.history.replaceState(null, "", "/dosyalar/m1?sekme=mali");
    render(<MattersPanel user={user(["matters.read"])} onOpenSession={vi.fn()} onNewChat={vi.fn()} />);

    expect(await screen.findByRole("tab", { name: "Özet" })).toHaveAttribute("aria-selected", "true");
  });
});
