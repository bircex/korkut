"""The agent's full office toolset — the practice-management surface exposed to the model.

`build_office_tools(user)` returns `(specs, handlers)` like `matter_tools` does: it wraps the
matter tools and adds the rest of the office (müvekkil, takvim, mali, görev, evrak, rapor), so the
agent can run the whole practice from chat AND from the messaging integrations (the gateway wires
the same toolset). Every handler closes over the authenticated `user`, opens its own session, and
delegates to the store modules.

Scope: create / read / update only. Deletion stays UI-only (a chat/WhatsApp message must not be
able to destroy records), and user/account settings (personas, model, channels, membership)
are intentionally NOT exposed here.
"""

from __future__ import annotations

from collections.abc import Callable
from typing import Any

from sqlalchemy import select

from korkut.api import finance_store as fin
from korkut.api import matter_tools, store
from korkut.api import matters_store as ms
from korkut.api import reports_store as reports
from korkut.api.models import Client, DocumentRecord, Expense, Invoice, Task, User
from korkut.core.tool_registry import ToolSpec

_INVOICE_STATUSES = ("draft", "issued", "paid", "cancelled")
_EVRAK_DIRECTIONS = ("incoming", "outgoing", "internal")


def _int(value: Any) -> int | None:
    try:
        v = int(value)
    except (TypeError, ValueError):
        return None
    # ids are positive and fit a signed 64-bit column; out-of-range -> "not found",
    # not a DB DataError.
    return v if 0 < v < 2**63 else None


def _spec(name: str, description: str, parameters: dict) -> ToolSpec:
    return ToolSpec(
        name=name, description=description, parameters=parameters, requires_key=None, module=""
    )


