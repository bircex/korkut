import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { MatterTeam } from "./MatterTeam";
import { teamApi, type MatterTeam as Team } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, teamApi: { get: vi.fn(), candidates: vi.fn(), add: vi.fn(), setLead: vi.fn(), remove: vi.fn() } };
});

const LEAD = { user_id: "u1", name: "Av. Elif", is_lead: true, added_at: "2026-10-15T10:00:00Z" };

describe("MatterTeam", () => {
  beforeEach(() => vi.resetAllMocks());

  it("lets the lead add a colleague to the matter", async () => {
    const team: Team = { members: [LEAD], can_manage: true };
    vi.mocked(teamApi.get).mockResolvedValue(team);
    vi.mocked(teamApi.candidates).mockResolvedValueOnce([{ user_id: "u2", name: "Av. Mert" }]).mockResolvedValueOnce([]);
    vi.mocked(teamApi.add).mockResolvedValue({ ...team, members: [LEAD, { ...LEAD, user_id: "u2", name: "Av. Mert", is_lead: false }] });
    render(<MatterTeam matterId="m1" />);

    fireEvent.change(await screen.findByLabelText("Ekibe eklenecek üye"), { target: { value: "u2" } });
    fireEvent.click(screen.getByRole("button", { name: "Ekibe ekle" }));

    await waitFor(() => expect(teamApi.add).toHaveBeenCalledWith("m1", "u2"));
    expect(await screen.findByText("Av. Mert")).toBeInTheDocument();
  });

  it("shows a member who is not the lead the team, without the controls", async () => {
    vi.mocked(teamApi.get).mockResolvedValue({ members: [LEAD], can_manage: false });
    render(<MatterTeam matterId="m1" />);

    expect(await screen.findByText("Av. Elif")).toBeInTheDocument();
    expect(screen.getByText("Sorumlu")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Çıkar" })).not.toBeInTheDocument();
    expect(teamApi.candidates).not.toHaveBeenCalled();
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
