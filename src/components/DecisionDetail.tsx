/* eslint-disable react-refresh/only-export-components -- kunyeOf/DecisionLike are colocated by design */
import { useEffect, useState, type ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError } from "../api";
import { ArrowSquareOut, Printer } from "../icons";
import { Spinner } from "../a11y";
import { formatLegalText } from "./legalFormat";
import { MevzuatDetail } from "./MevzuatDetail";

// Karar detayı: Karar Arama'daki sonuç drawer'ı, Kararlarım'daki kayıtlı karar drawer'ı ve
// (SourcesDrawer kendi başlık/rozet düzenini koruduğu için) yalnız ilk ikisi tarafından paylaşılır.
// `action` slotu çağırana göre "Ekle" (Karar Arama) ya da "Kaldır" (Kararlarım) düğmesini taşır.

export type DecisionLike = {
  source: string;
  id: string;
  title: string;
  date?: string | null;
  case_number?: string | null;
  decision_number?: string | null;
  court?: string | null;
  chamber?: string | null;
  url?: string | null;
  snippet?: string | null;
};

export function kunyeOf(d: DecisionLike): string {
  return [
    [d.court, d.chamber].filter(Boolean).join(" "),
    d.case_number && `${d.case_number} E.`,
    d.decision_number && `${d.decision_number} K.`,
    d.date,
  ]
    .filter(Boolean)
    .join(" · ");
}

type DocState = { state: "loading" } | { state: "error"; detail: string } | { state: "ready"; md: string };

export function DecisionDetail({ decision, action }: { decision: DecisionLike; action?: ReactNode }) {
  const [doc, setDoc] = useState<DocState>({ state: "loading" });

  useEffect(() => {
    let stale = false;
    api
      .legalDocument(decision.source, decision.id)
      .then((d) => {
        if (!stale) setDoc({ state: "ready", md: formatLegalText(d.content_md) });
      })
      .catch((err) => {
        if (!stale) {
          setDoc({ state: "error", detail: err instanceof ApiError ? err.message : "Belge getirilemedi." });
        }
      });
    return () => {
      stale = true;
    };
  }, [decision.source, decision.id]);

  const kunye = kunyeOf(decision);

  return (
    <div className="print-area px-4 py-3.5">
      <div className="flex items-start gap-3">
        <h3 className="min-w-0 flex-1 text-[14px] font-semibold leading-snug text-zinc-900">{decision.title}</h3>
        <button
          onClick={() => window.print()}
          title="Yazdır"
          className="shrink-0 rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
        >
          <Printer size={15} aria-hidden />
        </button>
        {decision.url && (
          <a
            href={decision.url}
            target="_blank"
            rel="noopener noreferrer"
            title="Kaynakta aç"
            className="shrink-0 rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
          >
            <ArrowSquareOut size={15} aria-hidden />
          </a>
        )}
      </div>
      {kunye && <p className="mt-1 text-[12px] text-zinc-500">{kunye}</p>}
      {action && <div className="mt-3">{action}</div>}
      <div className="mt-3 border-t border-zinc-100 pt-3 text-[13.5px] leading-relaxed text-zinc-800">
        {doc.state === "loading" && (
          <div className="flex justify-center py-8">
            <Spinner label="Belge yükleniyor…" />
          </div>
        )}
        {doc.state === "error" && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
            {doc.detail}
            {decision.url && (
              <>
                {" "}
                <a href={decision.url} target="_blank" rel="noopener noreferrer" className="font-medium underline">
                  Kaynakta açmayı deneyin
                </a>
                .
              </>
            )}
          </div>
        )}
        {doc.state === "ready" && (
          <div className="md">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{doc.md}</ReactMarkdown>
          </div>
        )}
      </div>
    </div>
  );
}

// mevzuat kaynaklı kararların madde ağacı vardır (kısım/bölüm/madde); diğer tüm kaynaklar (Yargıtay,
// Danıştay, Anayasa vb.) tekil belgedir. Bu ayrım drawer genişliğini de belirler (bkz. drawerWidthFor).
export function isMevzuat(decision: DecisionLike): boolean {
  return decision.source === "mevzuat";
}

// RightDrawer'ın width/overlayWidth prop'larına verilir: mevzuat için madde ağacının sığması adına
// geniş (%60), tekil kararlar için mevcut dar drawer genişliği.
export function drawerWidthFor(decision: DecisionLike | null): string {
  return decision && isMevzuat(decision) ? "60vw" : "480px";
}

/// Karar/mevzuat detayının tek giriş noktası: kaynağa göre MevzuatDetail (ağaç+yazdır) ya da
/// DecisionDetail (tekil belge) render eder — çağıranlar bu ayrımı tekrar tekrar yazmaz.
export function SmartDecisionDetail({ decision, action }: { decision: DecisionLike; action?: ReactNode }) {
  if (isMevzuat(decision)) {
    return <MevzuatDetail mevzuatId={decision.id} title={decision.title} url={decision.url} action={action} />;
  }
  return <DecisionDetail decision={decision} action={action} />;
}
