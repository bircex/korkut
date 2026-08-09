import { useEffect, useState } from "react";
import { api, ApiError, type SavedDecision } from "../api";
import { ArrowSquareOut, BookmarkSimple, Scales, Warning, X } from "../icons";
import { LoadingOverlay } from "../a11y";
import { RightDrawer } from "./RightDrawer";
import { SmartDecisionDetail, drawerWidthFor, kunyeOf, type DecisionLike } from "./DecisionDetail";

// Kararlarım: Ofisim altında, Karar Arama'dan ve sohbetteki İlgili Kararlar'dan "Ekle" ile
// kaydedilen kararların listesi. Aynı detay drawer'ını kullanır (DecisionDetail), yalnız action
// slotu "Kaldır"a döner.

function toDecision(d: SavedDecision): DecisionLike {
  return {
    source: d.source,
    id: d.external_id,
    title: d.title,
    date: d.date,
    case_number: d.case_number,
    decision_number: d.decision_number,
    court: d.court,
    chamber: d.chamber,
    url: d.url,
    snippet: d.snippet,
  };
}

type ListState =
  | { state: "loading" }
  | { state: "error"; detail: string }
  | { state: "ready"; items: SavedDecision[] };

export function KararlarimPanel() {
  const [list, setList] = useState<ListState>({ state: "loading" });
  const [detail, setDetail] = useState<SavedDecision | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);

  useEffect(() => {
    api
      .savedDecisions()
      .then((res) => setList({ state: "ready", items: res.items }))
      .catch((err) =>
        setList({ state: "error", detail: err instanceof ApiError ? err.message : "Kararlar yüklenemedi." }),
      );
  }, []);

  async function remove(id: string) {
    setRemovingId(id);
    try {
      await api.removeSavedDecision(id);
      setList((s) => (s.state === "ready" ? { state: "ready", items: s.items.filter((x) => x.id !== id) } : s));
      setDetail((d) => (d?.id === id ? null : d));
    } catch {
      /* satır kalır, tekrar denenebilir */
    } finally {
      setRemovingId(null);
    }
  }

  return (
    <div className="flex min-w-0 flex-1">
      <div className="min-w-0 flex-1 overflow-y-auto px-6 py-6">
      <div className="mx-auto w-full max-w-3xl">
        <h2 className="mb-1 text-[18px] font-semibold text-zinc-900">Kararlarım</h2>
        <p className="mb-5 text-[13px] text-zinc-500">
          Karar Arama'dan ve sohbetteki İlgili Kararlar'dan Ekle ile kaydettiğiniz kararlar burada listelenir.
        </p>

        {list.state === "loading" && <LoadingOverlay label="Kararlar yükleniyor…" />}
        {list.state === "error" && (
          <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
            <Warning size={16} aria-hidden className="mt-0.5 shrink-0" />
            {list.detail}
          </div>
        )}
        {list.state === "ready" && list.items.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-14 text-center text-zinc-400">
            <BookmarkSimple size={28} aria-hidden />
            <p className="text-[13px]">Henüz kaydedilmiş bir karar yok.</p>
          </div>
        )}
        {list.state === "ready" && list.items.length > 0 && (
          <div className="flex flex-col gap-2.5">
            {list.items.map((d) => {
              const kunye = kunyeOf(toDecision(d));
              return (
                <article
                  key={d.id}
                  onClick={() => setDetail(d)}
                  className="cursor-pointer rounded-xl border border-zinc-200 bg-white p-4 shadow-sm transition hover:border-zinc-400 hover:shadow-md"
                >
                  <div className="flex items-start gap-2.5">
                    <Scales size={18} aria-hidden className="mt-0.5 shrink-0 text-zinc-500" />
                    <div className="min-w-0 flex-1">
                      <h3 className="text-[14px] font-semibold leading-snug text-zinc-900">{d.title}</h3>
                      {kunye && <p className="mt-1 text-[12px] text-zinc-500">{kunye}</p>}
                    </div>
                    <div className="flex shrink-0 items-center gap-1" onClick={(e) => e.stopPropagation()}>
                      {d.url && (
                        <a
                          href={d.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          title="Kaynakta aç"
                          className="shrink-0 rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                        >
                          <ArrowSquareOut size={15} aria-hidden />
                        </a>
                      )}
                      <button
                        onClick={() => remove(d.id)}
                        disabled={removingId === d.id}
                        title="Kararlarımdan kaldır"
                        className="shrink-0 rounded-md p-1 text-zinc-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-40"
                      >
                        <X size={15} aria-hidden />
                      </button>
                    </div>
                  </div>
                  {d.snippet && (
                    <p className="mt-2 line-clamp-2 text-[13px] leading-relaxed text-zinc-600">{d.snippet}</p>
                  )}
                </article>
              );
            })}
          </div>
        )}
      </div>
      </div>

      <RightDrawer
        open={detail !== null}
        onClose={() => setDetail(null)}
        label="Karar detayı"
        title={detail?.title ?? "Karar detayı"}
        width={drawerWidthFor(detail && toDecision(detail))}
      >
        {detail && (
          <SmartDecisionDetail
            key={detail.id}
            decision={toDecision(detail)}
            action={
              <button
                onClick={() => remove(detail.id)}
                disabled={removingId === detail.id}
                className="flex items-center gap-1.5 rounded-md border border-red-200 bg-red-50 px-3 py-1.5 text-[13px] font-medium text-red-700 hover:bg-red-100 disabled:opacity-40"
              >
                <X size={13} aria-hidden /> Kararlarımdan kaldır
              </button>
            }
          />
        )}
      </RightDrawer>
    </div>
  );
}
