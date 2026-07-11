"""Create or update a user — an admin path alongside in-app self-registration.

Single-tenant: there is one Office (a singleton identity row). The first bootstrap names it (from
--office or a default); the first user created becomes the owner, later users are members.

Usage:
    python -m web.adduser <username> --office "Örnek Hukuk" --name "Ad Soyad"  # prompts for pw
    python -m web.adduser <username> --password <pw> --name "Ad Soyad"  # default office name
    python -m web.adduser <username> --disable                # deactivate + revoke sessions
    python -m web.adduser <username> --reset-2fa              # force fresh 2FA enrolment
    python -m web.adduser <username> --unlock                 # clear a brute-force lockout

Every account must complete TOTP 2FA enrolment on first login; admins never see the secret.
"""

from __future__ import annotations

import argparse
import getpass
import sys

from sqlalchemy import delete, select

from korkut.api import auth
from korkut.api.models import Office, RecoveryCode, User
from korkut.api.store import session
from korkut.core.tenancy import set_current_office


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="web.adduser", description="Create/update a Korkut user.")
    p.add_argument("username")
    p.add_argument("--password", default=None, help="if omitted, you are prompted")
    p.add_argument("--name", default=None, help="full name shown in the UI")
    p.add_argument(
        "--office",
        default=None,
        help="office name; used only on first bootstrap to name the singleton office "
        "(ignored afterwards). Defaults to '<username> ofisi'. The first user is the owner.",
    )
    p.add_argument("--disable", action="store_true", help="deactivate this user + revoke sessions")
    p.add_argument("--reset-2fa", action="store_true", help="clear 2FA so the user re-enrols")
    p.add_argument("--unlock", action="store_true", help="clear a brute-force lockout")
    args = p.parse_args(argv)

    username = args.username.strip().lower()

    with session() as s:
        user = s.scalar(select(User).where(User.username == username))

        if args.disable or args.reset_2fa or args.unlock:
            if not user:
                print(f"Kullanıcı yok: {username}", file=sys.stderr)
                return 1
            if args.disable:
                user.is_active = False
                user.token_epoch += 1
                print(f"Devre dışı + oturumlar iptal: {username}")
            if args.reset_2fa:
                user.totp_secret = None
                user.totp_confirmed_at = None
                user.totp_last_counter = 0
                user.token_epoch += 1
                s.execute(delete(RecoveryCode).where(RecoveryCode.user_id == user.id))
                print(f"2FA sıfırlandı (yeni kurulum gerekecek): {username}")
            if args.unlock:
                user.failed_attempts = 0
                user.locked_until = None
                print(f"Kilit açıldı: {username}")
            s.commit()
            return 0

        password = args.password or getpass.getpass(f"{username} için parola: ")
        if len(password) < 8:
            print("Parola en az 8 karakter olmalı.", file=sys.stderr)
            return 1

        if user:
            user.password_hash = auth.hash_secret(password)
            user.is_active = True
            user.token_epoch += 1  # a password change logs out existing sessions
            if args.name:
                user.full_name = args.name
            action = "güncellendi"
            office_name = None
        else:
            # Bootstrap the singleton office on first run; --office only names it then.
            office = s.scalar(select(Office))
            if office is None:
                office_name = (args.office or f"{username} ofisi").strip()
                office = Office(name=office_name[:200])
                s.add(office)
                s.flush()
            else:
                office_name = office.name
            # Scope inserts to this office so the new user (and any tenant rows) are stamped.
            set_current_office(office.id)
            # First user overall owns the office; later users join as "member".
            has_members = s.scalar(select(User)) is not None
            s.add(
                User(
                    role="member" if has_members else "owner",
                    username=username,
                    full_name=args.name,
                    password_hash=auth.hash_secret(password),
                )
            )
            action = "oluşturuldu"
        s.commit()

    suffix = f" — ofis: {office_name}" if office_name else ""
    print(f"Kullanıcı {action}: {username}{suffix} (ilk girişte 2FA kurulumu gerekecek)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
