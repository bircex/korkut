"""FastAPI application — the chat front door for Korkut, with hardened auth + mandatory 2FA.

Login is two steps and a full session cookie is issued ONLY after 2FA completes:

  POST /api/login      {username,password}
        password ok, no 2FA yet   -> 200 {step:"enroll"} + short-lived enrol ticket cookie
        password ok, 2FA enrolled -> 200 {step:"mfa"}    + short-lived mfa  ticket cookie
  GET  /api/2fa/provision         (enrol ticket)  -> {secret, otpauth_uri, qr_svg}
  POST /api/2fa/enroll {code}      (enrol ticket)  -> {recovery_codes:[...]} + session cookie
  POST /api/2fa/verify {code}      (mfa ticket)    -> session cookie  (TOTP or recovery code)
  POST /api/logout                 -> revokes ALL sessions (bumps token_epoch)
  GET  /api/me / /api/history / POST /api/chat     -> require a full session

Defences: per-user lockout (persisted), per-IP rate limit, constant-time + dummy-hash to blunt
username enumeration, anti-replay on TOTP, server-side session revocation, audited events,
HttpOnly + SameSite=Strict (+ Secure behind TLS) cookies. See SECURITY.md.
"""

from __future__ import annotations

import os
import time
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

from fastapi import Cookie, FastAPI, HTTPException, Request, Response
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.orm import Session

from korkut import agent
from web import auth, totp
from web.models import AuthEvent, Message, RecoveryCode, User
from web.store import session

_STATIC = Path(__file__).resolve().parent / "static"
_HISTORY_TURNS = 20
_ISSUER = os.getenv("KORKUT_ISSUER", "Cankar Hukuk")

# Lockout policy.
_MAX_FAILED = 5
_LOCK_MINUTES = 15
# Per-IP rate limit (in-memory; the persisted per-user lockout is the durable layer).
_IP_WINDOW = 300
_IP_MAX = 15
_TICKET_COOKIE = "korkut_ticket"

# Pre-computed so a login for a non-existent user does the same PBKDF2 work as a real one.
_DUMMY_HASH = auth.hash_secret("x" * 24)

app = FastAPI(title="Korkut — Cankar Hukuk", docs_url=None, redoc_url=None)

_ip_hits: dict[str, list[float]] = defaultdict(list)


# ---------------- request models ----------------

class LoginBody(BaseModel):
    username: str
    password: str


class CodeBody(BaseModel):
    code: str


class ChatBody(BaseModel):
    text: str


# ---------------- helpers ----------------

def _utcnow() -> datetime:
    """Naive UTC — matches how SQLite/Postgres round-trip the timestamp columns."""
    return datetime.now(timezone.utc).replace(tzinfo=None)


def _secure_cookies() -> bool:
    return os.getenv("KORKUT_SECURE_COOKIES", "0") == "1"


def _client_ip(request: Request) -> str:
    if os.getenv("KORKUT_TRUST_PROXY", "0") == "1":
        fwd = request.headers.get("x-forwarded-for")
        if fwd:
            return fwd.split(",")[0].strip()
    return request.client.host if request.client else "unknown"


def _audit(s: Session, event: str, *, username: str | None, ip: str, detail: str | None = None) -> None:
    s.add(AuthEvent(event=event, username=username, ip=ip, detail=detail))
    s.commit()


def _ip_allowed(ip: str) -> bool:
    now = time.time()
    hits = [t for t in _ip_hits[ip] if now - t < _IP_WINDOW]
    hits.append(now)
    _ip_hits[ip] = hits
    return len(hits) <= _IP_MAX


def _is_locked(user: User) -> bool:
    return user.locked_until is not None and user.locked_until > _utcnow()


def _register_failure(s: Session, user: User) -> None:
    user.failed_attempts += 1
    if user.failed_attempts >= _MAX_FAILED:
        user.locked_until = _utcnow() + timedelta(minutes=_LOCK_MINUTES)
    s.commit()


def _reset_failures(s: Session, user: User) -> None:
    user.failed_attempts = 0
    user.locked_until = None
    s.commit()


def _set_session_cookie(response: Response, user: User) -> None:
    token = auth.make_session_token(user.id, user.token_epoch, now=int(time.time()))
    response.set_cookie(
        auth.COOKIE_NAME, token,
        max_age=auth.MAX_AGE, httponly=True, samesite="strict",
        secure=_secure_cookies(), path="/",
    )


def _set_ticket_cookie(response: Response, purpose: str, user: User) -> None:
    ticket = auth.make_ticket(purpose, user.id, now=int(time.time()))
    response.set_cookie(
        _TICKET_COOKIE, ticket,
        max_age=auth.TICKET_MAX_AGE, httponly=True, samesite="strict",
        secure=_secure_cookies(), path="/",
    )


