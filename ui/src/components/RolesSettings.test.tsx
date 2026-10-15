import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { RolesSettings } from "./RolesSettings";
import { rolesApi, type RolesResponse } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, rolesApi: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setMemberRole: vi.fn() } };
});

const DATA: RolesResponse = {
  catalog: [
    { name: "Müvekkiller", permissions: [
      { key: "clients.read", label: "Müvekkilin adını görür", hint: null },
      { key: "clients.identity", label: "Kimlik bilgilerini görür", hint: "TC kimlik no, doğum tarihi" },
    ] },
    { name: "Mali", permissions: [{ key: "finance.read", label: "Fatura, masraf ve tahsilatları görür", hint: null }] },
  ],
  roles: [
    { key: "owner", name: "Yönetici", description: null, permissions: ["clients.read", "clients.identity", "finance.read"], is_system: true, editable: false, member_count: 1 },
    { key: "intern", name: "Stajyer", description: null, permissions: ["clients.read"], is_system: true, editable: true, member_count: 2 },
    { key: "custom-ab12", name: "Dış danışman", description: null, permissions: [], is_system: false, editable: true, member_count: 0 },
  ],
};

describe("RolesSettings", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(rolesApi.list).mockResolvedValue(DATA);
  });

  it("lays out permissions against roles, with the owner's column locked", async () => {
    render(<RolesSettings />);

    const ownerIdentity = await screen.findByRole("checkbox", { name: "Yönetici: Kimlik bilgilerini görür" });
    expect(ownerIdentity).toBeChecked();
    expect(ownerIdentity).toBeDisabled();
    expect(screen.getByRole("checkbox", { name: "Stajyer: Kimlik bilgilerini görür" })).not.toBeChecked();
    expect(screen.getByText("2 üye")).toBeInTheDocument();
  });

  it("saves only the roles that changed", async () => {
    vi.mocked(rolesApi.update).mockResolvedValue(DATA.roles[1]);
    render(<RolesSettings />);

    fireEvent.click(await screen.findByRole("checkbox", { name: "Stajyer: Kimlik bilgilerini görür" }));
    fireEvent.click(screen.getByRole("button", { name: "Değişiklikleri kaydet (1 rol)" }));

    await waitFor(() =>
      expect(rolesApi.update).toHaveBeenCalledWith("intern", { name: "Stajyer", description: null, permissions: ["clients.read", "clients.identity"] }),
    );
    expect(rolesApi.update).toHaveBeenCalledTimes(1);
  });

  it("adds a role, and offers to remove only an unused one the office added", async () => {
    vi.mocked(rolesApi.create).mockResolvedValue({ ...DATA.roles[2], key: "custom-cd34", name: "Arşiv" });
    render(<RolesSettings />);

    await screen.findByText("Dış danışman");
    expect(screen.getAllByRole("button", { name: "Sil" })).toHaveLength(1);
    fireEvent.change(screen.getByLabelText("Rol adı"), { target: { value: "Arşiv" } });
    fireEvent.click(screen.getByRole("button", { name: "Rol ekle" }));

    await waitFor(() => expect(rolesApi.create).toHaveBeenCalledWith({ name: "Arşiv", permissions: [] }));
  });

  it("has no accessibility violations", async () => {
    render(<RolesSettings />);
    await screen.findByText("Dış danışman");

    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
