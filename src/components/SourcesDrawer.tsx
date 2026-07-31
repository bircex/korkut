import type { SourceRef } from "../api";
import { ArrowSquareOut, Scales, BookOpenText } from "../icons";
import { RightDrawer } from "./RightDrawer";

// "İlgili Kararlar" drawer'ı (bkz. docs/DRAWER_ROADMAP.md): bir asistan mesajının dayandığı
// hukuki kaynakları kart listesi olarak gösterir — içtihatlar önde, mevzuat ayrı bölümde.
// Kart gövdesi karar künyesi + tarih + özettir; dış bağlantı oku kaynağın kendi sayfasını
// yeni sekmede açar. (Drawer içi tam metin görünümü Faz 5'te eklenecek.)

function SourceCard({ source }: { source: SourceRef }) {
  const ictihat = source.kind === "ictihat";
  const Ikon = ictihat ? Scales : BookOpenText;
  return (
    <article className="rounded-xl border border-zinc-200 bg-white p-3.5 shadow-sm">
      <div className="flex items-start gap-2">
        <Ikon size={17} aria-hidden className="mt-0.5 shrink-0 text-zinc-500" />
        <h3 className="min-w-0 flex-1 text-[13.5px] font-semibold leading-snug text-zinc-900">
          {source.title}
        </h3>
        <span className="shrink-0 rounded-md bg-zinc-100 px-1.5 py-0.5 text-[11px] font-medium text-zinc-500">
          {ictihat ? "İçtihat" : "Mevzuat"}
        </span>
        {source.url && (
          <a
            href={source.url}
            target="_blank"
            rel="noopener noreferrer"
            title="Kaynakta aç"
            className="shrink-0 rounded-md p-0.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
          >
            <ArrowSquareOut size={15} aria-hidden />
          </a>
        )}
      </div>
      {(source.date || source.snippet) && (
        <p className="mt-1.5 line-clamp-3 text-[12.5px] leading-relaxed text-zinc-600">
          {source.date && <span className="font-medium text-zinc-500">[Karar tarihi: {source.date}] </span>}
          {source.snippet}
        </p>
      )}
    </article>
  );
}

export function SourcesDrawer({
  sources,
  onClose,
}: {
  sources: SourceRef[];
  onClose: () => void;
}) {
  const kararlar = sources.filter((s) => s.kind === "ictihat");
  const mevzuat = sources.filter((s) => s.kind !== "ictihat");
  const title = kararlar.length > 0 ? `İlgili Kararlar (${kararlar.length})` : `Kaynaklar (${sources.length})`;

  return (
    <RightDrawer open onClose={onClose} label={title} title={title} width="440px">
      <div className="flex flex-col gap-2.5 bg-zinc-50 p-3.5">
        {kararlar.map((s) => (
          <SourceCard key={`${s.source}:${s.id}`} source={s} />
        ))}
        {mevzuat.length > 0 && (
          <>
            {kararlar.length > 0 && (
              <h3 className="mt-2 px-0.5 text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                Mevzuat
              </h3>
            )}
            {mevzuat.map((s) => (
              <SourceCard key={`${s.source}:${s.id}`} source={s} />
            ))}
          </>
        )}
      </div>
    </RightDrawer>
  );
}