def build_office_tools(user: Any) -> tuple[list[ToolSpec], dict[str, Callable[[dict], Any]]]:
    """Matter tools + the rest of the office, bound to `user` for one request/turn."""
    tools, handlers = matter_tools.build_matter_tools(user)
    user_id = user.id

    # ---------------- müvekkil (client) create / update ----------------

    def create_client(args: dict) -> dict:
        name = (args.get("full_name") or "").strip()
        if not name:
            return {"ok": False, "error": "Müvekkil adı (full_name) gerekli."}
        with store.session() as s:
            c = Client(
                full_name=name,
                national_id=(args.get("national_id") or "").strip() or None,
                phone=(args.get("phone") or "").strip() or None,
                email=(args.get("email") or "").strip() or None,
                address=(args.get("address") or "").strip() or None,
            )
            s.add(c)
            s.commit()
            return {"ok": True, "client": ms.client_brief(c), "note": f"'{c.full_name}' eklendi."}

    def update_client(args: dict) -> dict:
        cid = _int(args.get("client_id"))
        with store.session() as s:
            c = s.get(Client, cid) if cid else None
            if c is None:
                return {"ok": False, "error": "Müvekkil bulunamadı."}
            for field in ("full_name", "national_id", "phone", "email", "address"):
                if args.get(field) is not None:
                    setattr(c, field, str(args[field]).strip() or None)
            s.commit()
            return {"ok": True, "client": ms.client_brief(c), "note": "Müvekkil güncellendi."}

    # ---------------- takvim (calendar events) ----------------

    def create_event(args: dict) -> dict:
        title = (args.get("title") or "").strip()
        if not title:
            return {"ok": False, "error": "Etkinlik başlığı (title) gerekli."}
        if not args.get("start_at"):
            return {"ok": False, "error": "Başlangıç (start_at, YYYY-MM-DD veya ISO) gerekli."}
        with store.session() as s:
            try:
                e = ms.create_event(
                    s,
                    created_by=user_id,
                    category=(args.get("category") or "appointment"),
                    title=title,
                    start_at=args.get("start_at"),
                    end_at=args.get("end_at"),
                    all_day=bool(args.get("all_day")),
                    type=args.get("type"),
                    matter_id=_int(args.get("matter_id")),
                    client_id=_int(args.get("client_id")),
                    location=args.get("location"),
                    notes=args.get("notes"),
                    rrule=args.get("rrule"),
                    remind_before_min=_int(args.get("remind_before_min")),
                )
            except ValueError as exc:
                return {"ok": False, "error": f"Tarih biçimi hatalı: {exc}"}
            return {"ok": True, "event": ms.event_brief(e), "note": "Etkinlik eklendi."}

    def list_events(args: dict) -> dict:
        with store.session() as s:
            try:
                items = ms.events_in_range(
                    s,
                    start=args.get("start"),
                    end=args.get("end"),
                    category=args.get("category"),
                    matter_id=_int(args.get("matter_id")),
                    client_id=_int(args.get("client_id")),
                )
            except ValueError as exc:
                return {"error": f"Tarih biçimi hatalı: {exc}"}
            return {"events": items, "count": len(items)}

    def complete_event(args: dict) -> dict:
        eid = _int(args.get("event_id"))
        if not eid:
            return {"ok": False, "error": "event_id gerekli."}
        with store.session() as s:
            e = ms.update_event(s, eid, status="done")
            if e is None:
                return {"ok": False, "error": "Etkinlik bulunamadı."}
            return {"ok": True, "event": ms.event_brief(e), "note": "Etkinlik tamamlandı."}

    # ---------------- mali (finance) ----------------

    def add_invoice(args: dict) -> dict:
        status = args.get("status") if args.get("status") in _INVOICE_STATUSES else "draft"
        with store.session() as s:
            inv = Invoice(
                client_id=_int(args.get("client_id")),
                matter_id=_int(args.get("matter_id")),
                amount=fin.to_amount(args.get("amount")),
                vat_rate=fin.to_amount(args.get("vat_rate")) if args.get("vat_rate") else 20,
                description=(args.get("description") or "").strip() or None,
                status=status,
                issued_at=ms.parse_date(args.get("issued_at")),
            )
            s.add(inv)
            s.commit()
            return {"ok": True, "invoice": fin.invoice_brief(inv), "note": "Fatura eklendi."}

    def add_expense(args: dict) -> dict:
        with store.session() as s:
            exp = Expense(
                matter_id=_int(args.get("matter_id")),
                client_id=_int(args.get("client_id")),
                amount=fin.to_amount(args.get("amount")),
                kind=(args.get("kind") or "").strip() or None,
                description=(args.get("description") or "").strip() or None,
                spent_at=ms.parse_date(args.get("spent_at")),
            )
            s.add(exp)
            s.commit()
            return {"ok": True, "expense": fin.expense_brief(exp), "note": "Masraf eklendi."}

    def list_invoices(args: dict) -> dict:
        with store.session() as s:
            rows = fin.list_invoices(
                s, client_id=_int(args.get("client_id")), matter_id=_int(args.get("matter_id"))
            )
            return {"invoices": [fin.invoice_brief(i) for i in rows], "count": len(rows)}

    def list_expenses(args: dict) -> dict:
        with store.session() as s:
            rows = fin.list_expenses(
                s, client_id=_int(args.get("client_id")), matter_id=_int(args.get("matter_id"))
            )
            return {"expenses": [fin.expense_brief(e) for e in rows], "count": len(rows)}

    def finance_summary(args: dict) -> dict:
        with store.session() as s:
            return fin.summary(s, client_id=_int(args.get("client_id")))

    # ---------------- görev (tasks) ----------------

    def add_task(args: dict) -> dict:
        title = (args.get("title") or "").strip()
        if not title:
            return {"ok": False, "error": "Görev başlığı (title) gerekli."}
        with store.session() as s:
            t = Task(
                title=title,
                description=(args.get("description") or "").strip() or None,
                assignee_id=_int(args.get("assignee_id")),
                matter_id=_int(args.get("matter_id")),
                due_date=ms.parse_date(args.get("due_date")),
                created_by=user_id,
            )
            s.add(t)
            s.commit()
            return {"ok": True, "task": ms.task_brief(t), "note": "Görev eklendi."}

    def list_tasks(args: dict) -> dict:
        with store.session() as s:
            rows = ms.list_tasks(
                s,
                status=args.get("status"),
                assignee_id=_int(args.get("assignee_id")),
                matter_id=_int(args.get("matter_id")),
            )
            return {"tasks": [ms.task_brief(t) for t in rows], "count": len(rows)}

    def complete_task(args: dict) -> dict:
        tid = _int(args.get("task_id"))
        with store.session() as s:
            t = s.get(Task, tid) if tid else None
            if t is None:
                return {"ok": False, "error": "Görev bulunamadı."}
            t.status = "done"
            s.commit()
            return {"ok": True, "task": ms.task_brief(t), "note": "Görev tamamlandı."}

    # ---------------- evrak (document register) ----------------

    def add_evrak(args: dict) -> dict:
        title = (args.get("title") or "").strip()
        if not title:
            return {"ok": False, "error": "Evrak başlığı (title) gerekli."}
        direction = (
            args.get("direction") if args.get("direction") in _EVRAK_DIRECTIONS else "incoming"
        )
        with store.session() as s:
            d = DocumentRecord(
                matter_id=_int(args.get("matter_id")),
                direction=direction,
                title=title,
                reference_no=(args.get("reference_no") or "").strip() or None,
                doc_date=ms.parse_date(args.get("doc_date")),
                notes=(args.get("notes") or "").strip() or None,
                created_by=user_id,
            )
            s.add(d)
            s.commit()
            return {"ok": True, "evrak": ms.document_record_brief(d), "note": "Evrak kaydedildi."}

    def list_evrak(args: dict) -> dict:
        with store.session() as s:
            rows = ms.list_document_records(
                s, matter_id=_int(args.get("matter_id")), direction=args.get("direction")
            )
            return {"evrak": [ms.document_record_brief(d) for d in rows], "count": len(rows)}

    # ---------------- rapor + üye listesi (read-only) ----------------

    def office_overview(_args: dict) -> dict:
        with store.session() as s:
            return reports.overview(s)

    def list_members(_args: dict) -> dict:
        with store.session() as s:
            rows = s.scalars(select(User).order_by(User.id)).all()
            members = [
                {"id": u.id, "username": u.username, "full_name": u.full_name, "role": u.role}
                for u in rows
            ]
            return {"members": members, "count": len(members)}

    handlers.update(
        {
            "create_client": create_client,
            "update_client": update_client,
            "create_event": create_event,
            "list_events": list_events,
            "complete_event": complete_event,
            "add_invoice": add_invoice,
            "add_expense": add_expense,
            "list_invoices": list_invoices,
            "list_expenses": list_expenses,
            "finance_summary": finance_summary,
            "add_task": add_task,
            "list_tasks": list_tasks,
            "complete_task": complete_task,
            "add_evrak": add_evrak,
            "list_evrak": list_evrak,
            "office_overview": office_overview,
            "list_members": list_members,
        }
    )

    _opt_str = {"type": "string", "required": False, "description": ""}
    tools.extend(
        [
            _spec(
                "create_client",
                "Yeni müvekkil kaydı oluştur. Avukat 'X'i müvekkil olarak ekle' dediğinde kullan.",
                {
                    "full_name": {"type": "string", "required": True, "description": "Ad soyad."},
                    "national_id": {**_opt_str, "description": "TCKN (varsa)."},
                    "phone": {**_opt_str, "description": "Telefon (varsa)."},
                    "email": {**_opt_str, "description": "E-posta (varsa)."},
                    "address": {**_opt_str, "description": "Adres (varsa)."},
                },
            ),
            _spec(
                "update_client",
                "Bir müvekkilin bilgilerini güncelle (telefon, e-posta, adres, TCKN, ad).",
                {
                    "client_id": {
                        "type": "integer",
                        "required": True,
                        "description": "Müvekkil id.",
                    },
                    "full_name": _opt_str,
                    "national_id": _opt_str,
                    "phone": _opt_str,
                    "email": _opt_str,
                    "address": _opt_str,
                },
            ),
            _spec(
                "create_event",
                "Takvime etkinlik ekle: randevu/müvekkil görüşmesi (category=appointment), duruşma "
                "(hearing), süre (deadline) veya hatırlatma (reminder). Avukat 'yarın 14:00 "
                "görüşme ekle', 'şu tarihe toplantı koy' dediğinde kullan. Tekrar için rrule "
                "(FREQ=WEEKLY;INTERVAL=1;UNTIL=YYYY-MM-DD); başlamadan önce e-posta hatırlatması "
                "için remind_before_min (dakika) ver.",
                {
                    "title": {
                        "type": "string",
                        "required": True,
                        "description": "Etkinlik başlığı.",
                    },
                    "start_at": {
                        "type": "string",
                        "required": True,
                        "description": "Başlangıç: 'YYYY-MM-DD' veya 'YYYY-MM-DDTHH:MM:SS'.",
                    },
                    "category": {
                        **_opt_str,
                        "description": "appointment|hearing|deadline|reminder (vars. appointment).",
                    },
                    "type": {**_opt_str, "description": "Alt tür (örn. keşif, müvekkil görüşme)."},
                    "end_at": {**_opt_str, "description": "Bitiş (ISO), varsa."},
                    "all_day": {
                        "type": "boolean",
                        "required": False,
                        "description": "Tüm gün mü?",
                    },
                    "matter_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Bağlı dosya id (varsa).",
                    },
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Bağlı müvekkil id (varsa).",
                    },
                    "location": {**_opt_str, "description": "Yer (adliye/ofis/online)."},
                    "notes": {**_opt_str, "description": "Not (varsa)."},
                    "rrule": {**_opt_str, "description": "Tekrar kuralı (RFC-5545 alt kümesi)."},
                    "remind_before_min": {
                        "type": "integer",
                        "required": False,
                        "description": "Başlamadan kaç dk önce e-posta hatırlatması.",
                    },
                },
            ),
            _spec(
                "list_events",
                "Takvim etkinliklerini bir tarih aralığında listele (tekrarlar açılır). start ve "
                "end ISO tarih ver. category/matter_id/client_id ile süzebilirsin.",
                {
                    "start": {
                        "type": "string",
                        "required": True,
                        "description": "Aralık başı (ISO).",
                    },
                    "end": {
                        "type": "string",
                        "required": True,
                        "description": "Aralık sonu (ISO).",
                    },
                    "category": _opt_str,
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    },
                },
            ),
            _spec(
                "complete_event",
                "Bir takvim etkinliğini 'tamamlandı' işaretle.",
                {"event_id": {"type": "integer", "required": True, "description": "Etkinlik id."}},
            ),
            _spec(
                "add_invoice",
                "Fatura/serbest meslek makbuzu ekle (net tutar + KDV%). Avukat 'şu müvekkile … TL "
                "fatura kes' dediğinde kullan.",
                {
                    "amount": {
                        "type": "number",
                        "required": True,
                        "description": "Net tutar (KDV hariç).",
                    },
                    "vat_rate": {
                        "type": "number",
                        "required": False,
                        "description": "KDV % (varsayılan 20).",
                    },
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "description": _opt_str,
                    "status": {**_opt_str, "description": "draft|issued|paid|cancelled."},
                },
            ),
            _spec(
                "add_expense",
                "Ofis/dosya masrafı ekle (harç, posta, bilirkişi vb.). Avukat 'şu dosyaya … TL "
                "harç masrafı yaz' dediğinde kullan.",
                {
                    "amount": {"type": "number", "required": True, "description": "Tutar."},
                    "kind": {**_opt_str, "description": "Masraf türü (harç, posta, bilirkişi…)."},
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    },
                    "description": _opt_str,
                },
            ),
            _spec(
                "list_invoices",
                "Faturaları listele; client_id/matter_id ile süz.",
                {
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                },
            ),
            _spec(
                "list_expenses",
                "Masrafları listele; client_id/matter_id ile süz.",
                {
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                },
            ),
            _spec(
                "finance_summary",
                "Cari mali özet: faturalanan, tahsil edilen, bakiye, masraf. client_id verirsen o "
                "müvekkile özel.",
                {
                    "client_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Müvekkil id.",
                    }
                },
            ),
            _spec(
                "add_task",
                "Görev oluştur ve (opsiyonel) bir avukata ata. assignee_id'yi list_members'tan al.",
                {
                    "title": {"type": "string", "required": True, "description": "Görev başlığı."},
                    "assignee_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Atanan avukat id.",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "due_date": {**_opt_str, "description": "Son tarih (YYYY-MM-DD)."},
                    "description": _opt_str,
                },
            ),
            _spec(
                "list_tasks",
                "Görevleri listele; status (open/done), assignee_id, matter_id ile süz.",
                {
                    "status": {**_opt_str, "description": "open|done."},
                    "assignee_id": {
                        "type": "integer",
                        "required": False,
                        "description": "Atanan avukat id.",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                },
            ),
            _spec(
                "complete_task",
                "Bir görevi 'tamamlandı' işaretle.",
                {"task_id": {"type": "integer", "required": True, "description": "Görev id."}},
            ),
            _spec(
                "add_evrak",
                "Evrak defterine kayıt ekle (gelen/giden/iç yazışma). Avukat 'şu tebligatı evraka "
                "işle' dediğinde kullan.",
                {
                    "title": {"type": "string", "required": True, "description": "Evrak konusu."},
                    "direction": {
                        **_opt_str,
                        "description": "incoming|outgoing|internal (varsayılan incoming).",
                    },
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "reference_no": {**_opt_str, "description": "Sayı/referans."},
                    "doc_date": {**_opt_str, "description": "Evrak tarihi (YYYY-MM-DD)."},
                    "notes": _opt_str,
                },
            ),
            _spec(
                "list_evrak",
                "Evrak kayıtlarını listele; matter_id/direction ile süz.",
                {
                    "matter_id": {"type": "integer", "required": False, "description": "Dosya id."},
                    "direction": {**_opt_str, "description": "incoming|outgoing|internal."},
                },
            ),
            _spec(
                "office_overview",
                "Ofisin genel özetini getir: dosya/müvekkil sayıları, mali durum, görevler, "
                "ajanda, evrak. Avukat 'genel durum', 'özet ver' dediğinde kullan.",
                {},
            ),
            _spec(
                "list_members",
                "Ofis üyelerini (avukatları) listele — görev atamak için id almak üzere.",
                {},
            ),
        ]
    )

    return tools, handlers
