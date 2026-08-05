import { useState } from "react";
import { api } from "../api";
import { BookmarkSimple, Check } from "../icons";
import type { DecisionLike } from "./DecisionDetail";

// "Kararlarıma ekle" düğmesi: Karar Arama'nın liste kartında ve detay drawer'ında, aynı şekilde
// SourcesDrawer'ın (sohbetteki İlgili Kararlar) kart/detayında kullanılır — POST /api/saved-decisions
// idempotent olduğu için iki kez tıklamak hata vermez, kayıt zaten varsa aynısını döner.

type State = "idle" | "saving" | "saved" | "error";

export function SaveDecisionButton({ decision, size = "sm" }: { decision: DecisionLike; size?: "sm" | "md" }) {
  const [state, setState] = useState<State>("idle");

  async function save(e: React.MouseEvent) {
    e.stopPropagation();
    if (state === "saving" || state === "saved") return;
    setState("saving");
    try {
      await api.saveDecision({
        source: decision.source,
        externalId: decision.id,
        title: decision.title,
        date: decision.date,
        caseNumber: decision.case_number,
        decisionNumber: decision.decision_number,
        court: decision.court,
        chamber: decision.chamber,
        url: decision.url,
        snippet: decision.snippet,
      });
      setState("saved");
    } catch {
      setState("error");
      window.setTimeout(() => setState("idle"), 2500);
    }
  }

  const compact = size === "sm";
  return (
    <button
      type="button"
      onClick={save}
      disabled={state === "saving" || state === "saved"}
      title={state === "saved" ? "Kararlarıma eklendi" : "Kararlarıma ekle"}
      className={
        "flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md border font-medium transition " +
        (compact ? "px-2 py-1 text-[12px] " : "px-3 py-1.5 text-[13px] ") +
        (state === "saved"
          ? "border-green-200 bg-green-50 text-green-700"
          : state === "error"
            ? "border-red-200 bg-red-50 text-red-700"
            : "border-zinc-200 bg-white text-zinc-600 hover:border-zinc-400 hover:text-zinc-900")
      }
    >
      {state === "saved" ? (
        <>
          <Check size={compact ? 12 : 13} weight="bold" aria-hidden /> Eklendi
        </>
      ) : state === "error" ? (
        "Eklenemedi"
      ) : (
        <>
          <BookmarkSimple size={compact ? 12 : 13} aria-hidden /> {state === "saving" ? "Ekleniyor…" : "Ekle"}
        </>
      )}
    </button>
  );
}
