import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { axeComponent } from "../test/axe";
import { ChatTree } from "./ChatTree";
import type { MatterBrief, SessionBrief } from "../api";

const matter = (id: string, title: string): MatterBrief => ({
  id,
  title,
  client_name: null,
  file_no: null,
  court: null,
  case_type: null,
  counterparty: null,
  status: "open",
  opened_at: null,
  updated_at: null,
});

const chat = (id: string, title: string, matterId: string | null, updated = "2026-10-15T10:00:00Z"): SessionBrief => ({
  id: id as unknown as number,
  title,
  matter_id: matterId as unknown as number | null,
  created_by_name: null,
  updated_at: updated,
});

const MATTERS = [matter("m1", "Kira davası"), matter("m2", "İşçilik alacağı")];
const SESSIONS = [
  chat("s1", "Tahliye ihtarı", "m1"),
  chat("s2", "Fazla mesai hesabı", "m2", "2026-10-14T10:00:00Z"),
  chat("s3", "Genel soru", null),
];

function renderTree(props: Partial<Parameters<typeof ChatTree>[0]> = {}) {
  const onNew = vi.fn();
  render(
    <nav aria-label="Sohbetler">
      <ChatTree
        sessions={SESSIONS}
        matters={MATTERS}
        currentId={null}
        canPersonal
        renderRow={(s) => <div key={s.id}>{s.title}</div>}
        onNew={onNew}
        {...props}
      />
    </nav>,
  );
  return onNew;
}

describe("ChatTree", () => {
  it("keeps each matter's chats in its folder and personal chats apart", async () => {
    renderTree();

    expect(screen.queryByText("Tahliye ihtarı")).not.toBeInTheDocument(); // folders start closed
    expect(screen.getByText("Genel soru")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /^Kira davası/ }));
    expect(screen.getByText("Tahliye ihtarı")).toBeInTheDocument();
    expect(screen.queryByText("Fazla mesai hesabı")).not.toBeInTheDocument();
    expect(await axeComponent(document.body)).toHaveNoViolations();
  });

  it("opens the folder of the chat on screen", () => {
    renderTree({ currentId: "s2" as unknown as number });

    expect(screen.getByText("Fazla mesai hesabı")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^İşçilik alacağı/ })).toHaveAttribute("aria-expanded", "true");
  });

  it("starts a new chat inside a matter", () => {
    const onNew = renderTree();

    fireEvent.click(screen.getByRole("button", { name: "Yeni sohbet: Kira davası" }));
    expect(onNew).toHaveBeenCalledWith("m1");
  });

  it("offers no personal chat without chat.general", () => {
    renderTree({ canPersonal: false, sessions: SESSIONS.filter((s) => s.matter_id !== null) });

    expect(screen.queryByText("Kişisel")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Yeni kişisel sohbet" })).not.toBeInTheDocument();
  });
});
