import { Check, Warning } from "../icons";
import { Spinner } from "../a11y";
import { toolIcon, toolLabel } from "./toolLabels";
import type { ToolCallInfo } from "../api";

// Sohbette bir tur boyunca çalışan araç çağrılarının izi (KDM roadmap Faz 4): ThinkingTrail ile aynı
// silik dil — kart, kenarlık, tıklanabilir detay yok; yalnız o an ne yapıldığını söyleyen, kategori
// ikonuyla işaretli ve ince bir dikey çizgiyle birbirine bağlı düz metin satırları (ör. "⚖ Karar
// aranıyor"). LLM'in ham fonksiyon adı ve girdi/sonuç JSON'ı hiçbir zaman gösterilmez —
// toolLabels.ts'teki Türkçe karşılığı gösterilir. Asıl cevap her zaman bu izin altında, mesaj
// balonunda kalır; iz yalnız geçici bir durum bildirimidir.

function ToolCallEntry({ call }: { call: ToolCallInfo }) {
  const Icon = toolIcon(call.name);
  return (
    <div className="flex items-center gap-2 py-0.5 text-[12px] font-medium text-zinc-400">
      <Icon size={13} aria-hidden className="relative z-10 shrink-0" />
      <span className="truncate">{toolLabel(call.name)}</span>
      {!call.result ? (
        <Spinner label="Çalışıyor" />
      ) : call.result.isError ? (
        <Warning size={12} aria-hidden className="shrink-0 text-amber-500" />
      ) : (
        <Check size={12} weight="bold" aria-hidden className="shrink-0 text-emerald-500" />
      )}
    </div>
  );
}

export function ToolCallCard({ calls }: { calls: ToolCallInfo[] }) {
  if (calls.length === 0) return null;
  return (
    <div className="relative flex w-full flex-col">
      {/* Adımları ikondan ikona bağlayan ince çizgi — tek adımda görünmez sınırlara taşar, zararsız. */}
      <div className="pointer-events-none absolute bottom-[10px] left-[6.5px] top-[10px] w-px bg-zinc-200" aria-hidden />
      {calls.map((c, i) => (
        <ToolCallEntry key={i} call={c} />
      ))}
    </div>
  );
}
