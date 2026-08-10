import { useState } from "react";
import { CaretDown, Check, Warning, Wrench } from "../icons";
import { Spinner } from "../a11y";
import { JsonValueGrid } from "./JsonValueView";
import type { ToolCallInfo } from "../api";

// Sohbette bir tur boyunca çalışan araç çağrılarının kartı (KDM roadmap Faz 4): LLM hangi tool'u
// hangi argümanlarla çağırdı, sonucu/hatası ne oldu — önceden bu bilgi hiç görünmüyordu (backend
// yalnız tool adını yollardı). Girdi/sonuç JsonValueGrid ile aynı generic introspection'ı kullanır
// (bkz. JsonValueView.tsx, CalculatorForm.tsx ile paylaşılan).

function tryParseJsonObject(content: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(content);
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function ToolCallEntry({ call }: { call: ToolCallInfo }) {
  const [open, setOpen] = useState(false);
  const args =
    call.arguments && typeof call.arguments === "object" ? Object.entries(call.arguments as Record<string, unknown>) : [];
  const resultObject = call.result && !call.result.isError ? tryParseJsonObject(call.result.content) : null;

  return (
    <div className="w-full overflow-hidden rounded-lg border border-zinc-200 bg-white">
      <button
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-[12.5px] text-zinc-600 hover:text-zinc-900"
      >
        <Wrench size={13} aria-hidden className="shrink-0 text-zinc-400" />
        <code className="truncate font-mono text-[12px] text-zinc-700">{call.name}</code>
        {!call.result ? (
          <Spinner label="Çalışıyor" />
        ) : call.result.isError ? (
          <Warning size={13} aria-hidden className="shrink-0 text-amber-500" />
        ) : (
          <Check size={13} weight="bold" aria-hidden className="shrink-0 text-emerald-500" />
        )}
        <CaretDown size={11} aria-hidden className={"ml-auto shrink-0 transition-transform " + (open ? "" : "-rotate-90")} />
      </button>
      {open && (
        <div className="flex flex-col gap-3 border-t border-zinc-100 px-3 py-3">
          {args.length > 0 && (
            <div>
              <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">Girdi</div>
              <JsonValueGrid entries={args} />
            </div>
          )}
          {call.result && (
            <div>
              <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-zinc-400">
                {call.result.isError ? "Hata" : "Sonuç"}
              </div>
              {call.result.isError ? (
                <div className="text-[12.5px] text-amber-700">{call.result.content}</div>
              ) : resultObject ? (
                <JsonValueGrid entries={Object.entries(resultObject)} />
              ) : (
                <div className="whitespace-pre-wrap text-[12.5px] text-zinc-700">{call.result.content}</div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function ToolCallCard({ calls }: { calls: ToolCallInfo[] }) {
  if (calls.length === 0) return null;
  return (
    <div className="flex w-full flex-col gap-1.5">
      {calls.map((c, i) => (
        <ToolCallEntry key={i} call={c} />
      ))}
    </div>
  );
}
