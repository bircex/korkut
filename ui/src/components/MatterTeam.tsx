import { useEffect, useState } from "react";
import { ApiError, teamApi, type MatterTeam as Team, type MatterTeamCandidate } from "../api";
import { Button } from "../a11y";

// Dosyanın ekibi: dosyayı ve ona bağlı her şeyi (duruşma, süre, görev, not, evrak) yalnızca
// ekipteki üyeler görür; Yönetici bütün dosyaları görür. Ekibi dosyanın sorumlusu değiştirir.

function message(err: unknown, fallback: string) {
  return err instanceof ApiError ? err.message : fallback;
}

export function MatterTeam({ matterId }: { matterId: string }) {
  const [team, setTeam] = useState<Team | null>(null);
  const [candidates, setCandidates] = useState<MatterTeamCandidate[]>([]);
  const [selected, setSelected] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    teamApi
      .get(matterId)
      .then((t) => {
        setTeam(t);
        if (t.can_manage) teamApi.candidates(matterId).then(setCandidates).catch(() => {});
      })
      .catch(() => {});
  }, [matterId]);

  if (!team) return null;

  async function run(work: () => Promise<Team>) {
    setBusy(true);
    setError(null);
    try {
      const next = await work();
      setTeam(next);
      setSelected("");
      setCandidates(await teamApi.candidates(matterId).catch(() => []));
    } catch (err) {
      setError(message(err, "Ekip değiştirilemedi."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby={`team-${matterId}`} className="mt-6">
      <h2 id={`team-${matterId}`} className="mb-2 text-sm font-semibold text-zinc-700">
        Ekip
      </h2>
      <p className="mb-2 text-[12px] text-zinc-500">Bu dosyayı ve dosyaya bağlı kayıtları yalnızca ekipteki üyeler görür.</p>
      <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200">
        {team.members.map((m) => (
          <li key={m.user_id} className="flex items-center gap-2 px-3 py-2 text-sm">
            <span className="min-w-0 flex-1 truncate text-zinc-800">{m.name}</span>
            {m.is_lead && (
              <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-700">Sorumlu</span>
            )}
            {team.can_manage && (
              <>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => run(() => teamApi.setLead(matterId, m.user_id, !m.is_lead))}
                  className="rounded-md px-2 py-1 text-[12px] text-zinc-600 hover:bg-zinc-50"
                >
                  {m.is_lead ? "Sorumluluğu kaldır" : "Sorumlu yap"}
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => run(() => teamApi.remove(matterId, m.user_id))}
                  className="rounded-md px-2 py-1 text-[12px] text-red-600 hover:bg-red-50"
                >
                  Çıkar
                </button>
              </>
            )}
          </li>
        ))}
        {team.members.length === 0 && <li className="px-3 py-2 text-[13px] text-zinc-500">Ekipte kimse yok.</li>}
      </ul>
      {team.can_manage && candidates.length > 0 && (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <label className="sr-only" htmlFor={`add-${matterId}`}>
            Ekibe eklenecek üye
          </label>
          <select
            id={`add-${matterId}`}
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            className="min-w-0 flex-1 rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm"
          >
            <option value="">Üye seçin…</option>
            {candidates.map((c) => (
              <option key={c.user_id} value={c.user_id}>
                {c.name}
              </option>
            ))}
          </select>
          <Button variant="secondary" className="px-3 py-2 text-sm" disabled={busy || !selected} onClick={() => run(() => teamApi.add(matterId, selected))}>
            Ekibe ekle
          </Button>
        </div>
      )}
      {error && (
        <p role="alert" className="mt-2 text-[13px] text-red-700">
          {error}
        </p>
      )}
    </section>
  );
}
