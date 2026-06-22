"""Search KİK (Kamu İhale Kurulu / Public Procurement Authority) board decisions.

Source: the official kik.gov.tr v2 JSON API at ``https://ekapv2.kik.gov.tr``.
No API key is required.

The v2 API splits board decisions into three families, each served by its own
endpoint under ``/b_ihalearaclari/api/KurulKararlari/``:

    * "uyusmazlik"  (disputes)    -> GetKurulKararlari
    * "duzenleyici" (regulatory)  -> GetKurulKararlariDk
    * "mahkeme"     (court)       -> GetKurulKararlariMk

The decision family is chosen via the ``karar_turu`` parameter (default
"uyusmazlik"). Each endpoint takes a small key/value criteria envelope and
returns the same record shape, so a single parser covers all three.

Why a bespoke httpx.Client instead of plain ``BaseClient``:
The KİK host serves TLS through a legacy stack that modern OpenSSL rejects
unless ``OP_LEGACY_SERVER_CONNECT`` is enabled and a broad cipher list is
allowed. It also expects per-request signed timestamp headers. Neither can be
expressed through ``BaseClient``'s default ``httpx.Client``, so this client
builds its own transport in ``__init__`` while still reusing every other piece
of ``BaseClient`` (token bucket, 429 back-pressure, cache, retry loop) by
assigning the custom client to ``self._client``. The signing headers are
generated best-effort (see ``_signing_headers``); when the optional
``cryptography`` package is absent they are simply omitted, which keeps offline
tests and basic usage working. Verify behaviour against the live API.
"""

from __future__ import annotations

import ssl
from typing import Any

import httpx

from tools._base.client import BaseClient

BASE_URL = "https://ekapv2.kik.gov.tr"

# karar_turu -> (endpoint path, request root key, response wrapper, result key)
_DECISION_TYPES: dict[str, tuple[str, str, str, str]] = {
    "uyusmazlik": (
        "/b_ihalearaclari/api/KurulKararlari/GetKurulKararlari",
        "sorgulaKurulKararlari",
        "SorgulaKurulKararlariResponse",
        "SorgulaKurulKararlariResult",
    ),
    "duzenleyici": (
        "/b_ihalearaclari/api/KurulKararlari/GetKurulKararlariDk",
        "sorgulaKurulKararlariDk",
        "SorgulaKurulKararlariDkResponse",
        "SorgulaKurulKararlariDkResult",
    ),
    "mahkeme": (
        "/b_ihalearaclari/api/KurulKararlari/GetKurulKararlariMk",
        "sorgulaKurulKararlariMk",
        "SorgulaKurulKararlariMkResponse",
        "SorgulaKurulKararlariMkResult",
    ),
}

_DOCUMENT_URL = "https://ekap.kik.gov.tr/EKAP/Vatandas/KurulKararGoster.aspx?KararId="


def _legacy_ssl_context() -> ssl.SSLContext:
    """Build an SSL context tolerant of the KİK host's legacy TLS stack."""
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    if hasattr(ssl, "OP_LEGACY_SERVER_CONNECT"):
        ctx.options |= ssl.OP_LEGACY_SERVER_CONNECT
    try:
        ctx.set_ciphers("ALL:!aNULL:!eNULL:!EXPORT:!DES:!RC4:!MD5:!PSK:!SRP:!CAMELLIA")
    except ssl.SSLError:
        pass
    return ctx


