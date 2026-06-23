"""Create or update an office user — the only way an account is born (no self-service signup).

Usage:
    python -m web.adduser <username> [--name "Ad Soyad"]      # prompts for the password
    python -m web.adduser <username> --password <pw> --name "Ad Soyad"
    python -m web.adduser <username> --disable                # deactivate an account
"""

from __future__ import annotations

import argparse
import getpass
import sys

from sqlalchemy import select

from web import auth
from web.models import User
from web.store import session


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="web.adduser", description="Create/update a Korkut user.")
    p.add_argument("username")
    p.add_argument("--password", default=None, help="if omitted, you are prompted")
    p.add_argument("--name", default=None, help="full name shown in the UI")
    p.add_argument("--disable", action="store_true", help="deactivate this user")
    args = p.parse_args(argv)

    username = args.username.strip().lower()

    with session() as s:
        user = s.scalar(select(User).where(User.username == username))

        if args.disable:
            if not user:
                print(f"Kullanıcı yok: {username}", file=sys.stderr)
                return 1
            user.is_active = False
            s.commit()
            print(f"Devre dışı bırakıldı: {username}")
            return 0

        password = args.password or getpass.getpass(f"{username} için parola: ")
        if len(password) < 8:
            print("Parola en az 8 karakter olmalı.", file=sys.stderr)
            return 1

        if user:
            user.password_hash = auth.hash_password(password)
            user.is_active = True
            if args.name:
                user.full_name = args.name
            action = "güncellendi"
        else:
            s.add(User(
                username=username,
                full_name=args.name,
                password_hash=auth.hash_password(password),
            ))
            action = "oluşturuldu"
        s.commit()

    print(f"Kullanıcı {action}: {username}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
