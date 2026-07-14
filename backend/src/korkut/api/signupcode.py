"""Manage self-serve office-signup codes from the CLI (until the admin panel lands).

  python -m korkut.api.signupcode create [--label "..."] [--max-uses N] [--expires-days D]
  python -m korkut.api.signupcode list
  python -m korkut.api.signupcode disable <id>

Codes are GLOBAL (pre-tenant), so no office context is needed. The raw code is printed once on
create; only its hash is stored. A new firm enters it on the signup screen to create its office.
"""

from __future__ import annotations

import argparse
import secrets
import sys
from datetime import UTC, datetime, timedelta

from sqlalchemy import select

from korkut.api import auth
from korkut.api.models import SignupCode
from korkut.api.store import session


def _naive_utc() -> datetime:
    return datetime.now(UTC).replace(tzinfo=None)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="korkut.api.signupcode", description="Manage signup codes.")
    sub = p.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("create", help="mint a new code (prints it once)")
    c.add_argument("--label", default=None, help="note for the admin (e.g. who it's for)")
    c.add_argument("--max-uses", type=int, default=1, help="0 = unlimited")
    c.add_argument("--expires-days", type=int, default=None)
    sub.add_parser("list", help="list all codes")
    d = sub.add_parser("disable", help="disable a code")
    d.add_argument("id", type=int)
    args = p.parse_args(argv)

    with session() as s:
        if args.cmd == "create":
            code = secrets.token_urlsafe(9)  # short, human-shareable
            expires = (
                _naive_utc() + timedelta(days=args.expires_days) if args.expires_days else None
            )
            row = SignupCode(
                code_hash=auth.token_hash(code),
                label=args.label,
                max_uses=args.max_uses,
                expires_at=expires,
            )
            s.add(row)
            s.commit()
            print(f"kod (yalnızca bir kez gösterilir): {code}")
            print(f"  id={row.id}  max_uses={args.max_uses or '∞'}  expires={expires or '-'}")
        elif args.cmd == "list":
            rows = s.scalars(select(SignupCode).order_by(SignupCode.id)).all()
            if not rows:
                print("kayıt kodu yok")
                return 0
            for r in rows:
                state = "disabled" if r.disabled else "active"
                print(
                    f"#{r.id} {state} kullanım={r.used_count}/{r.max_uses or '∞'} "
                    f"expires={r.expires_at or '-'} label={r.label or '-'}"
                )
        elif args.cmd == "disable":
            row = s.get(SignupCode, args.id)
            if not row:
                print(f"kod yok: {args.id}", file=sys.stderr)
                return 1
            row.disabled = True
            s.commit()
            print(f"devre dışı: #{args.id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