class KikClient(BaseClient):
    """Client for the kik.gov.tr v2 board-decision API."""

    base_url = BASE_URL
    default_headers = {
        **BaseClient.default_headers,
        "Accept": "application/json",
        "Accept-Language": "tr",
        "Content-Type": "application/json",
        "Origin": BASE_URL,
        "Referer": f"{BASE_URL}/sorgulamalar/kurul-kararlari",
        "api-version": "v1",
    }

    def __init__(self, *, timeout: float = 60.0, **kwargs: Any) -> None:
        super().__init__(timeout=timeout, **kwargs)
        # Replace BaseClient's default transport with one that tolerates the
        # KİK host's legacy TLS; everything else (bucket, cache, retries) stays.
        self._client.close()
        self._client = httpx.Client(
            base_url=self.base_url,
            headers=self.default_headers,
            timeout=timeout,
            verify=_legacy_ssl_context(),
        )

    @staticmethod
    def _signing_headers() -> dict[str, str]:
        """Best-effort per-request signing headers expected by the KİK gateway.

        The live gateway signs a random GUID and the current epoch-millis
        timestamp with AES-192-CBC and rejects stale/missing values. We generate
        them when ``cryptography`` is available; otherwise we return no headers
        and rely on the server (or a mocked transport) to accept the request.
        Verify against the live API before relying on this path.
        """
        try:
            import base64
            import os
            import time
            import uuid

            from cryptography.hazmat.backends import default_backend
            from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        except Exception:  # noqa: BLE001 — optional dependency / unsupported env
            return {}

        signing_key = b"Qm2LtXR0aByP69vZNKef4wMJ"  # AES-192 key from the public web app
        iv = os.urandom(16)

        def sign(plaintext: str) -> str:
            encryptor = Cipher(
                algorithms.AES(signing_key), modes.CBC(iv), backend=default_backend()
            ).encryptor()
            data = plaintext.encode("utf-8")
            pad = 16 - (len(data) % 16)
            data += bytes([pad] * pad)
            return base64.b64encode(encryptor.update(data) + encryptor.finalize()).decode("ascii")

        guid = str(uuid.uuid4())
        return {
            "X-Custom-Request-Guid": guid,
            "X-Custom-Request-R8id": sign(guid),
            "X-Custom-Request-Siv": base64.b64encode(iv).decode("ascii"),
            "X-Custom-Request-Ts": sign(str(int(time.time() * 1000))),
        }

    @staticmethod
    def _criteria(
        query: str,
        karar_no: str,
        basvuran: str,
        idare_adi: str,
        date_start: str,
        date_end: str,
    ) -> list[dict[str, str]]:
        fields = (
            ("KararMetni", query),
            ("KararNo", karar_no),
            ("BasvuranAdi", basvuran),
            ("IdareAdi", idare_adi),
            ("BaslangicTarihi", date_start),
            ("BitisTarihi", date_end),
        )
        pairs = [{"key": k, "value": v} for k, v in fields if v]
        return pairs or [{"key": "KararMetni", "value": ""}]

    def search(
        self,
        karar_turu: str,
        query: str,
        *,
        limit: int = 10,
        page: int = 1,
        karar_no: str = "",
        basvuran: str = "",
        idare_adi: str = "",
        date_start: str = "",
        date_end: str = "",
    ) -> dict[str, Any]:
        if karar_turu not in _DECISION_TYPES:
            valid = ", ".join(_DECISION_TYPES)
            raise ValueError(f"karar_turu must be one of: {valid}")

        endpoint, root_key, wrapper_key, result_key = _DECISION_TYPES[karar_turu]
        payload = {
            root_key: {
                "keyValuePairs": {
                    "keyValueOfstringanyType": self._criteria(
                        query, karar_no, basvuran, idare_adi, date_start, date_end
                    )
                }
            }
        }

        # Signing headers carry a per-request timestamp, so refresh them on the
        # shared client immediately before dispatching through BaseClient's
        # rate-limited/cached request path.
        self._client.headers.update(self._signing_headers())
        body = self.post_json(endpoint, payload)

        result = ((body or {}).get(wrapper_key) or {}).get(result_key) or {}
        error_code = result.get("hataKodu")
        if error_code and str(error_code) != "0":
            raise RuntimeError(
                f"KİK API error {error_code}: {result.get('hataMesaji') or 'unknown'}"
            )

        records: list[dict[str, Any]] = []
        for group in result.get("KurulKararTutanakDetayListesi") or []:
            records.extend(group.get("kurulKararTutanakDetayi") or [])

        start = max(0, (page - 1) * limit)
        window = records[start : start + max(1, limit)]
        results = [self._to_result(r, karar_turu) for r in window]
        return {
            "total": len(records),
            "page": page,
            "count": len(results),
            "results": results,
        }

    @staticmethod
    def _to_result(record: dict[str, Any], karar_turu: str) -> dict[str, Any]:
        decision_id = record.get("gundemMaddesiId") or ""
        title = record.get("basvuruKonusu") or record.get("idareAdi") or record.get("kararNo") or ""
        url = f"{_DOCUMENT_URL}{decision_id}" if decision_id else ""
        return {
            "id": str(decision_id),
            "title": title,
            "decisionType": karar_turu,
            "date": record.get("kararTarihi") or "",
            "url": url,
        }


def run_tool(
    query: str,
    karar_turu: str = "uyusmazlik",
    limit: int = 10,
    page: int = 1,
    karar_no: str = "",
    basvuran: str = "",
    idare_adi: str = "",
    date_start: str = "",
    date_end: str = "",
) -> dict[str, Any]:
    """Search KİK board decisions.

    query        : free-text decision-content search (KararMetni)
    karar_turu   : decision family — "uyusmazlik" (default), "duzenleyici", "mahkeme"
    limit        : results per page (>= 1)
    page         : 1-indexed page
    karar_no     : optional decision number, e.g. "2025/UH.II-1801"
    basvuran     : optional applicant name
    idare_adi    : optional administration (procuring entity) name
    date_start   : optional start date (YYYY-MM-DD)
    date_end     : optional end date (YYYY-MM-DD)

    Returns {"total", "page", "count", "results": [{id, title, decisionType, date, url}]}.
    """
    with KikClient() as client:
        return client.search(
            karar_turu,
            query,
            limit=limit,
            page=page,
            karar_no=karar_no,
            basvuran=basvuran,
            idare_adi=idare_adi,
            date_start=date_start,
            date_end=date_end,
        )
