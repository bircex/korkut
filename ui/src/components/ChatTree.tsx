import { useState, type ReactNode } from "react";
import type { MatterBrief, SessionBrief } from "../api";
import { CaretDown, CaretRight, Folder, Plus } from "../icons";

// Sol menüdeki sohbetler, Codex'teki gibi dosya klasörleri altında: her dosyanın sohbetleri o dosyanın
// altında, dosyaya bağlı olmayanlar "Kişisel" altında. Liste zaten kullanıcının görebildikleriyle
// gelir (sunucu süzer); burada yalnızca gruplanır. Satırın kendisi (yeniden adlandırma, menü) çağıranda.

type Group = { matter: MatterBrief | null; title: string; sessions: SessionBrief[]; latest: string };

function groups(sessions: SessionBrief[], matters: MatterBrief[]): { folders: Group[]; personal: SessionBrief[] } {
  const byMatter = new Map<string, SessionBrief[]>();
  const personal: SessionBrief[] = [];
  for (const s of sessions) {
    if (s.matter_id == null) personal.push(s);
    else {
      const key = String(s.matter_id);
      byMatter.set(key, [...(byMatter.get(key) ?? []), s]);
    }
  }
  const latest = (list: SessionBrief[]) => list.reduce((acc, s) => ((s.updated_at ?? "") > acc ? (s.updated_at ?? "") : acc), "");
  const folders: Group[] = matters.map((m) => {
    const list = byMatter.get(m.id) ?? [];
    byMatter.delete(m.id);
    return { matter: m, title: m.title, sessions: list, latest: latest(list) };
  });
  // Listede adı olmayan bir dosyanın sohbetleri (ör. dosya listesi yüklenemedi) yine bir klasörde durur.
  for (const [, list] of byMatter) folders.push({ matter: null, title: "Dosya", sessions: list, latest: latest(list) });
  // Son konuşulan dosya üstte; sohbeti olmayanlar adlarına göre altta.
  folders.sort((a, b) => b.latest.localeCompare(a.latest) || a.title.localeCompare(b.title, "tr"));
  return { folders, personal };
}

export function ChatTree({
  sessions,
  matters,
  currentId,
  canPersonal,
  renderRow,
  onNew,
}: {
  sessions: SessionBrief[];
  matters: MatterBrief[];
  currentId: number | null;
  canPersonal: boolean;
  renderRow: (s: SessionBrief) => ReactNode;
  onNew: (matterId: string | null) => void;
}) {
  const { folders, personal } = groups(sessions, matters);
  const currentMatter = sessions.find((s) => s.id === currentId)?.matter_id;
  const [open, setOpen] = useState<Set<string>>(() => new Set(currentMatter != null ? [String(currentMatter)] : []));
  // Bir sohbete gelindiğinde (başka yerden de olsa) klasörü açılır; sonra yine kapatılabilir.
  const [seenMatter, setSeenMatter] = useState(currentMatter);
  if (currentMatter !== seenMatter) {
    setSeenMatter(currentMatter);
    if (currentMatter != null && !open.has(String(currentMatter))) setOpen(new Set(open).add(String(currentMatter)));
  }

  function toggle(key: string) {
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <>
      {folders.map((g) => {
        const key = g.matter?.id ?? String(g.sessions[0]?.matter_id);
        const expanded = open.has(key);
        return (
          <div key={key} className="mb-0.5">
            <div className="group flex items-center rounded-lg hover:bg-zinc-50">
              <button
                onClick={() => toggle(key)}
                aria-expanded={expanded}
                className="flex min-w-0 flex-1 items-center gap-1.5 px-2 py-1.5 text-left text-sm text-zinc-700"
              >
                {expanded ? (
                  <CaretDown size={12} className="shrink-0 text-zinc-400" aria-hidden />
                ) : (
                  <CaretRight size={12} className="shrink-0 text-zinc-400" aria-hidden />
                )}
                <Folder size={16} className="shrink-0 text-zinc-400" aria-hidden />
                <span className="truncate">{g.title}</span>
                {g.sessions.length > 0 && <span className="ml-auto shrink-0 text-[11px] text-zinc-400">{g.sessions.length}</span>}
              </button>
              {g.matter && (
                <button
                  onClick={() => onNew(g.matter!.id)}
                  title={`Yeni sohbet: ${g.title}`}
                  aria-label={`Yeni sohbet: ${g.title}`}
                  className="mr-1 shrink-0 rounded-md p-1 text-zinc-400 opacity-0 hover:bg-zinc-200 hover:text-zinc-700 focus:opacity-100 group-hover:opacity-100"
                >
                  <Plus size={14} aria-hidden />
                </button>
              )}
            </div>
            {expanded && (
              <div className="ml-3 border-l border-zinc-100 pl-1">
                {g.sessions.map((s) => renderRow(s))}
                {g.sessions.length === 0 && <p className="px-2 py-1.5 text-[12px] text-zinc-400">Bu dosyada henüz sohbet yok.</p>}
              </div>
            )}
          </div>
        );
      })}

      {(canPersonal || personal.length > 0) && (
        <div className="mt-2">
          <div className="group flex items-center px-2 pb-0.5 pt-1">
            <span className="flex-1 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">Kişisel</span>
            {canPersonal && (
              <button
                onClick={() => onNew(null)}
                title="Yeni kişisel sohbet"
                aria-label="Yeni kişisel sohbet"
                className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
              >
                <Plus size={14} aria-hidden />
              </button>
            )}
          </div>
          {personal.map((s) => renderRow(s))}
          {personal.length === 0 && <p className="px-2 py-1.5 text-[12px] text-zinc-400">Henüz kişisel sohbet yok.</p>}
        </div>
      )}

      {folders.length === 0 && !canPersonal && personal.length === 0 && (
        <p className="px-2 py-3 text-[13px] text-zinc-400">Size atanmış bir dosya olduğunda sohbetleri burada görünür.</p>
      )}
    </>
  );
}
