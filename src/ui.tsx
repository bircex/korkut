import { useEffect, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode } from "react";

export function Brand({ title = "Korkut", subtitle }: { title?: string; subtitle?: string }) {
  return (
    <div className="flex items-center gap-3.5">
      <img
        src="/korkut-logo.svg"
        alt="Korkut"
        className="h-12 w-12 rounded-xl bg-[#ffffff] p-1 ring-1 ring-black/10"
      />
      <div>
        <h1 className="m-0 text-xl font-bold text-zinc-900">{title}</h1>
        {subtitle && <p className="m-0 mt-0.5 text-[13px] text-zinc-500">{subtitle}</p>}
      </div>
    </div>
  );
}

export function AuthShell({ children }: { children: ReactNode }) {
  // Auth ekranları (giriş, demo/kayıt, bekleme) her zaman açık temada gösterilir: hiç giriş
  // yapmamış ya da tercihi koyu olan biri de beyaz görsün. Ekrandan çıkınca (giriş sonrası)
  // kullanıcının kendi teması geri uygulanır.
  useEffect(() => {
    const prev = document.documentElement.dataset.theme;
    document.documentElement.dataset.theme = "light";
    return () => {
      document.documentElement.dataset.theme = prev ?? "light";
    };
  }, []);

  return (
    <div className="flex min-h-dvh bg-white text-zinc-900">
      {/* Sol %70 — ileride tanıtım slider'ı buraya gelecek. Şimdilik marka tanıtım alanı;
          slider eklenince aşağıdaki içerik onunla değiştirilecek. Mobilde gizli. */}
      <aside className="relative hidden w-[70%] flex-col justify-center overflow-hidden border-r border-zinc-200 bg-zinc-50 px-16 lg:flex">
        {/* SLIDER PLACEHOLDER */}
        <div className="max-w-xl">
          <img
            src="/korkut-logo.svg"
            alt=""
            aria-hidden
            className="h-16 w-16 rounded-2xl bg-white p-1.5 ring-1 ring-black/10"
          />
          <h2 className="mt-8 text-4xl font-bold leading-[1.15] tracking-tight text-zinc-900">
            Hukuk büronuz için
            <br />
            yapay zekâ asistanı
          </h2>
          <p className="mt-4 max-w-md text-[15px] leading-relaxed text-zinc-500">
            Dilekçe ve sözleşmeleri saniyeler içinde hazırlayın, dosyalarınızı düzenli tutun,
            müvekkil yazışmalarını tek yerden yürütün. Verileriniz bürodan çıkmaz.
          </p>
        </div>
        {/* slider noktaları (placeholder) */}
        <div className="absolute bottom-10 left-16 flex items-center gap-2" aria-hidden>
          <span className="h-1.5 w-8 rounded-full bg-zinc-800" />
          <span className="h-1.5 w-2.5 rounded-full bg-zinc-300" />
          <span className="h-1.5 w-2.5 rounded-full bg-zinc-300" />
        </div>
      </aside>

      {/* Sağ ~%30 — form paneli */}
      <main className="flex flex-1 items-center justify-center px-6 py-10 sm:px-10">
        <div className="flex w-full max-w-sm flex-col gap-3">{children}</div>
      </main>
    </div>
  );
}

export function Field({ label, ...props }: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[13px] text-zinc-500">{label}</span>
      <input
        {...props}
        className="w-full rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus:border-zinc-900"
      />
    </label>
  );
}

export function Button({ children, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className="rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white transition-opacity hover:opacity-90 disabled:cursor-default disabled:opacity-50"
    >
      {children}
    </button>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  if (!children) return null;
  return <p className="m-0 text-[13px] text-red-600">{children}</p>;
}
