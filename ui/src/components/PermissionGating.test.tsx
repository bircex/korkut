import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { ClientDetailsCard } from "./ClientDetailsCard";
import { FinancePanel } from "./FinancePanel";
import { api } from "../api";
import { P, PermissionsContext } from "../permissions";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, api: { ...mod.api, invoices: vi.fn(), expenses: vi.fn(), financeSummary: vi.fn() } };
});

const as = (permissions: string[], ui: ReactNode) =>
  render(<PermissionsContext.Provider value={permissions}>{ui}</PermissionsContext.Provider>);

const CLIENT = {
  id: "c1",
  full_name: "Ayşe Yılmaz",
  phone: null,
  email: null,
  national_id: null,
  address: null,
  date_of_birth: null,
  contact_hidden: true,
  identity_hidden: true,
};

// A member is not shown what they may not do; the server refuses it anyway.
describe("actions follow permissions", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(api.invoices).mockResolvedValue({ items: [] });
    vi.mocked(api.expenses).mockResolvedValue({ items: [] });
    vi.mocked(api.financeSummary).mockResolvedValue({ invoiced: 0, paid: 0, outstanding: 0, expenses: 0 });
  });

  it("offers editing a client only with clients.write", () => {
    const { unmount } = as([P.clientsRead], <ClientDetailsCard client={CLIENT} onSaved={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Düzenle" })).not.toBeInTheDocument();
    unmount();

    as([P.clientsRead, P.clientsWrite], <ClientDetailsCard client={CLIENT} onSaved={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Düzenle" })).toBeInTheDocument();
  });

  it("offers new invoices and expenses only with finance.write", async () => {
    const { unmount } = as([P.financeRead], <FinancePanel />);
    expect(await screen.findByText("Mali")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "+ Fatura" })).not.toBeInTheDocument();
    unmount();

    as([P.financeRead, P.financeWrite], <FinancePanel />);
    expect(await screen.findByRole("button", { name: "+ Fatura" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "+ Masraf" })).toBeInTheDocument();
  });
});
