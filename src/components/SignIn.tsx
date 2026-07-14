import { useEffect, useState } from "react";
import { api } from "../api";
import { AuthShell, Brand } from "../ui";

// The callback bounces failures back here with ?error=<code>; map each to a friendly line.
const ERROR_MESSAGES: Record<string, string> = {
  verify: "Google doğrulaması tamamlanamadı. Lütfen tekrar deneyin.",
  no_office: "Bu Google hesabına bağlı aktif bir ofis yok. Ofis sahibinizden davet isteyin.",
  invite: "Davet kullanılamadı: süresi dolmuş, kullanılmış ya da farklı bir e-posta için olabilir.",
  pending: "Büronuz henüz onaylanmadı. Onaylandığında giriş yapabilirsiniz.",
};

export function SignIn() {
  // Read the callback's ?error once at mount (a lazy initializer avoids a cascading render).
  const [error] = useState<string | null>(() => {
    const code = new URLSearchParams(window.location.search).get("error");
    if (!code) return null;
    return ERROR_MESSAGES[code] ?? "Giriş yapılamadı. Lütfen tekrar deneyin.";
  });
  const [demoOn, setDemoOn] = useState(false);

  useEffect(() => {
    // Drop the ?error param so a refresh is clean (side effect only — no setState here).
    if (new URLSearchParams(window.location.search).get("error")) {
      window.history.replaceState(null, "", "/giris");
    }
    api
      .config()
      .then((c) => setDemoOn(c.demo_enabled))
      .catch(() => {});
  }, []);

  return (
    <AuthShell>
      <div className="mb-4">
        <Brand subtitle="Hukuk asistanınız" />
      </div>
      {error && (
        <div className="rounded-[10px] border border-red-200 bg-red-50 px-3 py-2 text-[13px] text-red-700">
          {error}
        </div>
      )}
      <a
        href={api.googleSignInUrl()}
        className="mt-1 flex items-center justify-center gap-2.5 rounded-[10px] border border-zinc-300 bg-white px-4 py-3 text-[15px] font-semibold text-zinc-800 transition-colors hover:bg-zinc-50"
      >
        <GoogleMark />
        Google ile giriş yap
      </a>
      {demoOn && (
        <a
          href={api.googleSignInUrl()}
          className="text-center text-[13px] font-medium text-zinc-600 underline-offset-2 hover:text-zinc-900 hover:underline"
        >
          Yeni firma mısınız? Demo talep edin
        </a>
      )}
      <p className="mt-1 text-center text-[13px] text-zinc-500">
        Ofisinize davet edildiyseniz davet bağlantısındaki giriş düğmesini kullanın.
      </p>
    </AuthShell>
  );
}

function GoogleMark() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.71-1.57 2.68-3.89 2.68-6.62z"
      />
      <path
        fill="#34A853"
        d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.02-3.7H.96v2.34A9 9 0 0 0 9 18z"
      />
      <path
        fill="#FBBC05"
        d="M3.98 10.72a5.4 5.4 0 0 1 0-3.44V4.94H.96a9 9 0 0 0 0 8.12l3.02-2.34z"
      />
      <path
        fill="#EA4335"
        d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58C13.46.9 11.43 0 9 0A9 9 0 0 0 .96 4.94l3.02 2.34C4.68 5.16 6.66 3.58 9 3.58z"
      />
    </svg>
  );
}
