import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api, ApiError, type SourceRef } from "../api";
import { ArrowLeft, ArrowSquareOut, BookOpenText, Scales } from "../icons";
import { Spinner } from "../a11y";
import { formatLegalText } from "./legalFormat";
import { RightDrawer } from "./RightDrawer";
import { SaveDecisionButton } from "./SaveDecisionButton";
import { MevzuatDetail } from "./MevzuatDetail";
import { drawerWidthFor, isMevzuat } from "./DecisionDetail";

// "İlgili Kararlar" drawer'ı (bkz. docs/DRAWER_ROADMAP.md): bir asistan mesajının dayandığı
// hukuki kaynakları kart listesi olarak gösterir — içtihatlar önde, mevzuat ayrı bölümde.
// Kart gövdesine tıklayınca kararın tam metni drawer içinde açılır (GET /api/legal/documents,
// fetch_document ile aynı kaynak istemcileri); dış bağlantı oku kaynağın kendi sayfasını yeni
// sekmede açar. Tam metin desteklemeyen kaynaklarda detay görünümü hatayı ve varsa dış
// bağlantıyı gösterir.

type DocState =
  | { state: "loading" }
  | { state: "error"; detail: string }
  | { state: "ready"; md: string };

function Badge({ ictihat }: { ictihat: boolean }) {
  return (
    <span className="shrink-0 rounded-md bg-zinc-100 px-1.5 py-0.5 text-[11px] font-medium text-zinc-600">
      {ictihat ? "İçtihat" : "Mevzuat"}
    </span>
  );
}

function ExternalLink({ url }: { url: string }) {
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      title="Kaynakta aç"
      onClick={(e) => e.stopPropagation()}
      className="shrink-0 rounded-md p-0.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
    >
      <ArrowSquareOut size={15} aria-hidden />
    </a>
  );
}

function SourceCard({ source, onOpen }: { source: SourceRef; onOpen: () => void }) {
  const ictihat = source.kind === "ictihat";
  const Ikon = ictihat ? Scales : BookOpenText;
  return (
    <article
      onClick={onOpen}
      className="cursor-pointer rounded-xl border border-zinc-200 bg-white p-3.5 shadow-sm hover:border-zinc-400"
    >
      <div className="flex items-start gap-2">
        <Ikon size={17} aria-hidden className="mt-0.5 shrink-0 text-zinc-500" />
        <h3 className="min-w-0 flex-1 text-[13.5px] font-semibold leading-snug text-zinc-900">
          <button onClick={onOpen} className="text-left hover:underline">
            {source.title}
          </button>
        </h3>
        <Badge ictihat={ictihat} />
        <span onClick={(e) => e.stopPropagation()}>
          <SaveDecisionButton decision={source} />
        </span>
        {source.url && <ExternalLink url={source.url} />}
      </div>
      {(source.date || source.snippet) && (
        <p className="mt-1.5 line-clamp-3 text-[12.5px] leading-relaxed text-zinc-600">
          {source.date && <span className="font-medium text-zinc-600">[Karar tarihi: {source.date}] </span>}
          {source.snippet}
        </p>
      )}
    </article>
  );
}

