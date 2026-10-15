import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { AuditLog } from "./AuditLog";
import { auditApi, type AuditEvent } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, auditApi: { list: vi.fn() } };
});

const event = (action: string, details: string | null, actor: string | null = "Av. Elif"): AuditEvent => ({
  id: `${action}-${Math.random()}`,
  at: "2026-10-15T10:00:00Z",
  actor_id: actor ? "u1" : null,
  actor_name: actor,
  action,
  entity_type: "client",
  entity_id: null,
  details,
});

describe("AuditLog", () => {
  beforeEach(() => vi.resetAllMocks());

  it("shows who looked at what, naming the client fields but not their values", async () => {
    vi.mocked(auditApi.list).mockResolvedValue({
      items: [
        event("client.details_viewed", '{"fields":["email","national_id"]}'),
        event("access.denied", '{"method":"GET","path":"/api/finance/invoices"}', "Av. Mert"),
      ],
      total: 2,
    });
    render(<AuditLog />);

    expect(await screen.findByText("Müvekkil bilgisi görüntülendi")).toBeInTheDocument();
    expect(screen.getByText("e-posta, kimlik no")).toBeInTheDocument();
    expect(screen.getByText("Erişim reddedildi")).toBeInTheDocument();
    expect(screen.getByText("GET /api/finance/invoices")).toBeInTheDocument();
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });

  it("filters by kind", async () => {
    vi.mocked(auditApi.list).mockResolvedValue({ items: [], total: 0 });
    render(<AuditLog />);
    await screen.findByText("Kayıt yok.");

    fireEvent.change(screen.getByLabelText("Tür"), { target: { value: "finance" } });

    await waitFor(() => expect(auditApi.list).toHaveBeenLastCalledWith(50, 0, "finance"));
  });
});
