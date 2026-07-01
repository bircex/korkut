"""Data access for the finance domain (fatura / masraf / cari).

Mirrors `matters_store`: shapers + queries in one place, shared by the `/api/finance` endpoints.
Money is stored as `Numeric` and shaped to plain floats for JSON. Real e-SMM/e-fatura submission
(GİB) is out of scope — this is the office's own bookkeeping.
"""

from __future__ import annotations

from decimal import Decimal

from sqlalchemy import select
from sqlalchemy.orm import Session

from korkut.api.models import Expense, Invoice

_ISSUED = ("issued", "paid")  # statuses that count as billed toward the client


def to_amount(value: object) -> Decimal:
    """Parse a client-supplied amount into a 2-decimal Decimal (0 on junk)."""
    try:
        return Decimal(str(value)).quantize(Decimal("0.01"))
    except (ArithmeticError, TypeError, ValueError):
        return Decimal("0.00")


def _f(value: object) -> float:
    return float(value or 0)


def invoice_brief(i: Invoice) -> dict:
    net = _f(i.amount)
    gross = round(net * (1 + _f(i.vat_rate) / 100), 2)
    return {
        "id": i.id,
        "client_id": i.client_id,
        "matter_id": i.matter_id,
        "amount": net,
        "vat_rate": _f(i.vat_rate),
        "gross": gross,
        "description": i.description,
        "status": i.status,
        "issued_at": i.issued_at.isoformat() if i.issued_at else None,
    }


def expense_brief(e: Expense) -> dict:
    return {
        "id": e.id,
        "matter_id": e.matter_id,
        "client_id": e.client_id,
        "amount": _f(e.amount),
        "kind": e.kind,
        "description": e.description,
        "spent_at": e.spent_at.isoformat() if e.spent_at else None,
    }


def list_invoices(
    s: Session, client_id: int | None = None, matter_id: int | None = None
) -> list[Invoice]:
    q = select(Invoice).order_by(Invoice.created_at.desc())
    if client_id is not None:
        q = q.where(Invoice.client_id == client_id)
    if matter_id is not None:
        q = q.where(Invoice.matter_id == matter_id)
    return list(s.scalars(q))


def list_expenses(
    s: Session, client_id: int | None = None, matter_id: int | None = None
) -> list[Expense]:
    q = select(Expense).order_by(Expense.created_at.desc())
    if client_id is not None:
        q = q.where(Expense.client_id == client_id)
    if matter_id is not None:
        q = q.where(Expense.matter_id == matter_id)
    return list(s.scalars(q))


def summary(s: Session, client_id: int | None = None) -> dict:
    """A simple current-account (cari) roll-up: billed, collected, outstanding, expenses."""
    invoices = list_invoices(s, client_id=client_id)
    expenses = list_expenses(s, client_id=client_id)
    invoiced = sum(_f(i.amount) for i in invoices if i.status in _ISSUED)
    paid = sum(_f(i.amount) for i in invoices if i.status == "paid")
    total_expenses = sum(_f(e.amount) for e in expenses)
    return {
        "invoiced": round(invoiced, 2),
        "paid": round(paid, 2),
        "outstanding": round(invoiced - paid, 2),
        "expenses": round(total_expenses, 2),
    }
