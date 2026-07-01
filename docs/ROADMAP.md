# Roadmap

Where Korkut is headed: from an AI legal assistant toward a **self-hosted practice-management
system for a Turkish law office** — the day-to-day management (clients, files, calendar, billing)
and the AI assistant in one place, on the office's own machine.

This is a planning document, not a promise. Features below are grouped by area with their current
status; sequencing is a guide, not a commitment.

## Reference

Feature scope was mapped against common Turkish legal practice-management products (Günce,
Dosya&Vekâlet, HukukPanel and peers). The recurring modules across them:

- **Client & party (CRM)** — clients, counterparties, referrers; individual/corporate records;
  contacts; power-of-attorney tracking; every file linked to a client; a shared "office memory"; a
  client portal.
- **Files/cases/enforcement** — lawsuits, enforcement (icra) proceedings, opinions; one screen;
  UYAP lookups.
- **Calendar/agenda** — hearings, procedural deadlines (süre), site visits; a smart calendar with
  automatic reminders; UETS (KEP/PTT) inbox.
- **Tasks** — assign work to team members; to-dos.
- **Documents** — contracts/petitions; incoming/outgoing correspondence, internal notes, powers of
  attorney; secure storage.
- **Finance/accounting** — client-based invoicing (e-SMM), expense/advance tracking, current
  accounts, collection plans, bank movements.
- **Integrations** — UYAP, UETS, e-mail.
- **Reporting, roles, mobile/cloud.**

## Where Korkut is today

| Area | Status |
|---|---|
| Clients (müvekkil) — name, TCKN, phone, e-mail, address, birth date | ✅ model + edit form inside a matter |
| Matters (dosya/dava) — title, client, court, file no, type, counterparty, status | ✅ |
| Hearings + deadlines + unified agenda | ✅ |
| Drafting — Canvas editor, templates, DOCX/UDF export | ✅ |
| Correspondence — propose→confirm e-mail; Telegram/WhatsApp/e-mail | ✅ |
| AI — live legal research, drafting, matter tools | ✅ |
| Persona (Kişilik) + office memory (Hafıza), UI-configured | ✅ |
| Redaction — best-effort PII masking before cloud calls (regex + checksum + DB-known entities) | ✅ (mitigation, not a guarantee) |
| Lawyer profile — bar-registry number | ✅ |

## Gaps toward practice management

Ordered roughly by value/effort. Each is its own change.

1. **Standalone client management** — a dedicated *Müvekkiller* screen: list + create/edit clients
   directly (not only through a matter), including counterparties and referrers. Also feeds the
   redactor's known-entity layer.
2. **Memory extraction** — see below.
3. **Finance/accounting** — invoices (e-SMM), expenses/advances, current accounts, collection
   plans. The largest missing module; start from a minimal schema.
4. **Tasks** — assign to-dos to members, link to matters, surface in the agenda.
5. **Document archive** — incoming/outgoing correspondence and files, beyond drafts.
6. **UYAP / UETS integration** — file lookups and the notification inbox (biggest external
   dependency; likely a sidecar).
7. **Reporting** — matters/finance summaries.

## Memory extraction — design

Now that Korkut also *manages* the office, memory splits in two, and the agent routes accordingly:

- **Structured facts → management records, not memory.** If a conversation reveals a client's TCKN,
  a hearing date, a counterparty, etc., that belongs in the actual DB records (create/update the
  client, add the hearing/deadline). The agent already has matter tools for this; writes stay behind
  human confirmation.
- **Unstructured, durable context → the Hafıza memory, propose→approve.** Preferences and standing
  facts that have no structured home ("client X prefers video hearings", "this office uses template
  Y for enforcement files"). The agent proposes these via a `remember` tool as a **pending** memory
  item; the lawyer approves or rejects them in the Hafıza tab. Only approved (`active`) items are
  injected into prompts.

Nothing about a client is remembered without a lawyer choosing to — consistent with the whole app's
propose→confirm philosophy and appropriate for handling client data. This keeps free-text memory
small and high-signal, while real case data lands in the records where it can be tracked, filtered,
and reported on.
