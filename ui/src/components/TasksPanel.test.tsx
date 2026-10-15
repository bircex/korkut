import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { TasksPanel } from "./TasksPanel";
import { api, type Task, type User } from "../api";

vi.mock("../api", async (importOriginal) => {
  const mod = await importOriginal<typeof import("../api")>();
  return { ...mod, api: { ...mod.api, tasks: vi.fn(), members: vi.fn() } };
});

const task = (title: string, extra: Partial<Task>): Task => ({
  id: title as unknown as number,
  category: "gorev",
  title,
  description: null,
  type: null,
  assignee_id: null,
  assignee_name: null,
  matter_id: null,
  client_id: null,
  status: "open",
  due_date: null,
  end_at: null,
  all_day: true,
  location: null,
  rrule: null,
  remind_before_min: null,
  reminder_status: "pending",
  created_by: null,
  ...extra,
});

const ME: User = {
  id: "me",
  username: "u",
  email: "u@example.com",
  full_name: null,
  bar_registry_no: null,
  role: "lawyer",
  role_name: "Avukat",
  office: null,
  permissions: [],
};

describe("TasksPanel scope", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    window.history.replaceState(null, "", "/gorevler");
    vi.mocked(api.members).mockResolvedValue([]);
    vi.mocked(api.tasks).mockResolvedValue({
      items: [
        task("Bana atanan", { assignee_id: "me" as unknown as number, created_by: "other" }),
        task("Benim açtığım", { created_by: "me" }),
        task("Dosyadaki", { matter_id: "m1" as unknown as number, created_by: "other" }),
      ],
    } as never);
  });

  it("narrows the list to what is assigned to me, what I opened or my matters' tasks", async () => {
    render(<TasksPanel user={ME} />);
    expect(await screen.findByText("Dosyadaki")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Kapsam"), { target: { value: "bana" } });
    expect(screen.getByText("Bana atanan")).toBeInTheDocument();
    expect(screen.queryByText("Benim açtığım")).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Kapsam"), { target: { value: "benim" } });
    expect(screen.getByText("Benim açtığım")).toBeInTheDocument();
    expect(screen.queryByText("Dosyadaki")).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Kapsam"), { target: { value: "dosya" } });
    expect(screen.getByText("Dosyadaki")).toBeInTheDocument();
    expect(screen.queryByText("Bana atanan")).not.toBeInTheDocument();
    expect(window.location.search).toBe("?kapsam=dosya");
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });
});
