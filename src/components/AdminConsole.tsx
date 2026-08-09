import { useEffect, useState } from "react";
import {
  adminApi,
  type AdminModel,
  type AdminOffice,
  type AdminOfficeDetail,
  type AdminSessionState,
  type AdminStats,
} from "../api";
import { navigate } from "../router";
import {
  ArrowLeft,
  ArrowsClockwise,
  Check,
  Copy,
  MagnifyingGlass,
  Sparkle,
  Users,
  Warning,
  X,
} from "../icons";

export function AdminConsole() {
  const [session, setSession] = useState<AdminSessionState | null>(null);
  const [loading, setLoading] = useState(true);
  const [totpCode, setTotpCode] = useState("");
  const [totpError, setTotpError] = useState("");
  const [totpBusy, setTotpBusy] = useState(false);

  // Bootstrap (first-time TOTP enrollment) state
  const [enrollSecret, setEnrollSecret] = useState<string | null>(null);
  const [enrollError, setEnrollError] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [secretCopied, setSecretCopied] = useState(false);

  // Authed Console Data
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [offices, setOffices] = useState<AdminOffice[]>([]);
  const [models, setModels] = useState<AdminModel[]>([]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "pending" | "active" | "disabled">("all");
  const [refreshing, setRefreshing] = useState(false);

  // Selected Office Detail Drawer
  const [selectedOfficeId, setSelectedOfficeId] = useState<string | null>(null);
  const [officeDetail, setOfficeDetail] = useState<AdminOfficeDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // Model Edit state inside Detail Modal
  const [selectedModelKey, setSelectedModelKey] = useState<string>("");
  const [modelEnabled, setModelEnabled] = useState<boolean>(false);
  const [modelSaving, setModelSaving] = useState(false);
  const [modelSuccess, setModelSuccess] = useState(false);

  // Global action busy state per office id
  const [actionBusyId, setActionBusyId] = useState<string | null>(null);

  async function checkSession() {
    setLoading(true);
    try {
      const s = await adminApi.session();
      setSession(s);
      if (s.state === "authed") {
        await loadConsoleData();
      } else if (s.state === "bootstrap_required") {
        await startEnrollment();
      }
    } catch {
      setSession({ state: "anon" });
    } finally {
      setLoading(false);
    }
  }

  async function startEnrollment() {
    setEnrollError("");
    try {
      const { secret } = await adminApi.enrollStart2fa();
      setEnrollSecret(secret);
    } catch (err: unknown) {
      setEnrollError(err instanceof Error ? err.message : "Kayıt başlatılamadı");
    }
  }

  async function loadConsoleData() {
    setRefreshing(true);
    try {
      const [st, off, md] = await Promise.all([
        adminApi.stats(),
        adminApi.offices(),
        adminApi.models().catch(() => []),
      ]);
      setStats(st);
      setOffices(off);
      setModels(md);
    } catch (e) {
      console.error("Admin data loading failed:", e);
    } finally {
      setRefreshing(false);
    }
  }

  useEffect(() => {
    checkSession();
  }, []);

  async function handleTotpSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!totpCode.trim()) return;
    setTotpBusy(true);
    setTotpError("");
    try {
      if (session?.state === "bootstrap_required") {
        const { recovery_codes } = await adminApi.enrollConfirm2fa(totpCode.trim());
        setRecoveryCodes(recovery_codes);
      } else {
        await adminApi.challenge2fa(totpCode.trim());
        await checkSession();
      }
    } catch (err: unknown) {
      setTotpError(err instanceof Error ? err.message : "Doğrulama başarısız");
    } finally {
      setTotpBusy(false);
    }
  }

  function copySecret() {
    if (!enrollSecret) return;
    navigator.clipboard.writeText(enrollSecret).then(() => {
      setSecretCopied(true);
      setTimeout(() => setSecretCopied(false), 1500);
    });
  }

  async function handleLogout() {
    try {
      await adminApi.logout();
    } catch {
      /* ignore */
    }
    setSession({ state: "anon" });
  }

  async function toggleOfficeStatus(office: AdminOffice) {
    setActionBusyId(office.id);
    try {
      if (office.disabled) {
        await adminApi.enableOffice(office.id);
      } else {
        await adminApi.disableOffice(office.id);
      }
      await loadConsoleData();
      if (selectedOfficeId === office.id) {
        await openOfficeDetail(office.id);
      }
    } catch (e) {
      alert("İşlem gerçekleştirilemedi: " + (e instanceof Error ? e.message : String(e)));
    } finally {
      setActionBusyId(null);
    }
  }

  async function openOfficeDetail(id: string) {
    setSelectedOfficeId(id);
    setDetailLoading(true);
    setModelSuccess(false);
    try {
      const d = await adminApi.officeDetail(id);
      setOfficeDetail(d);
      setSelectedModelKey(d.model.model_key ?? "");
      setModelEnabled(d.model.enabled);
    } catch (e) {
      alert("Büro detayı alınamadı: " + (e instanceof Error ? e.message : String(e)));
      setSelectedOfficeId(null);
    } finally {
      setDetailLoading(false);
    }
  }

  async function handleSaveModelAssignment() {
    if (!selectedOfficeId) return;
    setModelSaving(true);
    setModelSuccess(false);
    try {
      const updated = await adminApi.assignModel(
        selectedOfficeId,
        selectedModelKey.trim() || null,
        modelEnabled,
      );
      setOfficeDetail(updated);
      setModelSuccess(true);
      await loadConsoleData();
    } catch (e) {
      alert("Model ataması başarısız: " + (e instanceof Error ? e.message : String(e)));
    } finally {
      setModelSaving(false);
    }
  }

  // Filtered offices list
  const filteredOffices = offices.filter((o) => {
    const matchesSearch =
      o.name.toLowerCase().includes(search.toLowerCase()) ||
      (o.owner_email && o.owner_email.toLowerCase().includes(search.toLowerCase()));

    if (!matchesSearch) return false;

    if (statusFilter === "pending") return o.disabled;
    if (statusFilter === "active") return !o.disabled;
    if (statusFilter === "disabled") return o.disabled;
    return true;
  });

  if (loading) {
    return (
      <div className="grid min-h-dvh place-items-center bg-zinc-950 text-sm text-zinc-400">
        <div className="flex flex-col items-center gap-3">
          <div className="size-6 animate-spin rounded-full border-2 border-zinc-500 border-t-white" />
          <span>Platform Yönetim Paneli Yükleniyor…</span>
        </div>
      </div>
    );
  }

  // State: bootstrap enrollment confirmed — show the one-time recovery codes before entering.
  if (recoveryCodes) {
    return (
      <div className="grid min-h-dvh place-items-center bg-zinc-950 px-4 text-zinc-100">
        <div className="w-full max-w-md rounded-2xl border border-zinc-800 bg-zinc-900 p-6 shadow-2xl">
          <div className="mb-6 text-center">
            <div className="mx-auto mb-3 grid size-12 place-items-center rounded-xl bg-emerald-500/10 text-emerald-400">
              <Check size={24} />
            </div>
            <h1 className="text-xl font-bold text-white">Kurtarma Kodları</h1>
            <p className="mt-1 text-xs text-zinc-400">
              Authenticator'a erişimini kaybedersen kullanılacak tek kullanımlık kodlar. Şimdi bir
              yere kaydet — bir daha gösterilmeyecek.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-2 rounded-lg border border-zinc-700 bg-zinc-800 p-4 font-mono text-sm text-white">
            {recoveryCodes.map((code) => (
              <span key={code}>{code}</span>
            ))}
          </div>

          <button
            onClick={() => {
              setRecoveryCodes(null);
              checkSession();
            }}
            className="mt-4 w-full rounded-lg bg-indigo-600 py-2.5 text-sm font-semibold text-white transition hover:bg-indigo-500"
          >
            Kaydettim, Devam Et
          </button>
        </div>
      </div>
    );
  }

  // State: 2FA required (existing enrollment) or bootstrap (first-time enrollment)
  if (session?.state === "totp_required" || session?.state === "bootstrap_required") {
    const isBootstrap = session.state === "bootstrap_required";
    return (
      <div className="grid min-h-dvh place-items-center bg-zinc-950 px-4 text-zinc-100">
        <div className="w-full max-w-md rounded-2xl border border-zinc-800 bg-zinc-900 p-6 shadow-2xl">
          <div className="mb-6 text-center">
            <div className="mx-auto mb-3 grid size-12 place-items-center rounded-xl bg-amber-500/10 text-amber-400">
              <Warning size={24} />
            </div>
            <h1 className="text-xl font-bold text-white">
              {isBootstrap ? "İki Faktörlü Doğrulama Kurulumu" : "İki Faktörlü Doğrulama (2FA)"}
            </h1>
            <p className="mt-1 text-xs text-zinc-400">
              {isBootstrap
                ? `Bu hesap için henüz TOTP kaydı yok (${session.email}). Aşağıdaki anahtarı authenticator app'ine ekle.`
                : `Platform yöneticisi girişi için doğrulama kodunuzu girin (${session.email})`}
            </p>
          </div>

          {isBootstrap && (
            <div className="mb-4 rounded-lg border border-zinc-700 bg-zinc-800 p-4">
              {enrollSecret ? (
                <>
                  <label className="block text-xs font-medium text-zinc-400">
                    Kurulum Anahtarı (manuel giriş)
                  </label>
                  <div className="mt-1 flex items-center gap-2">
                    <code className="flex-1 truncate rounded-lg bg-zinc-900 px-3 py-2 font-mono text-sm text-white">
                      {enrollSecret}
                    </code>
                    <button
                      type="button"
                      onClick={copySecret}
                      className="grid size-9 shrink-0 place-items-center rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-700"
                      title="Kopyala"
                    >
                      {secretCopied ? <Check size={16} /> : <Copy size={16} />}
                    </button>
                  </div>
                  <p className="mt-2 text-[11px] text-zinc-500">
                    Google Authenticator / Authy / 1Password'de "Kurulum anahtarını gir" seçeneğiyle
                    ekle (bu üretim hesabınla aynı değil, sadece bu ortama özel).
                  </p>
                </>
              ) : (
                <p className="text-xs text-zinc-400">Kurulum anahtarı hazırlanıyor…</p>
              )}
              {enrollError && (
                <div className="mt-3 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-400">
                  {enrollError}
                </div>
              )}
            </div>
          )}

          <form onSubmit={handleTotpSubmit} className="flex flex-col gap-4">
            <div>
              <label className="block text-xs font-medium text-zinc-400">6 Haneli TOTP Kodu</label>
              <input
                type="text"
                maxLength={6}
                value={totpCode}
                onChange={(e) => setTotpCode(e.target.value)}
                placeholder="000000"
                className="mt-1 w-full rounded-lg border border-zinc-700 bg-zinc-800 px-4 py-2.5 text-center font-mono text-xl tracking-widest text-white outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
                autoFocus
              />
            </div>

            {totpError && (
              <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-400">
                {totpError}
              </div>
            )}

            <button
              type="submit"
              disabled={totpBusy || !totpCode.trim()}
              className="w-full rounded-lg bg-indigo-600 py-2.5 text-sm font-semibold text-white transition hover:bg-indigo-500 disabled:opacity-50"
            >
              {totpBusy ? "Doğrulanıyor…" : isBootstrap ? "Kaydı Tamamla" : "Doğrula ve Giriş Yap"}
            </button>
          </form>
        </div>
      </div>
    );
  }

  // State: Anon or Denied
  if (session?.state === "anon" || session?.state === "denied") {
    return (
      <div className="grid min-h-dvh place-items-center bg-zinc-950 px-4 text-zinc-100">
        <div className="w-full max-w-md text-center">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-indigo-500/20 bg-indigo-500/10 px-4 py-1.5 text-xs font-semibold text-indigo-400">
            <Sparkle size={14} />
            <span>Korkut Platform Admin Console</span>
          </div>

          <h1 className="text-2xl font-bold text-white">Yönetici Girişi</h1>
          <p className="mt-2 text-sm text-zinc-400">
            {session.state === "denied"
              ? "Bu hesaba platform yöneticisi yetkisi tanımlanmamış."
              : "Platform yönetim paneline erişmek için yetkili yönetici hesabıyla oturum açın."}
          </p>

          <div className="mt-6">
            <a
              href="/admin/api/auth/google/start"
              className="inline-flex w-full items-center justify-center gap-3 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-zinc-900 transition hover:bg-zinc-100"
            >
              <svg className="size-5" viewBox="0 0 24 24">
                <path
                  fill="#4285F4"
                  d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.82-2.4 3.68v3.05h3.88c2.27-2.09 3.665-5.17 3.665-9.17z"
                />
                <path
                  fill="#34A853"
                  d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.24v3.15C3.26 21.37 7.37 24 12 24z"
                />
                <path
                  fill="#FBBC05"
                  d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.24C.45 8.15 0 9.99 0 12s.45 3.85 1.24 5.42l4.04-3.15z"
                />
                <path
                  fill="#EA4335"
                  d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.37 0 3.26 2.63 1.24 6.58l4.04 3.15c.95-2.83 3.6-4.98 6.72-4.98z"
                />
              </svg>
              <span>Google Admin Hesabı ile Giriş Yap</span>
            </a>
          </div>

          <button
            onClick={() => navigate("/dosyalar")}
            className="mt-4 text-xs font-medium text-zinc-500 hover:text-zinc-300 cursor-pointer"
          >
            ← Ana Uygulamaya Dön
          </button>
        </div>
      </div>
    );
  }

  // State: Authed Admin Console
  return (
    <div className="min-h-dvh bg-zinc-950 text-zinc-100">
      {/* Top Bar Navigation */}
      <header className="sticky top-0 z-30 border-b border-zinc-800/80 bg-zinc-950/90 backdrop-blur-md">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <div className="flex items-center gap-3 min-w-0">
            <button
              onClick={() => navigate("/dosyalar")}
              className="inline-flex items-center gap-1.5 rounded-lg border border-zinc-800 bg-zinc-900 px-2.5 py-1.5 text-xs font-medium text-zinc-400 transition hover:bg-zinc-800 hover:text-white cursor-pointer"
              title="Ana Uygulamaya Dön"
            >
              <ArrowLeft size={14} />
              <span className="hidden sm:inline">Uygulama</span>
            </button>
            <div className="h-4 w-px bg-zinc-800 hidden sm:block" />
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="grid size-6 place-items-center rounded-md bg-indigo-600 text-[11px] font-bold text-white shrink-0">
                  K
                </span>
                <h1 className="text-sm sm:text-base font-bold text-white truncate">
                  Korkut Platform Yönetimi
                </h1>
                <span className="hidden sm:inline-flex items-center rounded-full bg-indigo-500/10 px-2 py-0.5 text-[10px] font-semibold text-indigo-400 border border-indigo-500/20">
                  Superadmin
                </span>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <button
              onClick={loadConsoleData}
              disabled={refreshing}
              className="grid size-8 place-items-center rounded-lg border border-zinc-800 bg-zinc-900 text-zinc-400 hover:bg-zinc-800 hover:text-white disabled:opacity-50 cursor-pointer"
              title="Yenile"
            >
              <ArrowsClockwise size={15} className={refreshing ? "animate-spin" : ""} />
            </button>

            {session?.email && (
              <span className="hidden md:inline-block text-xs text-zinc-400 truncate max-w-[180px]">
                {session.email}
              </span>
            )}

            <button
              onClick={handleLogout}
              className="rounded-lg border border-red-500/20 bg-red-500/10 px-3 py-1.5 text-xs font-medium text-red-400 hover:bg-red-500/20 transition cursor-pointer"
            >
              Çıkış
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        {/* Metric Cards Grid */}
        <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4 mb-6">
          <div className="rounded-xl border border-zinc-800/80 bg-zinc-900/60 p-4 backdrop-blur-sm">
            <p className="text-xs font-medium text-zinc-400">Toplam Büro</p>
            <div className="mt-2 flex items-baseline justify-between">
              <span className="text-2xl font-bold text-white">{stats?.offices ?? "—"}</span>
              <span className="text-[11px] font-medium text-zinc-500">Kayıtlı Kiracı</span>
            </div>
          </div>

          <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-4 backdrop-blur-sm">
            <p className="text-xs font-medium text-emerald-400">Aktif Bürolar</p>
            <div className="mt-2 flex items-baseline justify-between">
              <span className="text-2xl font-bold text-emerald-400">{stats?.active ?? "—"}</span>
              <span className="inline-flex items-center gap-1 text-[11px] text-emerald-500">
                <span className="size-1.5 rounded-full bg-emerald-400 animate-pulse" />
                Yayında
              </span>
            </div>
          </div>

          <div
            className={
              "rounded-xl border p-4 backdrop-blur-sm transition " +
              (stats?.pending && stats.pending > 0
                ? "border-amber-500/50 bg-amber-500/10 ring-1 ring-amber-500/30"
                : "border-zinc-800/80 bg-zinc-900/60")
            }
          >
            <p
              className={
                "text-xs font-medium " +
                (stats?.pending && stats.pending > 0 ? "text-amber-400" : "text-zinc-400")
              }
            >
              Onay Bekleyenler
            </p>
            <div className="mt-2 flex items-baseline justify-between">
              <span
                className={
                  "text-2xl font-bold " +
                  (stats?.pending && stats.pending > 0 ? "text-amber-300" : "text-white")
                }
              >
                {stats?.pending ?? "—"}
              </span>
              {stats?.pending && stats.pending > 0 ? (
                <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-semibold text-amber-300">
                  Onay Gerekli
                </span>
              ) : (
                <span className="text-[11px] text-zinc-500">Bekleyen Yok</span>
              )}
            </div>
          </div>

          <div className="rounded-xl border border-zinc-800/80 bg-zinc-900/60 p-4 backdrop-blur-sm">
            <p className="text-xs font-medium text-zinc-400">Toplam Kullanıcı</p>
            <div className="mt-2 flex items-baseline justify-between">
              <span className="text-2xl font-bold text-white">{stats?.users ?? "—"}</span>
              <span className="text-[11px] font-medium text-zinc-500">Kayıtlı Üye</span>
            </div>
          </div>
        </div>

        {/* Search & Filter Toolbar */}
        <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {/* Status Tabs */}
          <div className="flex items-center gap-1 rounded-xl border border-zinc-800 bg-zinc-900 p-1 overflow-x-auto">
            <button
              onClick={() => setStatusFilter("all")}
              className={
                "rounded-lg px-3 py-1.5 text-xs font-medium transition shrink-0 cursor-pointer " +
                (statusFilter === "all"
                  ? "bg-zinc-800 text-white shadow-sm"
                  : "text-zinc-400 hover:text-white")
              }
            >
              Tümü ({offices.length})
            </button>
            <button
              onClick={() => setStatusFilter("pending")}
              className={
                "rounded-lg px-3 py-1.5 text-xs font-medium transition shrink-0 flex items-center gap-1.5 cursor-pointer " +
                (statusFilter === "pending"
                  ? "bg-amber-500/20 text-amber-300 font-semibold"
                  : "text-zinc-400 hover:text-amber-300")
              }
            >
              <span>Bekleyenler</span>
              {stats?.pending ? (
                <span className="rounded-full bg-amber-500/30 px-1.5 py-0.2 text-[10px]">
                  {stats.pending}
                </span>
              ) : null}
            </button>
            <button
              onClick={() => setStatusFilter("active")}
              className={
                "rounded-lg px-3 py-1.5 text-xs font-medium transition shrink-0 cursor-pointer " +
                (statusFilter === "active"
                  ? "bg-emerald-500/20 text-emerald-400 font-semibold"
                  : "text-zinc-400 hover:text-emerald-400")
              }
            >
              Aktifler
            </button>
            <button
              onClick={() => setStatusFilter("disabled")}
              className={
                "rounded-lg px-3 py-1.5 text-xs font-medium transition shrink-0 cursor-pointer " +
                (statusFilter === "disabled"
                  ? "bg-zinc-800 text-zinc-300"
                  : "text-zinc-400 hover:text-white")
              }
            >
              Pasifler
            </button>
          </div>

          {/* Search Box */}
          <div className="relative flex-1 sm:max-w-xs">
            <MagnifyingGlass
              size={15}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-500"
            />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Büro adı veya e-posta ara…"
              className="w-full rounded-xl border border-zinc-800 bg-zinc-900 py-2 pl-9 pr-8 text-xs text-white placeholder-zinc-500 outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
            {search && (
              <button
                onClick={() => setSearch("")}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-500 hover:text-white cursor-pointer"
              >
                <X size={13} />
              </button>
            )}
          </div>
        </div>

        {/* Offices View */}
        {filteredOffices.length === 0 ? (
          <div className="rounded-2xl border border-zinc-800/80 bg-zinc-900/40 p-12 text-center">
            <p className="text-sm font-medium text-zinc-400">Arama kriterine uygun büro bulunamadı</p>
            <p className="mt-1 text-xs text-zinc-600">
              Filtrelerinizi temizleyin veya arama terimini değiştirin.
            </p>
          </div>
        ) : (
          <>
            {/* Desktop Table View (Hidden on mobile) */}
            <div className="hidden md:block overflow-hidden rounded-2xl border border-zinc-800/80 bg-zinc-900/40 backdrop-blur-sm">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-zinc-800 bg-zinc-900/80 text-zinc-400 uppercase tracking-wider font-semibold text-[10px]">
                  <tr>
                    <th className="px-4 py-3.5">Hukuk Bürosu</th>
                    <th className="px-4 py-3.5">Büro Sahibi</th>
                    <th className="px-4 py-3.5">Üyeler</th>
                    <th className="px-4 py-3.5">Durum</th>
                    <th className="px-4 py-3.5">Kayıt Tarihi</th>
                    <th className="px-4 py-3.5 text-right">İşlemler</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-800/60 text-zinc-300">
                  {filteredOffices.map((office) => {
                    const isBusy = actionBusyId === office.id;
                    return (
                      <tr key={office.id} className="hover:bg-zinc-800/30 transition">
                        <td className="px-4 py-3.5 font-medium text-white">
                          <div className="flex items-center gap-2">
                            <span className="grid size-7 shrink-0 place-items-center rounded-lg bg-zinc-800 text-xs font-bold text-zinc-300">
                              {office.name.charAt(0).toUpperCase()}
                            </span>
                            <span className="truncate max-w-[200px]">{office.name}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3.5 text-zinc-400">
                          {office.owner_email ?? "—"}
                        </td>
                        <td className="px-4 py-3.5">
                          <span className="inline-flex items-center gap-1 text-zinc-400 font-medium">
                            <Users size={13} className="text-zinc-500" />
                            {office.members} kişi
                          </span>
                        </td>
                        <td className="px-4 py-3.5">
                          {office.disabled ? (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-500/10 px-2.5 py-1 text-[11px] font-semibold text-amber-400 border border-amber-500/20">
                              <span className="size-1.5 rounded-full bg-amber-400" />
                              Onay Bekliyor / Pasif
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/10 px-2.5 py-1 text-[11px] font-semibold text-emerald-400 border border-emerald-500/20">
                              <span className="size-1.5 rounded-full bg-emerald-400" />
                              Aktif
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3.5 text-zinc-400">
                          {office.created_at
                            ? new Date(office.created_at).toLocaleDateString("tr-TR")
                            : "—"}
                        </td>
                        <td className="px-4 py-3.5 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              onClick={() => toggleOfficeStatus(office)}
                              disabled={isBusy}
                              className={
                                "rounded-lg px-2.5 py-1.5 font-medium transition disabled:opacity-50 cursor-pointer " +
                                (office.disabled
                                  ? "bg-emerald-600 text-white hover:bg-emerald-500"
                                  : "bg-zinc-800 text-amber-400 hover:bg-zinc-700")
                              }
                            >
                              {isBusy
                                ? "…"
                                : office.disabled
                                ? "Onayla & Etkinleştir"
                                : "Pasife Al"}
                            </button>

                            <button
                              onClick={() => openOfficeDetail(office.id)}
                              className="rounded-lg border border-zinc-700 bg-zinc-800 px-2.5 py-1.5 font-medium text-white hover:bg-zinc-700 transition cursor-pointer"
                            >
                              Detay
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Mobile Cards View (Visible on narrow screens) */}
            <div className="grid grid-cols-1 gap-3 md:hidden">
              {filteredOffices.map((office) => {
                const isBusy = actionBusyId === office.id;
                return (
                  <div
                    key={office.id}
                    className="rounded-xl border border-zinc-800 bg-zinc-900/60 p-4 space-y-3"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-2.5 min-w-0">
                        <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-zinc-800 text-xs font-bold text-white">
                          {office.name.charAt(0).toUpperCase()}
                        </span>
                        <div className="min-w-0">
                          <h3 className="text-sm font-bold text-white truncate">{office.name}</h3>
                          <p className="text-xs text-zinc-400 truncate">{office.owner_email ?? "—"}</p>
                        </div>
                      </div>

                      {office.disabled ? (
                        <span className="shrink-0 rounded-full bg-amber-500/10 px-2 py-0.5 text-[10px] font-semibold text-amber-400 border border-amber-500/20">
                          Bekliyor
                        </span>
                      ) : (
                        <span className="shrink-0 rounded-full bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold text-emerald-400 border border-emerald-500/20">
                          Aktif
                        </span>
                      )}
                    </div>

                    <div className="flex items-center justify-between text-xs text-zinc-400 pt-2 border-t border-zinc-800/60">
                      <span>Üyeler: {office.members} kişi</span>
                      <span>
                        {office.created_at
                          ? new Date(office.created_at).toLocaleDateString("tr-TR")
                          : "—"}
                      </span>
                    </div>

                    <div className="flex items-center gap-2 pt-1">
                      <button
                        onClick={() => toggleOfficeStatus(office)}
                        disabled={isBusy}
                        className={
                          "flex-1 rounded-lg py-2 text-xs font-semibold transition disabled:opacity-50 cursor-pointer " +
                          (office.disabled
                            ? "bg-emerald-600 text-white hover:bg-emerald-500"
                            : "bg-zinc-800 text-amber-400 hover:bg-zinc-700")
                        }
                      >
                        {isBusy
                          ? "İşleniyor…"
                          : office.disabled
                          ? "Onayla & Etkinleştir"
                          : "Pasife Al"}
                      </button>
                      <button
                        onClick={() => openOfficeDetail(office.id)}
                        className="rounded-lg border border-zinc-700 bg-zinc-800 px-3 py-2 text-xs font-medium text-white hover:bg-zinc-700 cursor-pointer"
                      >
                        Detay
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}
      </main>

      {/* Office Detail Drawer / Modal */}
      {selectedOfficeId && (
        <div className="fixed inset-0 z-50 flex items-center justify-end bg-black/60 backdrop-blur-xs">
          <div className="relative flex h-full w-full max-w-xl flex-col bg-zinc-900 border-l border-zinc-800 text-zinc-100 shadow-2xl drawer-anim">
            {/* Drawer Header */}
            <div className="flex items-center justify-between border-b border-zinc-800 p-4 sm:px-6">
              <div className="min-w-0">
                <h2 className="text-base font-bold text-white truncate">
                  {detailLoading ? "Yükleniyor…" : officeDetail?.name}
                </h2>
                <p className="text-xs text-zinc-400">Büro Detayları & Yapılandırması</p>
              </div>
              <button
                onClick={() => setSelectedOfficeId(null)}
                className="grid size-8 place-items-center rounded-lg border border-zinc-800 text-zinc-400 hover:bg-zinc-800 hover:text-white cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            {/* Drawer Content */}
            <div className="flex-1 overflow-y-auto p-4 sm:p-6 space-y-6">
              {detailLoading || !officeDetail ? (
                <div className="grid place-items-center py-12 text-zinc-500 text-sm">
                  Detaylar yükleniyor…
                </div>
              ) : (
                <>
                  {/* Status Banner */}
                  <div
                    className={
                      "flex items-center justify-between rounded-xl border p-4 " +
                      (officeDetail.disabled
                        ? "border-amber-500/30 bg-amber-500/10 text-amber-300"
                        : "border-emerald-500/30 bg-emerald-500/10 text-emerald-300")
                    }
                  >
                    <div>
                      <span className="text-xs font-semibold uppercase tracking-wider block">
                        Durum
                      </span>
                      <span className="text-sm font-bold">
                        {officeDetail.disabled ? "Onay Bekliyor / Pasif" : "Aktif (Yayında)"}
                      </span>
                    </div>

                    <button
                      onClick={() =>
                        toggleOfficeStatus({
                          id: officeDetail.id,
                          name: officeDetail.name,
                          disabled: officeDetail.disabled,
                          owner_email: null,
                          members: officeDetail.members.length,
                          created_at: officeDetail.created_at,
                        })
                      }
                      disabled={actionBusyId === officeDetail.id}
                      className={
                        "rounded-lg px-3 py-1.5 text-xs font-bold transition disabled:opacity-50 cursor-pointer " +
                        (officeDetail.disabled
                          ? "bg-emerald-600 text-white hover:bg-emerald-500"
                          : "bg-amber-600/30 text-amber-200 hover:bg-amber-600/50")
                      }
                    >
                      {officeDetail.disabled ? "Ofisi Onayla" : "Pasife Al"}
                    </button>
                  </div>

                  {/* Usage Overview Stats */}
                  <div className="grid grid-cols-2 gap-3">
                    <div className="rounded-xl border border-zinc-800 bg-zinc-950/50 p-3">
                      <span className="text-[11px] font-medium text-zinc-500">Sohbet Oturumları</span>
                      <p className="mt-1 text-lg font-bold text-white">{officeDetail.totals.chats}</p>
                    </div>
                    <div className="rounded-xl border border-zinc-800 bg-zinc-950/50 p-3">
                      <span className="text-[11px] font-medium text-zinc-500">Mesaj Sayısı</span>
                      <p className="mt-1 text-lg font-bold text-white">{officeDetail.totals.messages}</p>
                    </div>
                  </div>

                  {/* AI Model Configuration Section */}
                  <div className="rounded-xl border border-zinc-800 bg-zinc-950/50 p-4 space-y-4">
                    <div className="flex items-center gap-2 text-sm font-bold text-white">
                      <Sparkle size={16} className="text-indigo-400" />
                      <span>AI Model Yapılandırması</span>
                    </div>

                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-medium text-zinc-400 mb-1">
                          Model Kataloğu Seçimi
                        </label>
                        {models.length === 0 ? (
                          <p className="text-xs text-zinc-500 italic">
                            Tanımlı model kataloğu yok (varsayılan sistem modeli kullanılır).
                          </p>
                        ) : (
                          <select
                            value={selectedModelKey}
                            onChange={(e) => setSelectedModelKey(e.target.value)}
                            className="w-full rounded-lg border border-zinc-700 bg-zinc-800 px-3 py-2 text-xs text-white outline-none focus:border-indigo-500"
                          >
                            <option value="">(Varsayılan Sistem Modeli)</option>
                            {models.map((m) => (
                              <option key={m.key} value={m.key}>
                                {m.label} ({m.model}) {m.is_local ? "[Yerel]" : ""}
                              </option>
                            ))}
                          </select>
                        )}
                      </div>

                      <div className="flex items-center justify-between pt-1">
                        <span className="text-xs font-medium text-zinc-300">
                          Özel Model Atamasını Etkinleştir
                        </span>
                        <input
                          type="checkbox"
                          checked={modelEnabled}
                          onChange={(e) => setModelEnabled(e.target.checked)}
                          className="size-4 rounded accent-indigo-600"
                        />
                      </div>

                      <div className="flex items-center justify-between pt-2">
                        {modelSuccess ? (
                          <span className="text-xs font-semibold text-emerald-400 flex items-center gap-1">
                            <Check size={14} /> Kaydedildi
                          </span>
                        ) : (
                          <span />
                        )}

                        <button
                          onClick={handleSaveModelAssignment}
                          disabled={modelSaving}
                          className="rounded-lg bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-indigo-500 transition disabled:opacity-50 cursor-pointer"
                        >
                          {modelSaving ? "Kaydediliyor…" : "Kaydet"}
                        </button>
                      </div>
                    </div>
                  </div>

                  {/* Members List */}
                  <div className="space-y-3">
                    <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-400">
                      Büro Üyeleri ({officeDetail.members.length})
                    </h3>

                    {officeDetail.members.length === 0 ? (
                      <p className="text-xs text-zinc-500">Büroya kayıtlı üye bulunmuyor.</p>
                    ) : (
                      <div className="divide-y divide-zinc-800/80 rounded-xl border border-zinc-800 bg-zinc-950/50">
                        {officeDetail.members.map((m) => (
                          <div
                            key={m.id}
                            className="flex items-center justify-between gap-3 p-3 text-xs"
                          >
                            <div className="min-w-0">
                              <p className="font-semibold text-white truncate">
                                {m.name ?? m.email}
                              </p>
                              <p className="text-zinc-500 truncate">{m.email}</p>
                            </div>

                            <div className="flex items-center gap-3 shrink-0 text-right">
                              <span className="rounded-full bg-zinc-800 px-2 py-0.5 text-[10px] font-medium text-zinc-300">
                                {m.role === "owner" ? "Sahip" : "Üye"}
                              </span>
                              <span className="text-zinc-500 text-[11px]">
                                {m.chats} sohbet
                              </span>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