def _clear_ticket(response: Response) -> None:
    response.delete_cookie(_TICKET_COOKIE, path="/")


def _current_user(s: Session, token: str | None) -> User | None:
    if not token:
        return None
    parsed = auth.read_session_token(token, now=int(time.time()))
    if parsed is None:
        return None
    uid, epoch = parsed
    user = s.get(User, uid)
    if user and user.is_active and user.has_2fa and user.token_epoch == epoch:
        return user
    return None


def _require_user(s: Session, token: str | None) -> User:
    user = _current_user(s, token)
    if user is None:
        raise HTTPException(status_code=401, detail="Oturum gerekli")
    return user


def _ticket_user(s: Session, ticket: str | None, purpose: str) -> User:
    if not ticket:
        raise HTTPException(status_code=401, detail="Doğrulama oturumu yok, baştan giriş yapın")
    uid = auth.read_ticket(ticket, purpose, now=int(time.time()))
    if uid is None:
        raise HTTPException(status_code=401, detail="Doğrulama süresi doldu, baştan giriş yapın")
    user = s.get(User, uid)
    if not user or not user.is_active:
        raise HTTPException(status_code=401, detail="Geçersiz oturum")
    return user


# ---------------- auth endpoints ----------------

@app.post("/api/login")
def login(body: LoginBody, request: Request, response: Response):
    ip = _client_ip(request)
    if not _ip_allowed(ip):
        raise HTTPException(status_code=429, detail="Çok fazla deneme. Biraz sonra tekrar deneyin.")

    username = body.username.strip().lower()
    with session() as s:
        user = s.scalar(select(User).where(User.username == username))

        if user and _is_locked(user):
            _audit(s, "login_locked", username=username, ip=ip)
            raise HTTPException(status_code=429, detail="Hesap geçici olarak kilitli. Daha sonra deneyin.")

        # Constant work whether or not the user exists (blunts username enumeration).
        stored = user.password_hash if user else _DUMMY_HASH
        ok = auth.verify_secret(body.password, stored)

        if not user or not user.is_active or not ok:
            if user:
                _register_failure(s, user)
            _audit(s, "login_fail", username=username, ip=ip)
            raise HTTPException(status_code=401, detail="Kullanıcı adı veya parola hatalı")

        _reset_failures(s, user)
        if user.has_2fa:
            _set_ticket_cookie(response, auth.PURPOSE_MFA, user)
            _audit(s, "login_password_ok", username=username, ip=ip, detail="mfa")
            return {"step": "mfa"}
        _set_ticket_cookie(response, auth.PURPOSE_ENROLL, user)
        _audit(s, "login_password_ok", username=username, ip=ip, detail="enroll")
        return {"step": "enroll"}


@app.get("/api/2fa/provision")
def provision(korkut_ticket: str | None = Cookie(default=None)):
    with session() as s:
        user = _ticket_user(s, korkut_ticket, auth.PURPOSE_ENROLL)
        if user.has_2fa:
            raise HTTPException(status_code=409, detail="2FA zaten kurulu")
        # Generate (or rotate) the pending secret; it is not active until /enroll confirms.
        user.totp_secret = totp.new_secret()
        s.commit()
        uri = totp.provisioning_uri(user.totp_secret, user.username, _ISSUER)
        return {"secret": user.totp_secret, "otpauth_uri": uri, "qr": _qr_data_uri(uri)}


@app.post("/api/2fa/enroll")
def enroll(body: CodeBody, request: Request, response: Response,
           korkut_ticket: str | None = Cookie(default=None)):
    ip = _client_ip(request)
    if not _ip_allowed(ip):
        raise HTTPException(status_code=429, detail="Çok fazla deneme.")
    with session() as s:
        user = _ticket_user(s, korkut_ticket, auth.PURPOSE_ENROLL)
        if user.has_2fa:
            raise HTTPException(status_code=409, detail="2FA zaten kurulu")
        if not user.totp_secret:
            raise HTTPException(status_code=400, detail="Önce QR kodu oluşturun")

        counter = totp.verify(user.totp_secret, body.code)
        if counter is None:
            _audit(s, "enroll_fail", username=user.username, ip=ip)
            raise HTTPException(status_code=401, detail="Kod hatalı")

        user.totp_confirmed_at = _utcnow()
        user.totp_last_counter = counter
        user.token_epoch += 1  # invalidate anything older
        codes = auth.new_recovery_codes()
        for c in codes:
            s.add(RecoveryCode(user_id=user.id, code_hash=auth.hash_secret(c)))
        s.commit()
        _audit(s, "enroll_ok", username=user.username, ip=ip)
        _clear_ticket(response)
        _set_session_cookie(response, user)
        return {"username": user.username, "full_name": user.full_name, "recovery_codes": codes}