// Drawer içi detay: künye + tam metin. Metin sunucudan Markdown gelir ve chat balonlarıyla aynı
// `md` tipografisiyle basılır.
function SourceDetail({ source, onBack }: { source: SourceRef; onBack: () => void }) {
  const [doc, setDoc] = useState<DocState>({ state: "loading" });

  // Not: bileşen `key`lenerek mount edilir (aşağıda), her kaynak için taze "loading" ile başlar;
  // efekt içinde senkron setState'e gerek kalmaz. Mevzuat kaynaklarını MevzuatDetail kendi
  // getirir (madde ağacı + seçili maddeye göre) — burada ayrıca çekmeye gerek yok.
  useEffect(() => {
    if (isMevzuat(source)) return;
    let stale = false;
    api
      .legalDocument(source.source, source.id)
      .then((d) => {
        // Sunucu düz metne yakın içerik döndürür; sunum formatlaması burada (bkz. legalFormat.ts).
        if (!stale) setDoc({ state: "ready", md: formatLegalText(d.content_md) });
      })
      .catch((err) => {
        if (!stale) {
          setDoc({
            state: "error",
            detail: err instanceof ApiError ? err.message : "Belge getirilemedi.",
          });
        }
      });
    return () => {
      stale = true;
    };
  }, [source]);

  const kunye = [
    [source.court, source.chamber].filter(Boolean).join(" "),
    source.case_number && `${source.case_number} E.`,
    source.decision_number && `${source.decision_number} K.`,
    source.date,
  ]
    .filter(Boolean)
    .join(" · ");

  const backHeader = (
    <div className="flex items-center gap-2 border-b border-zinc-200 px-3.5 py-2">
      {/* Detaya geçişte focus buraya taşınır — klavye/ekran okuyucu görünüm değişimini duyar. */}
      <button
        autoFocus
        onClick={onBack}
        className="flex items-center gap-1 rounded-md px-2 py-1 text-[13px] text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900"
      >
        <ArrowLeft size={13} aria-hidden /> Listeye dön
      </button>
    </div>
  );

  if (isMevzuat(source)) {
    return (
      <div className="flex min-h-0 flex-1 flex-col">
        {backHeader}
        <MevzuatDetail
          mevzuatId={source.id}
          title={source.title}
          url={source.url}
          action={<SaveDecisionButton decision={source} />}
        />
      </div>
    );
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex items-center gap-2 border-b border-zinc-200 px-3.5 py-2">
        {/* Detaya geçişte focus buraya taşınır — klavye/ekran okuyucu görünüm değişimini duyar. */}
        <button
          autoFocus
          onClick={onBack}
          className="flex items-center gap-1 rounded-md px-2 py-1 text-[13px] text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900"
        >
          <ArrowLeft size={13} aria-hidden /> Listeye dön
        </button>
        <span className="ml-auto flex items-center gap-1.5">
          <SaveDecisionButton decision={source} />
          {source.url && <ExternalLink url={source.url} />}
        </span>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3.5">
        <h3 className="text-[14px] font-semibold leading-snug text-zinc-900">{source.title}</h3>
        {kunye && <p className="mt-1 text-[12px] text-zinc-600">{kunye}</p>}
        <div className="mt-3 border-t border-zinc-100 pt-3 text-[13.5px] leading-relaxed text-zinc-800">
          {doc.state === "loading" && (
            <div className="flex justify-center py-8">
              <Spinner label="Belge yükleniyor…" />
            </div>
          )}
          {doc.state === "error" && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-[13px] text-amber-800">
              {doc.detail}
              {source.url && (
                <>
                  {" "}
                  <a href={source.url} target="_blank" rel="noopener noreferrer" className="font-medium underline">
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
    </div>
  );
}

export function SourcesDrawer({
  sources,
  onClose,
}: {
  sources: SourceRef[];
  onClose: () => void;
}) {
  const [detail, setDetail] = useState<SourceRef | null>(null);
  const kararlar = sources.filter((s) => s.kind === "ictihat");
  const mevzuat = sources.filter((s) => s.kind !== "ictihat");
  const title = kararlar.length > 0 ? `İlgili Kararlar (${kararlar.length})` : `Kaynaklar (${sources.length})`;

  return (
    <RightDrawer open onClose={onClose} label={title} title={title} width={detail ? drawerWidthFor(detail) : "440px"}>
      {detail ? (
        <SourceDetail key={`${detail.source}:${detail.id}`} source={detail} onBack={() => setDetail(null)} />
      ) : (
        <div className="flex flex-col gap-2.5 bg-zinc-50 p-3.5">
          {kararlar.map((s) => (
            <SourceCard key={`${s.source}:${s.id}`} source={s} onOpen={() => setDetail(s)} />
          ))}
          {mevzuat.length > 0 && (
            <>
              {kararlar.length > 0 && (
                <h3 className="mt-2 px-0.5 text-[11px] font-semibold uppercase tracking-wider text-zinc-500">
                  Mevzuat
                </h3>
              )}
              {mevzuat.map((s) => (
                <SourceCard key={`${s.source}:${s.id}`} source={s} onOpen={() => setDetail(s)} />
              ))}
            </>
          )}
        </div>
      )}
    </RightDrawer>
  );
}
