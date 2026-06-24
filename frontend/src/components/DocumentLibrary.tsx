import { useState } from "react";
import { type DocBrief } from "../api";

const PAGE = 12;

function whenLabel(iso: string | null): string {
  return iso
    ? new Date(iso).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric" })
    : "";
}

function Section({
  title,
  icon,
  items,
  currentId,
  onOpen,
}: {
  title: string;
  icon: string;
  items: DocBrief[];
  currentId: number | null;
  onOpen: (d: DocBrief) => void;
}) {
  const [page, setPage] = useState(0);
  const pages = Math.max(1, Math.ceil(items.length / PAGE));
  const safePage = Math.min(page, pages - 1);
  const slice = items.slice(safePage * PAGE, safePage * PAGE + PAGE);

  return (
    <section className="mb-8">
      <div className="mb-3 flex items-center gap-2">
        <span className="text-lg leading-none">{icon}</span>
        <h2 className="text-sm font-semibold text-zinc-800">{title}</h2>
        <span className="rounded-full bg-zinc-200 px-2 py-0.5 text-[11px] font-medium text-zinc-600">
          {items.length}
        </span>
      </div>
      {items.length === 0 ? (
        <p className="text-[13px] text-zinc-400">Henüz kayıt yok.</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
            {slice.map((d) => (
              <button
                key={d.id}
                onClick={() => onOpen(d)}
                title={d.title}
                className={
                  "group flex h-32 flex-col rounded-xl border bg-white p-3 text-left transition hover:shadow-md " +
                  (d.conversation_id === currentId
                    ? "border-zinc-900"
                    : "border-zinc-200 hover:border-zinc-400")
                }
              >
                <span className="mb-2 text-2xl leading-none">{icon}</span>
                <span className="line-clamp-3 flex-1 text-[13px] font-medium leading-snug text-zinc-800">
                  {d.title}
                </span>
                <span className="mt-1 text-[11px] text-zinc-400">{whenLabel(d.updated_at)}</span>
              </button>
            ))}
          </div>
          {pages > 1 && (
            <div className="mt-3 flex items-center justify-end gap-3 text-[13px] text-zinc-500">
              <button
                onClick={() => setPage(safePage - 1)}
                disabled={safePage === 0}
                className="rounded-md px-2 py-1 hover:bg-zinc-100 disabled:opacity-40"
              >
                ‹ Önceki
              </button>
              <span>
                {safePage + 1} / {pages}
              </span>
              <button
                onClick={() => setPage(safePage + 1)}
                disabled={safePage >= pages - 1}
                className="rounded-md px-2 py-1 hover:bg-zinc-100 disabled:opacity-40"
              >
                Sonraki ›
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export function DocumentLibrary({
  docs,
  currentId,
  onOpen,
}: {
  docs: DocBrief[];
  currentId: number | null;
  onOpen: (d: DocBrief) => void;
}) {
  const sozlesmeler = docs.filter((d) => d.kind === "sozlesme");
  const dilekceler = docs.filter((d) => d.kind !== "sozlesme");

  return (
    <div className="flex-1 overflow-y-auto px-6 py-6">
      <div className="mx-auto w-full max-w-5xl">
        <h1 className="mb-6 text-lg font-bold text-zinc-900">Dilekçeler & Sözleşmeler</h1>
        <Section
          title="Sözleşmeler"
          icon="📄"
          items={sozlesmeler}
          currentId={currentId}
          onOpen={onOpen}
        />
        <Section
          title="Dilekçeler"
          icon="⚖️"
          items={dilekceler}
          currentId={currentId}
          onOpen={onOpen}
        />
      </div>
    </div>
  );
}