@app.post("/api/2fa/verify")
def verify_2fa(body: CodeBody, request: Request, response: Response,
               korkut_ticket: str | None = Cookie(default=None)):
    ip = _client_ip(request)
    if not _ip_allowed(ip):
        raise HTTPException(status_code=429, detail="Çok fazla deneme.")
    with session() as s:
        user = _ticket_user(s, korkut_ticket, auth.PURPOSE_MFA)
        if _is_locked(user):
            raise HTTPException(status_code=429, detail="Hesap geçici olarak kilitli.")

        code = body.code.strip()
        counter = totp.verify(user.totp_secret or "", code)
        used_recovery = False
        if counter is not None:
            if counter <= user.totp_last_counter:  # anti-replay
                _register_failure(s, user)
                _audit(s, "mfa_replay", username=user.username, ip=ip)
                raise HTTPException(status_code=401, detail="Kod hatalı")
            user.totp_last_counter = counter
        else:
            used_recovery = _consume_recovery_code(s, user, code)
            if not used_recovery:
                _register_failure(s, user)
                _audit(s, "mfa_fail", username=user.username, ip=ip)
                raise HTTPException(status_code=401, detail="Kod hatalı")

        _reset_failures(s, user)
        s.commit()
        _audit(s, "mfa_ok", username=user.username, ip=ip,
               detail="recovery" if used_recovery else "totp")
        _clear_ticket(response)
        _set_session_cookie(response, user)
        return {"username": user.username, "full_name": user.full_name,
                "used_recovery": used_recovery}


@app.post("/api/logout")
def logout(response: Response, korkut_session: str | None = Cookie(default=None)):
    with session() as s:
        user = _current_user(s, korkut_session)
        if user:
            user.token_epoch += 1  # revoke every outstanding session for this user
            s.commit()
            _audit(s, "logout", username=user.username, ip="-")
    response.delete_cookie(auth.COOKIE_NAME, path="/")
    return {"ok": True}


@app.get("/api/me")
def me(korkut_session: str | None = Cookie(default=None)):
    with session() as s:
        user = _require_user(s, korkut_session)
        return {"username": user.username, "full_name": user.full_name}


@app.get("/api/coverage")
def coverage_stats(korkut_session: str | None = Cookie(default=None)):
    """How many judicial decisions Korkut can search + the source list (measured live)."""
    with session() as s:
        _require_user(s, korkut_session)
    from web import coverage
    return coverage.summary()


# ---------------- chat endpoints ----------------

@app.get("/api/history")
def history(korkut_session: str | None = Cookie(default=None)):
    with session() as s:
        user = _require_user(s, korkut_session)
        rows = s.scalars(
            select(Message).where(Message.user_id == user.id).order_by(Message.id)
        ).all()
        return [{"role": m.role, "content": m.content} for m in rows]


@app.post("/api/chat")
def chat(body: ChatBody, korkut_session: str | None = Cookie(default=None)):
    with session() as s:
        user = _require_user(s, korkut_session)
        text = body.text.strip()
        if not text:
            raise HTTPException(status_code=400, detail="Boş mesaj")
        rows = s.scalars(
            select(Message).where(Message.user_id == user.id).order_by(Message.id)
        ).all()
        history_msgs = [{"role": m.role, "content": m.content} for m in rows[-_HISTORY_TURNS:]]
        s.add(Message(user_id=user.id, role="user", content=text))
        s.commit()
        uid = user.id

    reply = agent.answer(text, history=history_msgs)

    with session() as s:
        s.add(Message(user_id=uid, role="assistant", content=reply))
        s.commit()
    return {"reply": reply}


# ---------------- recovery codes / QR ----------------

def _consume_recovery_code(s: Session, user: User, code: str) -> bool:
    code = code.strip().lower()
    rows = s.scalars(
        select(RecoveryCode).where(RecoveryCode.user_id == user.id, RecoveryCode.used_at.is_(None))
    ).all()
    for rc in rows:
        if auth.verify_secret(code, rc.code_hash):
            rc.used_at = _utcnow()
            s.commit()
            return True
    return False


def _qr_data_uri(data: str) -> str:
    # PNG data URI (segno's pure-Python PNG writer). A raster scales crisply in an <img> and
    # avoids SVG viewBox/scaling pitfalls that can crop the code and break scanning.
    try:
        import segno
        return segno.make(data, error="m").png_data_uri(scale=8, border=4,
                                                         dark="#000000", light="#ffffff")
    except Exception:
        return ""  # the otpauth_uri + secret are always returned for manual entry


# ---------------- static SPA ----------------

@app.get("/")
def index():
    return FileResponse(_STATIC / "index.html")


if (_STATIC / "assets").exists():
    app.mount("/assets", StaticFiles(directory=_STATIC / "assets"), name="assets")
