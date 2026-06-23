import type { User } from "../api";

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-zinc-100 py-2 last:border-0">
      <span className="text-zinc-500">{label}</span>
      <span className="font-medium text-zinc-900">{value}</span>
    </div>
  );
}

export function Settings({ user, onClose }: { user: User; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-2xl border border-zinc-200 bg-white p-6 shadow-lg"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="m-0 text-lg font-bold text-zinc-900">Ayarlar</h2>
          <button onClick={onClose} className="text-zinc-400 hover:text-zinc-900" title="Kapat">
            ✕
          </button>
        </div>

        <h3 className="mb-1 text-[13px] font-semibold uppercase tracking-wide text-zinc-400">Hesap</h3>
        <div className="mb-4 text-sm">
          <Row label="Kullanıcı adı" value={user.username} />
          <Row label="Ad Soyad" value={user.full_name ?? "—"} />
        </div>

        <h3 className="mb-1 text-[13px] font-semibold uppercase tracking-wide text-zinc-400">Güvenlik</h3>
        <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-[13px] text-zinc-600">
          İki adımlı doğrulama (2FA) <span className="font-medium text-zinc-800">aktif</span>. Parola
          değişikliği veya 2FA sıfırlama için yöneticinizle görüşün.
        </div>
      </div>
    </div>
  );
}
