import { useEffect, useState } from "react";
import { api, type PersonaData } from "../api";

// Ayarlar → Kişilik (owner edits; members view). These fields augment Korkut's locked core
// persona: the office sets a house tone, focus areas, and extra rules. The core security,
// citation and approval rules — and Turkish replies — are fixed in the backend and cannot be
// overridden here.

function Area({
  label,
  hint,
  value,
  onChange,
  rows = 3,
  disabled,
}: {
  label: string;
  hint: string;
  value: string;
  onChange: (v: string) => void;
  rows?: number;
  disabled: boolean;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[13px] font-medium text-zinc-700">{label}</span>
      <textarea
        value={value}
        rows={rows}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className="resize-y rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-zinc-900 disabled:bg-zinc-50 disabled:text-zinc-500"
      />
      <span className="text-[12px] text-zinc-400">{hint}</span>
    </label>
  );
}

export function PersonaSettings() {
  const [data, setData] = useState<PersonaData | null>(null);
  const [tone, setTone] = useState("");
  const [expertise, setExpertise] = useState("");
  const [extraRules, setExtraRules] = useState("");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api
      .getPersona()
      .then((d) => {
        setData(d);
        setTone(d.tone);
        setExpertise(d.expertise);
        setExtraRules(d.extra_rules);
      })
      .catch(() => {});
  }, []);

  if (!data) return <p className="text-[13px] text-zinc-400">Yükleniyor…</p>;
  const disabled = !data.can_edit;

  const change = (setter: (v: string) => void) => (v: string) => {
    setter(v);
    setSaved(false);
  };

  async function save() {
    setBusy(true);
    try {
      const d = await api.savePersona({ tone, expertise, extra_rules: extraRules });
      setData(d);
      setSaved(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <p className="mb-4 text-[13px] text-zinc-500">
        Korkut'un çekirdek güvenlik, atıf ve onay kuralları ile Türkçe yanıt kuralı sabittir; burada
        yalnızca bunların üzerine eklersiniz.
      </p>
      <div className="rounded-lg border border-zinc-200 p-3">
        <div className="flex flex-col gap-3">
          <Area
            label="Üslup"
            hint="Korkut'un genel ses tonu (örn. “resmi ve öz”, “sıcak ve açıklayıcı”)."
            value={tone}
            onChange={change(setTone)}
            disabled={disabled}
          />
          <Area
            label="Uzmanlık alanları"
            hint="Büronun odaklandığı alanlar (örn. “ceza, aile, icra-iflas”)."
            value={expertise}
            onChange={change(setExpertise)}
            disabled={disabled}
          />
          <Area
            label="Ek kurallar"
            hint="Büronun eklemek istediği talimatlar (çekirdek kuralların üzerine)."
            value={extraRules}
            onChange={change(setExtraRules)}
            rows={4}
            disabled={disabled}
          />
        </div>
        {!disabled && (
          <div className="mt-3 flex items-center gap-3">
            <button
              onClick={save}
              disabled={busy}
              className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
            >
              {busy ? "Kaydediliyor…" : "Kaydet"}
            </button>
            {saved && <span className="text-[12px] text-zinc-400">Kaydedildi.</span>}
          </div>
        )}
        {disabled && (
          <p className="mt-3 text-[12px] text-zinc-400">
            Bu ayarları yalnızca büro sahibi düzenleyebilir.
          </p>
        )}
      </div>
    </div>
  );
}
