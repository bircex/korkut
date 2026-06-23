"""FastAPI application — the chat front door for Korkut.

Routes:
  GET  /                static single-page app (login + chat)
  POST /api/login       {username, password} -> sets the signed session cookie
  POST /api/logout      clears the cookie
  GET  /api/me          current user, or 401
  GET  /api/history     this user's past messages with Korkut
  POST /api/chat        {text} -> Korkut's reply (runs the tool-calling brain)

There is no signup route by design: accounts come only from `python -m web.adduser`.
Chat handlers are sync `def` so FastAPI runs them in a worker thread — the brain's
tool-calling loop is blocking and must not stall the event loop.
"""

from __future__ import annotations

import time
from pathlib import Path

from fastapi import Cookie, FastAPI, HTTPException, Response
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from sqlalchemy import select

from korkut import agent
from web import auth
from web.models import Message, User
from web.store import session

_STATIC = Path(__file__).resolve().parent / "static"
_HISTORY_TURNS = 20  # how many past messages to feed back to the brain as context

app = FastAPI(title="Korkut — Cankar Hukuk", docs_url=None, redoc_url=None)


class LoginBody(BaseModel):
    username: str
    password: str


class ChatBody(BaseModel):
    text: str


def _current_user(token: str | None) -> User | None:
    if not token:
        return None
    uid = auth.read_session_token(token, now=int(time.time()))
    if uid is None:
        return None
    with session() as s:
        user = s.get(User, uid)
        if user and user.is_active:
            return user
    return None


def _require_user(token: str | None) -> User:
    user = _current_user(token)
    if user is None:
        raise HTTPException(status_code=401, detail="Oturum gerekli")
    return user


@app.post("/api/login")
def login(body: LoginBody, response: Response):
    with session() as s:
        user = s.scalar(select(User).where(User.username == body.username.strip().lower()))
        if not user or not user.is_active or not auth.verify_password(body.password, user.password_hash):
            raise HTTPException(status_code=401, detail="Kullanıcı adı veya parola hatalı")
        token = auth.make_session_token(user.id, now=int(time.time()))
        response.set_cookie(
            auth.COOKIE_NAME, token,
            max_age=auth.MAX_AGE, httponly=True, samesite="lax", path="/",
        )
        return {"username": user.username, "full_name": user.full_name}


@app.post("/api/logout")
def logout(response: Response):
    response.delete_cookie(auth.COOKIE_NAME, path="/")
    return {"ok": True}


@app.get("/api/me")
def me(korkut_session: str | None = Cookie(default=None)):
    user = _require_user(korkut_session)
    return {"username": user.username, "full_name": user.full_name}


@app.get("/api/history")
def history(korkut_session: str | None = Cookie(default=None)):
    user = _require_user(korkut_session)
    with session() as s:
        rows = s.scalars(
            select(Message).where(Message.user_id == user.id).order_by(Message.id)
        ).all()
        return [{"role": m.role, "content": m.content} for m in rows]


@app.post("/api/chat")
def chat(body: ChatBody, korkut_session: str | None = Cookie(default=None)):
    user = _require_user(korkut_session)
    text = body.text.strip()
    if not text:
        raise HTTPException(status_code=400, detail="Boş mesaj")

    with session() as s:
        rows = s.scalars(
            select(Message).where(Message.user_id == user.id).order_by(Message.id)
        ).all()
        recent = rows[-_HISTORY_TURNS:]
        history_msgs = [{"role": m.role, "content": m.content} for m in recent]

        s.add(Message(user_id=user.id, role="user", content=text))
        s.commit()

    reply = agent.answer(text, history=history_msgs)

    with session() as s:
        s.add(Message(user_id=user.id, role="assistant", content=reply))
        s.commit()

    return {"reply": reply}


@app.get("/")
def index():
    return FileResponse(_STATIC / "index.html")


app.mount("/static", StaticFiles(directory=_STATIC), name="static")
