"""unify calendar events

Replace the separate `hearings` and `deadlines` tables with a single `calendar_events` table
(see korkut.api.models.CalendarEvent). The app is clean-install with no production data yet, so the
old tables are simply dropped rather than migrated row-by-row.

Revision ID: b7e2c4a19f30
Revises: b8945f4948e1
Create Date: 2026-07-01 12:00:00.000000

"""
from __future__ import annotations

from collections.abc import Sequence

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'b7e2c4a19f30'
down_revision: str | None = 'b8945f4948e1'
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.drop_table('hearings')
    op.drop_table('deadlines')

    op.create_table(
        'calendar_events',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('category', sa.String(length=20), nullable=False),
        sa.Column('type', sa.String(length=100), nullable=True),
        sa.Column('title', sa.String(length=200), nullable=False),
        sa.Column('start_at', sa.DateTime(), nullable=False),
        sa.Column('end_at', sa.DateTime(), nullable=True),
        sa.Column('all_day', sa.Boolean(), nullable=False),
        sa.Column('matter_id', sa.Integer(), nullable=True),
        sa.Column('client_id', sa.Integer(), nullable=True),
        sa.Column('assignee_id', sa.Integer(), nullable=True),
        sa.Column('location', sa.String(length=200), nullable=True),
        sa.Column('status', sa.String(length=20), nullable=False),
        sa.Column('result', sa.Text(), nullable=True),
        sa.Column('reminder_status', sa.String(length=30), nullable=False),
        sa.Column('remind_before_min', sa.Integer(), nullable=True),
        sa.Column('remind_channel', sa.String(length=32), nullable=True),
        sa.Column('rrule', sa.String(length=300), nullable=True),
        sa.Column('notes', sa.Text(), nullable=True),
        sa.Column('created_by', sa.Integer(), nullable=True),
        sa.Column('created_at', sa.DateTime(), nullable=False),
        sa.ForeignKeyConstraint(['assignee_id'], ['users.id'], ),
        sa.ForeignKeyConstraint(['client_id'], ['clients.id'], ),
        sa.ForeignKeyConstraint(['created_by'], ['users.id'], ),
        sa.ForeignKeyConstraint(['matter_id'], ['matters.id'], ),
        sa.PrimaryKeyConstraint('id'),
    )
    with op.batch_alter_table('calendar_events', schema=None) as batch_op:
        batch_op.create_index(
            batch_op.f('ix_calendar_events_assignee_id'), ['assignee_id'], unique=False
        )
        batch_op.create_index(
            batch_op.f('ix_calendar_events_category'), ['category'], unique=False
        )
        batch_op.create_index(
            batch_op.f('ix_calendar_events_client_id'), ['client_id'], unique=False
        )
        batch_op.create_index(
            batch_op.f('ix_calendar_events_matter_id'), ['matter_id'], unique=False
        )
        batch_op.create_index(
            batch_op.f('ix_calendar_events_start_at'), ['start_at'], unique=False
        )


def downgrade() -> None:
    op.drop_table('calendar_events')

    op.create_table(
        'deadlines',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('matter_id', sa.Integer(), nullable=False),
        sa.Column('due_date', sa.Date(), nullable=False),
        sa.Column('title', sa.String(length=200), nullable=False),
        sa.Column('type', sa.String(length=100), nullable=True),
        sa.Column('status', sa.String(length=20), nullable=False),
        sa.Column('notes', sa.Text(), nullable=True),
        sa.Column('created_at', sa.DateTime(), nullable=False),
        sa.ForeignKeyConstraint(['matter_id'], ['matters.id'], ),
        sa.PrimaryKeyConstraint('id'),
    )
    with op.batch_alter_table('deadlines', schema=None) as batch_op:
        batch_op.create_index(batch_op.f('ix_deadlines_due_date'), ['due_date'], unique=False)
        batch_op.create_index(batch_op.f('ix_deadlines_matter_id'), ['matter_id'], unique=False)

    op.create_table(
        'hearings',
        sa.Column('id', sa.Integer(), nullable=False),
        sa.Column('matter_id', sa.Integer(), nullable=False),
        sa.Column('date', sa.Date(), nullable=False),
        sa.Column('time', sa.Time(), nullable=True),
        sa.Column('location', sa.String(length=200), nullable=True),
        sa.Column('type', sa.String(length=100), nullable=True),
        sa.Column('result', sa.Text(), nullable=True),
        sa.Column('reminder_status', sa.String(length=30), nullable=False),
        sa.Column('notes', sa.Text(), nullable=True),
        sa.Column('created_at', sa.DateTime(), nullable=False),
        sa.ForeignKeyConstraint(['matter_id'], ['matters.id'], ),
        sa.PrimaryKeyConstraint('id'),
    )
    with op.batch_alter_table('hearings', schema=None) as batch_op:
        batch_op.create_index(batch_op.f('ix_hearings_date'), ['date'], unique=False)
        batch_op.create_index(batch_op.f('ix_hearings_matter_id'), ['matter_id'], unique=False)
