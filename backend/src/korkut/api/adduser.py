"""Provision an office + user from the admin side (Google OIDC binds the account on first sign-in).

Creates the office on first run (``--office`` names it) and its owner; later users on that office
join as members. A user is pre-provisioned by email; their Google ``sub`` binds on first sign-in.
Pass ``--google-sub`` to bind a known account immediately.

Usage:
    python -m korkut.api.adduser <email> --office "Örnek Hukuk" --name "Ad Soyad"
    python -m korkut.api.adduser <email> --username avukat --name "Ad Soyad"
    python -m korkut.api.adduser <email> --disable        # deactivate + revoke sessions
"""

from __future__ import annotations

import argparse
import sys

from sqlalchemy import select

from korkut.api.models import Office, User
from korkut.api.store import session
from korkut.core.tenancy import set_current_office


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="korkut.api.adduser", description="Provision a Korkut user.")
    p.add_argument("email")
    p.add_argument(
        "--username", default=None, help="display handle; defaults to the email local-part"
    )
    p.add_argument("--name", default=None, help="full name shown in the UI")
    p.add_argument("--google-sub", default=None, help="bind this Google subject id now")
    p.add_argument(
        "--office",
        default=None,
        help="office name; used only on first bootstrap to name the new office "
        "(ignored afterwards). Defaults to '<username> ofisi'. The first user is the owner.",
    )
    p.add_argument("--disable", action="store_true", help="deactivate this user + revoke sessions")
    args = p.parse_args(argv)

    email = args.email.strip().lower()
    username = (args.username or email.split("@")[0]).strip().lower()

    # Resolve (or bootstrap) the office FIRST and set the tenant context before any tenant query;
    # otherwise the filter + RLS scope every query to nothing. Office is the tenant root (unscoped).
    with session() as s:
        office = s.scalar(select(Office))
        if office is None:
            if args.disable:
                print(f"Kullanıcı yok: {email}", file=sys.stderr)
                return 1
            office = Office(name=(args.office or f"{username} ofisi").strip()[:200])
            s.add(office)
            s.commit()
        office_id, office_display = office.id, office.name
    set_current_office(office_id)

    with session() as s:
        # email is a global unique, so look the user up with the tenant filter bypassed
        user = s.scalar(
            select(User).where(User.email == email).execution_options(skip_tenant_filter=True)
        )

        if args.disable:
            if not user:
                print(f"Kullanıcı yok: {email}", file=sys.stderr)
                return 1
            user.is_active = False
            user.token_epoch += 1
            s.commit()
            print(f"Devre dışı + oturumlar iptal: {email}")
            return 0

        if user:
            user.is_active = True
            if args.name:
                user.full_name = args.name
            if args.google_sub:
                user.google_sub = args.google_sub
            action = "güncellendi"
            office_name = None
        else:
            # First user overall owns the office; later users join as "member".
            has_members = s.scalar(select(User)) is not None
            s.add(
                User(
                    role="member" if has_members else "owner",
                    email=email,
                    username=username,
                    full_name=args.name,
                    google_sub=args.google_sub,
                )
            )
            action = "oluşturuldu"
            office_name = office_display
        s.commit()

    suffix = f", ofis: {office_name}" if office_name else ""
    print(f"Kullanıcı {action}: {email}{suffix} (Google ile ilk girişte hesap bağlanır)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
