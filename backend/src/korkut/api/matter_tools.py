"""Per-request, office-scoped agent tools for the matter domain (dosya/müvekkil/duruşma).

Mirrors the `_send_email` handler pattern: `build_matter_tools(user)` returns `(specs, handlers)`
where each handler closes over the authenticated `user`, opens its own DB session, and delegates to
`matters_store` (which enforces `office_id`). Writes happen DIRECTLY (low-risk, reversible internal
state); deletion is intentionally NOT exposed to the agent — only the UI deletes. The model decides
when to call these; no keyword pre-classification.
"""

from __future__ import annotations

from collections.abc import Callable
from typing import Any

from korkut.api import matters_store as ms
from korkut.api import store
from korkut.core.tool_registry import ToolSpec


def _int(value: Any) -> int | None:
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def build_matter_tools(user: Any) -> tuple[list[ToolSpec], dict[str, Callable[[dict], Any]]]:
    """Build the office-scoped matter toolset for one authenticated request."""
    office_id = user.office_id
    user_id = user.id

    def list_matters(args: dict) -> dict:
        with store.session() as s:
            rows = ms.list_matters(s, office_id, status=(args.get("status") or None))
            return {"matters": [ms.matter_brief(m) for m in rows], "count": len(rows)}

    def get_matter(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        with store.session() as s:
            full = ms.matter_full(s, office_id, mid) if mid else None
            if full is None:
                return {"error": "not_found", "detail": "Dosya bulunamadı."}
            return {"matter": full}

    def prepare_hearing(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        if not mid:
            return {"ok": False, "error": "matter_id gerekli."}
        with store.session() as s:
            full = ms.matter_full(s, office_id, mid)
            if full is None:
                return {"error": "not_found", "detail": "Dosya bulunamadı."}
            agenda = [
                a for a in ms.upcoming_agenda(s, office_id, days=365) if a["matter_id"] == mid
            ]
            return {
                "matter": full,
                "agenda": agenda,
                "note": (
                    "Bu dosya için kısa bir DURUŞMA HAZIRLIK özeti yaz: taraflar ve talep, "
                    "son durum, yaklaşan duruşma ve bekleyen süreler, hazır belgeler ve "
                    "eksikler. Gerekirse ilgili içtihadı search_decisions ile araştırıp ekle."
                ),
            }

    def create_matter(args: dict) -> dict:
        title = (args.get("title") or "").strip()
        if not title:
            return {"ok": False, "error": "Dosya başlığı (title) gerekli."}
        with store.session() as s:
            m = ms.create_matter(
                s,
                office_id,
                user_id,
                title=title,
                client_name=args.get("client_name"),
                file_no=args.get("file_no"),
                court=args.get("court"),
                case_type=args.get("case_type"),
                counterparty=args.get("counterparty"),
            )
            return {
                "ok": True,
                "matter": ms.matter_brief(m),
                "note": f"'{m.title}' dosyası oluşturuldu (dosya no: {m.id}).",
            }

    def update_matter(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        if not mid:
            return {"ok": False, "error": "matter_id gerekli."}
        fields = {
            k: args.get(k)
            for k in ("title", "status", "file_no", "court", "case_type", "counterparty", "notes")
        }
        with store.session() as s:
            m = ms.update_matter(s, office_id, mid, **fields)
            if m is None:
                return {"ok": False, "error": "Dosya bulunamadı."}
            return {"ok": True, "matter": ms.matter_brief(m), "note": "Dosya güncellendi."}

    def list_hearings(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        with store.session() as s:
            if mid:
                m = ms.get_matter(s, office_id, mid)
                if m is None:
                    return {"error": "not_found", "detail": "Dosya bulunamadı."}
                hs = list(m.hearings)
            else:
                hs = ms.upcoming_hearings(s, office_id, days=_int(args.get("upcoming_days")) or 14)
            return {"hearings": [ms.hearing_brief(h) for h in hs], "count": len(hs)}

    def add_hearing(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        if not mid:
            return {"ok": False, "error": "matter_id gerekli."}
        if not args.get("date"):
            return {"ok": False, "error": "date (YYYY-MM-DD) gerekli."}
        with store.session() as s:
            try:
                h = ms.add_hearing(
                    s,
                    office_id,
                    mid,
                    date=args.get("date"),
                    time=args.get("time"),
                    location=args.get("location"),
                    type=args.get("type"),
                    notes=args.get("notes"),
                )
            except ValueError as exc:
                return {"ok": False, "error": f"Tarih/saat biçimi hatalı: {exc}"}
            if h is None:
                return {"ok": False, "error": "Dosya bulunamadı."}
            return {"ok": True, "hearing": ms.hearing_brief(h), "note": "Duruşma eklendi."}

    def upcoming_deadlines(args: dict) -> dict:
        days = _int(args.get("days")) or 14
        with store.session() as s:
            items = ms.upcoming_agenda(s, office_id, days=days)
            return {"agenda": items, "count": len(items), "days": days}

    def add_deadline(args: dict) -> dict:
        mid = _int(args.get("matter_id"))
        if not mid:
            return {"ok": False, "error": "matter_id gerekli."}
        title = (args.get("title") or "").strip()
        if not title:
            return {"ok": False, "error": "Süre başlığı (title) gerekli, örn. 'Cevap süresi'."}
        if not args.get("due_date"):
            return {"ok": False, "error": "due_date (YYYY-MM-DD) gerekli."}
        with store.session() as s:
            try:
                d = ms.add_deadline(
                    s,
                    office_id,
                    mid,
                    due_date=args.get("due_date"),
                    title=title,
                    type=args.get("type"),
                    notes=args.get("notes"),
                )
            except ValueError as exc:
                return {"ok": False, "error": f"Tarih biçimi hatalı: {exc}"}
            if d is None:
                return {"ok": False, "error": "Dosya bulunamadı."}
            return {"ok": True, "deadline": ms.deadline_brief(d), "note": "Süre eklendi."}

    def complete_deadline(args: dict) -> dict:
        did = _int(args.get("deadline_id"))
        if not did:
            return {"ok": False, "error": "deadline_id gerekli."}
        with store.session() as s:
            d = ms.complete_deadline(s, office_id, did)
            if d is None:
                return {"ok": False, "error": "Süre bulunamadı."}
            return {"ok": True, "deadline": ms.deadline_brief(d), "note": "Süre tamamlandı."}

    def list_deadlines(args: dict) -> dict:
        with store.session() as s:
            ds = ms.list_deadlines(s, office_id, matter_id=_int(args.get("matter_id")))
            return {"deadlines": [ms.deadline_brief(d) for d in ds], "count": len(ds)}

    def list_clients(_args: dict) -> dict:
        with store.session() as s:
            cs = ms.list_clients(s, office_id)
            return {"clients": [ms.client_brief(c) for c in cs], "count": len(cs)}

    handlers: dict[str, Callable[[dict], Any]] = {
        "list_matters": list_matters,
        "get_matter": get_matter,
        "prepare_hearing": prepare_hearing,
        "create_matter": create_matter,
        "update_matter": update_matter,
        "list_hearings": list_hearings,
        "add_hearing": add_hearing,
        "upcoming_deadlines": upcoming_deadlines,
        "add_deadline": add_deadline,
        "complete_deadline": complete_deadline,
        "list_deadlines": list_deadlines,
        "list_clients": list_clients,
    }

    def _spec(name: str, description: str, parameters: dict) -> ToolSpec:
        return ToolSpec(
            name=name, description=description, parameters=parameters, requires_key=None, module=""
        )

    tools = [
        _spec(
            "list_matters",
            "Ofisin dosyalarını (dava/dosya) listele. Avukat 'dosyalarım', 'açık davalar' gibi "
            "sorduğunda kullan. İsteğe bağlı status ile süz (örn. open/closed).",
            {
                "status": {
                    "type": "string",
                    "required": False,
                    "description": "Durum süzgeci, örn. open/closed.",
                }
            },
        ),
        _spec(
            "get_matter",
            "Tek bir dosyanın detayını (müvekkil, mahkeme, durum, duruşmalar, süreler, "
            "belgeler) getir. Avukat belirli bir dosyayı sorduğunda kullan. matter_id'yi "
            "list_matters'tan al.",
            {"matter_id": {"type": "integer", "required": True, "description": "Dosya no (id)."}},
        ),
        _spec(
            "prepare_hearing",
            "Bir dosya için DURUŞMA HAZIRLIK paketini topla: dosya bilgileri + yaklaşan "
            "duruşma/süreler + hazır belgeler. Avukat 'şu dosyada duruşmaya hazırlan', "
            "'duruşma için özet çıkar' dediğinde kullan; dönen paketten kısa bir hazırlık "
            "özeti yaz.",
            {"matter_id": {"type": "integer", "required": True, "description": "Dosya no (id)."}},
        ),
        _spec(
            "create_matter",
            "Yeni bir dosya/dava aç. Avukat 'X için dosya aç', 'yeni dava' dediğinde kullan. "
            "client_name verirsen müvekkil otomatik bağlanır/oluşturulur.",
            {
                "title": {
                    "type": "string",
                    "required": True,
                    "description": "Dosya başlığı, örn. 'Ahmet Y. – Boşanma'.",
                },
                "client_name": {
                    "type": "string",
                    "required": False,
                    "description": "Müvekkil adı (varsa).",
                },
                "file_no": {
                    "type": "string",
                    "required": False,
                    "description": "Esas/dosya no (varsa).",
                },
                "court": {"type": "string", "required": False, "description": "Mahkeme (varsa)."},
                "case_type": {
                    "type": "string",
                    "required": False,
                    "description": "Dava türü (varsa).",
                },
                "counterparty": {
                    "type": "string",
                    "required": False,
                    "description": "Karşı taraf (varsa).",
                },
            },
        ),
        _spec(
            "update_matter",
            "Mevcut bir dosyanın alanlarını güncelle (durum, mahkeme, esas no, karşı taraf, "
            "not vb.).",
            {
                "matter_id": {"type": "integer", "required": True, "description": "Dosya no (id)."},
                "status": {
                    "type": "string",
                    "required": False,
                    "description": "Yeni durum, örn. open/closed.",
                },
                "file_no": {"type": "string", "required": False, "description": "Esas/dosya no."},
                "court": {"type": "string", "required": False, "description": "Mahkeme."},
                "case_type": {"type": "string", "required": False, "description": "Dava türü."},
                "counterparty": {
                    "type": "string",
                    "required": False,
                    "description": "Karşı taraf.",
                },
                "notes": {"type": "string", "required": False, "description": "Serbest not."},
            },
        ),
        _spec(
            "list_hearings",
            "Duruşmaları listele. matter_id verirsen o dosyanın tüm duruşmaları; vermezsen "
            "upcoming_days içindeki ofis geneli yaklaşan duruşmalar döner.",
            {
                "matter_id": {
                    "type": "integer",
                    "required": False,
                    "description": "Dosya no (id). Boşsa ofis geneli yaklaşanlar.",
                },
                "upcoming_days": {
                    "type": "integer",
                    "required": False,
                    "description": "Kaç günlük pencere (varsayılan 14).",
                },
            },
        ),
        _spec(
            "add_hearing",
            "Bir dosyaya duruşma ekle. Avukat 'şu dosyaya … tarihinde duruşma ekle' "
            "dediğinde kullan.",
            {
                "matter_id": {"type": "integer", "required": True, "description": "Dosya no (id)."},
                "date": {"type": "string", "required": True, "description": "Tarih, YYYY-MM-DD."},
                "time": {
                    "type": "string",
                    "required": False,
                    "description": "Saat, HH:MM (varsa).",
                },
                "location": {
                    "type": "string",
                    "required": False,
                    "description": "Yer/salon (varsa).",
                },
                "type": {
                    "type": "string",
                    "required": False,
                    "description": "Tür (örn. ön inceleme, tahkikat).",
                },
                "notes": {"type": "string", "required": False, "description": "Not (varsa)."},
            },
        ),
        _spec(
            "upcoming_deadlines",
            "Ofis genelinde yaklaşan AJANDAYI getir — duruşmalar VE süreler birlikte, tarihe göre "
            "sıralı. Avukat 'yaklaşan duruşmalar/süreler', 'bu hafta neler var' dediğinde kullan. "
            "Her öğe kind='hearing'|'deadline' ile işaretlidir.",
            {
                "days": {
                    "type": "integer",
                    "required": False,
                    "description": "Kaç günlük pencere (varsayılan 14).",
                }
            },
        ),
        _spec(
            "add_deadline",
            "Bir dosyaya hukuki SÜRE ekle (ör. cevap, istinaf, temyiz süresi). Duruşmadan farklı: "
            "süre, bir işlemin yapılması gereken son tarihtir. Avukat 'şu dosyada cevap süresi … "
            "tarihinde doluyor' dediğinde kullan.",
            {
                "matter_id": {"type": "integer", "required": True, "description": "Dosya no (id)."},
                "due_date": {
                    "type": "string",
                    "required": True,
                    "description": "Son tarih, YYYY-MM-DD.",
                },
                "title": {
                    "type": "string",
                    "required": True,
                    "description": "Süre adı, örn. 'Cevap süresi'.",
                },
                "type": {
                    "type": "string",
                    "required": False,
                    "description": "Tür (cevap/istinaf/temyiz vb.).",
                },
                "notes": {"type": "string", "required": False, "description": "Not (varsa)."},
            },
        ),
        _spec(
            "complete_deadline",
            "Bir süreyi 'tamamlandı' olarak işaretle (artık ajandada görünmez).",
            {"deadline_id": {"type": "integer", "required": True, "description": "Süre id'si."}},
        ),
        _spec(
            "list_deadlines",
            "Süreleri listele. matter_id verirsen o dosyanın süreleri; vermezsen ofis geneli.",
            {
                "matter_id": {
                    "type": "integer",
                    "required": False,
                    "description": "Dosya no (id). Boşsa ofis geneli.",
                }
            },
        ),
        _spec(
            "list_clients",
            "Ofisin müvekkillerini listele.",
            {},
        ),
    ]

    return tools, handlers
