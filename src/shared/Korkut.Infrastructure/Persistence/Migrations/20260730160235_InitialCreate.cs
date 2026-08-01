using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "korkut");

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_json = table.Column<string>(type: "text", nullable: true),
                    after_json = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mail_preferences",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    notify_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_preferences", x => x.office_id);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    national_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.UniqueConstraint("AK_clients_office_id_id", x => new { x.office_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "communication_notes",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "correspondence",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    doc_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correspondence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deadlines",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reminder_days_before = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deadlines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bucket = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    object_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    retention_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    legal_hold = table.Column<bool>(type: "boolean", nullable: false),
                    tombstoned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.UniqueConstraint("AK_documents_office_id_id", x => new { x.office_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    spent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_entries",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_entries", x => new { x.office_id, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "invites",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_audit",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memory_items",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "office_custom_roles",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    permissions = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_office_custom_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "office_persona",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expertise = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    extra_rules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_office_persona", x => x.office_id);
                });

            migrationBuilder.CreateTable(
                name: "offices",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    disabled = table.Column<bool>(type: "boolean", nullable: false),
                    require_two_factor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "style_profiles",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    letterhead = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    signature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    boilerplate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_style_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    all_day = table.Column<bool>(type: "boolean", nullable: false),
                    location = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    rrule = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    remind_before_min = table.Column<int>(type: "integer", nullable: true),
                    reminder_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_totp",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_step = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_totp", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bar_registry_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    google_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    token_epoch = table.Column<int>(type: "integer", nullable: false),
                    failed_access_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matters",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name_snapshot = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    file_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    case_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    counterparty = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    court_name_snapshot = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matters", x => x.id);
                    table.UniqueConstraint("AK_matters_office_id_id", x => new { x.office_id, x.id });
                    table.ForeignKey(
                        name: "FK_matters_clients_office_id_client_id",
                        columns: x => new { x.office_id, x.client_id },
                        principalSchema: "korkut",
                        principalTable: "clients",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "document_extractions",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version = table.Column<int>(type: "integer", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    span_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    source_checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_object_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_extractions", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_extractions_documents_office_id_document_id",
                        columns: x => new { x.office_id, x.document_id },
                        principalSchema: "korkut",
                        principalTable: "documents",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_relations",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_relations_documents_office_id_document_id",
                        columns: x => new { x.office_id, x.document_id },
                        principalSchema: "korkut",
                        principalTable: "documents",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matter_hearings",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    location = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    result = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reminder_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matter_hearings", x => x.id);
                    table.ForeignKey(
                        name: "FK_matter_hearings_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties", x => x.id);
                    table.ForeignKey(
                        name: "FK_parties_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity",
                schema: "korkut",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_office_at",
                schema: "korkut",
                table: "audit_events",
                columns: new[] { "office_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_conversation",
                schema: "korkut",
                table: "chat_messages",
                columns: new[] { "office_id", "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_clients_office_deleted",
                schema: "korkut",
                table: "clients",
                columns: new[] { "office_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comm_notes_office_matter",
                schema: "korkut",
                table: "communication_notes",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_office_updated",
                schema: "korkut",
                table: "conversations",
                columns: new[] { "office_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_office_direction",
                schema: "korkut",
                table: "correspondence",
                columns: new[] { "office_id", "direction" });

            migrationBuilder.CreateIndex(
                name: "ix_courts_office",
                schema: "korkut",
                table: "courts",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_deadlines_office_due",
                schema: "korkut",
                table: "deadlines",
                columns: new[] { "office_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ux_doc_extractions_provenance",
                schema: "korkut",
                table: "document_extractions",
                columns: new[] { "office_id", "document_id", "document_version", "parser_version", "page", "span_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doc_relations_target",
                schema: "korkut",
                table: "document_relations",
                columns: new[] { "office_id", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ux_doc_relations_link",
                schema: "korkut",
                table: "document_relations",
                columns: new[] { "office_id", "document_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_office_matter",
                schema: "korkut",
                table: "documents",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_office_status",
                schema: "korkut",
                table: "documents",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_documents_object_key",
                schema: "korkut",
                table: "documents",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_office_spent",
                schema: "korkut",
                table: "expenses",
                columns: new[] { "office_id", "spent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_office_email",
                schema: "korkut",
                table: "invites",
                columns: new[] { "office_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_token_hash",
                schema: "korkut",
                table: "invites",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_office_status",
                schema: "korkut",
                table: "invoices",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_audit_message",
                schema: "korkut",
                table: "job_audit",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_audit_office_at",
                schema: "korkut",
                table: "job_audit",
                columns: new[] { "office_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_matter_hearings_office_matter",
                schema: "korkut",
                table: "matter_hearings",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "IX_matters_office_id_client_id",
                schema: "korkut",
                table: "matters",
                columns: new[] { "office_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "ix_matters_office",
                schema: "korkut",
                table: "matters",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_matters_office_status",
                schema: "korkut",
                table: "matters",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_memory_items_office_status",
                schema: "korkut",
                table: "memory_items",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_custom_roles_office_key",
                schema: "korkut",
                table: "office_custom_roles",
                columns: new[] { "office_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_office_key",
                schema: "korkut",
                table: "outbox_messages",
                columns: new[] { "office_id", "idempotency_key" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_status_available_at",
                schema: "korkut",
                table: "outbox_messages",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_office_matter",
                schema: "korkut",
                table: "parties",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reset_tokens_office_hash",
                schema: "korkut",
                table: "password_reset_tokens",
                columns: new[] { "office_id", "token_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_office_user",
                schema: "korkut",
                table: "recovery_codes",
                columns: new[] { "office_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family",
                schema: "korkut",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_hash",
                schema: "korkut",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_style_profiles_office_scope_user",
                schema: "korkut",
                table: "style_profiles",
                columns: new[] { "office_id", "scope", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_office_status",
                schema: "korkut",
                table: "tasks",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_office_due",
                schema: "korkut",
                table: "tasks",
                columns: new[] { "office_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ux_user_totp_office_user",
                schema: "korkut",
                table: "user_totp",
                columns: new[] { "office_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_google_sub",
                schema: "korkut",
                table: "users",
                column: "google_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_office_email",
                schema: "korkut",
                table: "users",
                columns: new[] { "office_id", "email" },
                unique: true);

            // ===========================================================================
            // Tenant isolation, least-privilege roles, and privileged resolver functions.
            // Hand-maintained: EF does not generate this. Re-applied after every regen via
            // scratchpad/inject_security.py. See memory korkut-migrations-squashed.
            // ===========================================================================
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_app') THEN
                        CREATE ROLE korkut_app NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_resolver') THEN
                        CREATE ROLE korkut_resolver NOLOGIN BYPASSRLS;
                    END IF;
                END $$;
                GRANT USAGE ON SCHEMA korkut TO korkut_app;
                GRANT USAGE ON SCHEMA korkut TO korkut_resolver;");

            // Standard tenant tables: full DML for the app role, FORCE row-level security keyed
            // on the app.current_office GUC (NULL when unset -> no row matches -> access denied).
            foreach (var table in new[]
            {
                "chat_messages", "clients", "communication_notes",
                "conversations", "correspondence", "courts", "deadlines", "documents",
                "document_extractions", "document_relations", "expenses", "invites", "invoices",
                "mail_preferences", "matters", "matter_hearings", "memory_items", "office_custom_roles", "office_persona",
                "parties", "password_reset_tokens", "recovery_codes", "refresh_tokens",
                "style_profiles", "tasks", "user_totp", "users",
            })
            {
                migrationBuilder.Sql($@"
                    GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.{table} TO korkut_app;
                    ALTER TABLE korkut.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE korkut.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON korkut.{table}
                        USING (office_id = current_setting('app.current_office', true)::uuid)
                        WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
            }

            // offices is the tenant root (keyed by id, not office_id).
            migrationBuilder.Sql(@"
                GRANT SELECT ON korkut.offices TO korkut_app;
                GRANT UPDATE (disabled, require_two_factor) ON korkut.offices TO korkut_app;");

            // audit_events: tenant-isolated AND append-only (UPDATE/DELETE blocked for everyone).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT ON korkut.audit_events TO korkut_app;
                ALTER TABLE korkut.audit_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.audit_events FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.audit_events
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);
                CREATE OR REPLACE FUNCTION korkut.audit_events_immutable()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only (% not allowed)', TG_OP;
                END;
                $fn$;
                CREATE TRIGGER audit_events_no_mutate
                    BEFORE UPDATE OR DELETE ON korkut.audit_events
                    FOR EACH ROW EXECUTE FUNCTION korkut.audit_events_immutable();");

            // Job tables (outbox/inbox/job_audit): the worker sets tenant scope per message,
            // so these stay OUT of RLS; the app role gets only the privileges it actually uses.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE ON korkut.outbox_messages TO korkut_app;
                GRANT SELECT, INSERT, UPDATE ON korkut.inbox_entries TO korkut_app;
                GRANT SELECT, INSERT ON korkut.job_audit TO korkut_app;");

            // Privileged SECURITY DEFINER resolver functions (verbatim from the original migrations).
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_resolver') THEN
                                        CREATE ROLE korkut_resolver NOLOGIN BYPASSRLS;
                                    END IF;
                                END $$;
                                -- Cloud SQL: the migrating user is NOT a superuser, so changing function
                                -- ownership requires (a) membership in the target role and (b) the target
                                -- role holding CREATE on the schema. Both are no-ops for a real superuser;
                                -- CREATE is revoked again at the end of this migration.
                                GRANT korkut_resolver TO CURRENT_USER;
                                GRANT USAGE, CREATE ON SCHEMA korkut TO korkut_resolver;
                                GRANT SELECT, UPDATE ON korkut.users TO korkut_resolver;
                                GRANT SELECT ON korkut.offices TO korkut_resolver;
                
                                CREATE FUNCTION korkut.resolve_user_by_google_sub(p_sub text)
                                RETURNS TABLE(id uuid, office_id uuid, email text)
                                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT id, office_id, email FROM korkut.users WHERE google_sub = p_sub;
                                $fn$;
                
                                CREATE FUNCTION korkut.resolve_users_by_email(p_email text)
                                RETURNS TABLE(id uuid, office_id uuid, email text)
                                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT id, office_id, email FROM korkut.users
                                    WHERE email = p_email AND google_sub IS NULL;
                                $fn$;
                
                                CREATE FUNCTION korkut.link_google_sub(p_user_id uuid, p_sub text)
                                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    UPDATE korkut.users SET google_sub = p_sub WHERE id = p_user_id;
                                $fn$;
                
                                CREATE FUNCTION korkut.is_office_disabled(p_office uuid)
                                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT COALESCE((SELECT disabled FROM korkut.offices WHERE id = p_office), true);
                                $fn$;
                
                                CREATE FUNCTION korkut.user_has_two_factor(p_user uuid)
                                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT COALESCE((SELECT two_factor_enabled FROM korkut.users WHERE id = p_user), false);
                                $fn$;
                
                                -- Own the functions with the BYPASSRLS role so DEFINER actually bypasses RLS.
                                ALTER FUNCTION korkut.resolve_user_by_google_sub(text) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.resolve_users_by_email(text) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.link_google_sub(uuid, text) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.is_office_disabled(uuid) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.user_has_two_factor(uuid) OWNER TO korkut_resolver;
                
                                -- Only these narrow functions are executable by the app role.
                                REVOKE ALL ON FUNCTION korkut.resolve_user_by_google_sub(text) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.resolve_users_by_email(text) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.link_google_sub(uuid, text) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.is_office_disabled(uuid) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.user_has_two_factor(uuid) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.resolve_user_by_google_sub(text) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.resolve_users_by_email(text) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.link_google_sub(uuid, text) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.is_office_disabled(uuid) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.user_has_two_factor(uuid) TO korkut_app;");
            migrationBuilder.Sql(@"
                CREATE FUNCTION korkut.office_requires_two_factor(p_office uuid)
                                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT COALESCE((SELECT require_two_factor FROM korkut.offices WHERE id = p_office), false);
                                $fn$;
                                ALTER FUNCTION korkut.office_requires_two_factor(uuid) OWNER TO korkut_resolver;
                                REVOKE ALL ON FUNCTION korkut.office_requires_two_factor(uuid) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.office_requires_two_factor(uuid) TO korkut_app;");
            migrationBuilder.Sql(@"
                GRANT INSERT, SELECT, UPDATE ON korkut.refresh_tokens TO korkut_resolver;
                
                                CREATE FUNCTION korkut.issue_refresh_token(
                                    p_id uuid, p_family uuid, p_office uuid, p_user uuid, p_hash text,
                                    p_expires timestamptz, p_now timestamptz)
                                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    INSERT INTO korkut.refresh_tokens(id, family_id, office_id, user_id, token_hash, expires_at, created_at)
                                    VALUES (p_id, p_family, p_office, p_user, p_hash, p_expires, p_now);
                                $fn$;
                
                                CREATE FUNCTION korkut.rotate_refresh_token(
                                    p_hash text, p_new_id uuid, p_new_hash text, p_expires timestamptz, p_now timestamptz)
                                RETURNS TABLE(r_status text, r_office uuid, r_user uuid, r_family uuid)
                                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                                DECLARE r korkut.refresh_tokens%ROWTYPE;
                                BEGIN
                                    SELECT * INTO r FROM korkut.refresh_tokens WHERE token_hash = p_hash FOR UPDATE;
                                    IF NOT FOUND THEN
                                        RETURN QUERY SELECT 'invalid'::text, NULL::uuid, NULL::uuid, NULL::uuid; RETURN;
                                    END IF;
                                    IF r.revoked_at IS NOT NULL OR r.used_at IS NOT NULL THEN
                                        UPDATE korkut.refresh_tokens SET revoked_at = p_now
                                            WHERE family_id = r.family_id AND revoked_at IS NULL;
                                        RETURN QUERY SELECT 'reuse_revoked'::text, r.office_id, r.user_id, r.family_id; RETURN;
                                    END IF;
                                    IF r.expires_at <= p_now THEN
                                        RETURN QUERY SELECT 'expired'::text, r.office_id, r.user_id, r.family_id; RETURN;
                                    END IF;
                                    UPDATE korkut.refresh_tokens SET used_at = p_now WHERE id = r.id;
                                    INSERT INTO korkut.refresh_tokens(id, family_id, office_id, user_id, token_hash, expires_at, created_at)
                                        VALUES (p_new_id, r.family_id, r.office_id, r.user_id, p_new_hash, p_expires, p_now);
                                    RETURN QUERY SELECT 'rotated'::text, r.office_id, r.user_id, r.family_id;
                                END;
                                $fn$;
                
                                CREATE FUNCTION korkut.revoke_refresh_family(p_family uuid, p_now timestamptz)
                                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    UPDATE korkut.refresh_tokens SET revoked_at = p_now
                                    WHERE family_id = p_family AND revoked_at IS NULL;
                                $fn$;
                
                                ALTER FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) OWNER TO korkut_resolver;
                                ALTER FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) OWNER TO korkut_resolver;
                
                                REVOKE ALL ON FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) FROM PUBLIC;
                                REVOKE ALL ON FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) TO korkut_app;
                                GRANT EXECUTE ON FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) TO korkut_app;");
            migrationBuilder.Sql(@"
                -- The resolver role owns the function and reads the table under its own
                                -- (BYPASSRLS) identity, so it needs table-level SELECT.
                                GRANT SELECT ON korkut.password_reset_tokens TO korkut_resolver;
                
                                CREATE FUNCTION korkut.resolve_reset_token(p_hash text, p_now timestamptz)
                                RETURNS TABLE(office_id uuid, user_id uuid)
                                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT office_id, user_id
                                    FROM korkut.password_reset_tokens
                                    WHERE token_hash = p_hash AND used_at IS NULL AND expires_at > p_now
                                    LIMIT 1;
                                $fn$;
                                ALTER FUNCTION korkut.resolve_reset_token(text, timestamptz) OWNER TO korkut_resolver;
                                REVOKE ALL ON FUNCTION korkut.resolve_reset_token(text, timestamptz) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.resolve_reset_token(text, timestamptz) TO korkut_app;");
            migrationBuilder.Sql(@"
                
                
                                -- Accepting an invite happens before any session, so resolving its office
                                -- crosses RLS via a narrow BYPASSRLS SECURITY DEFINER function (owned by
                                -- korkut_resolver) — the sanctioned pattern. Returns an active invite only.
                                GRANT SELECT ON korkut.invites TO korkut_resolver;
                                CREATE FUNCTION korkut.resolve_invite_token(p_hash text, p_now timestamptz)
                                RETURNS TABLE(id uuid, office_id uuid, email text, role text)
                                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                                    SELECT id, office_id, email, role
                                    FROM korkut.invites
                                    WHERE token_hash = p_hash AND accepted_at IS NULL AND expires_at > p_now
                                    LIMIT 1;
                                $fn$;
                                ALTER FUNCTION korkut.resolve_invite_token(text, timestamptz) OWNER TO korkut_resolver;
                                REVOKE ALL ON FUNCTION korkut.resolve_invite_token(text, timestamptz) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.resolve_invite_token(text, timestamptz) TO korkut_app;");
            migrationBuilder.Sql(@"
                GRANT INSERT ON korkut.offices TO korkut_resolver;
                                GRANT INSERT, SELECT ON korkut.users TO korkut_resolver;
                
                                CREATE FUNCTION korkut.provision_office(
                                    p_email text, p_google_sub text, p_office_name text, p_now timestamptz)
                                RETURNS TABLE(office_id uuid, user_id uuid)
                                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                                DECLARE
                                    v_office uuid;
                                    v_user uuid;
                                BEGIN
                                    -- Never duplicate an existing identity (by email or Google subject).
                                    IF EXISTS (SELECT 1 FROM korkut.users
                                               WHERE lower(email) = lower(p_email) OR google_sub = p_google_sub) THEN
                                        RETURN;
                                    END IF;
                
                                    v_office := gen_random_uuid();
                                    v_user := gen_random_uuid();
                
                                    INSERT INTO korkut.offices (id, name, disabled, require_two_factor, created_at)
                                        VALUES (v_office, p_office_name, true, false, p_now);
                                    INSERT INTO korkut.users
                                        (id, office_id, email, role, google_sub, two_factor_enabled,
                                         security_stamp, token_epoch, failed_access_count, created_at)
                                        VALUES (v_user, v_office, lower(p_email), 'owner', p_google_sub, false,
                                                gen_random_uuid(), 0, 0, p_now);
                
                                    RETURN QUERY SELECT v_office, v_user;
                                END;
                                $fn$;
                                ALTER FUNCTION korkut.provision_office(text, text, text, timestamptz) OWNER TO korkut_resolver;
                                REVOKE ALL ON FUNCTION korkut.provision_office(text, text, text, timestamptz) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.provision_office(text, text, text, timestamptz) TO korkut_app;");
            migrationBuilder.Sql(@"
                CREATE FUNCTION korkut.provision_office_local(
                                    p_email text, p_password_hash text, p_office_name text, p_full_name text, p_now timestamptz)
                                RETURNS TABLE(office_id uuid, user_id uuid)
                                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                                DECLARE
                                    v_office uuid;
                                    v_user uuid;
                                BEGIN
                                    -- Never duplicate an existing e-mail identity.
                                    IF EXISTS (SELECT 1 FROM korkut.users WHERE lower(email) = lower(p_email)) THEN
                                        RETURN;
                                    END IF;
                
                                    v_office := gen_random_uuid();
                                    v_user := gen_random_uuid();
                
                                    INSERT INTO korkut.offices (id, name, disabled, require_two_factor, created_at)
                                        VALUES (v_office, p_office_name, true, false, p_now);
                                    INSERT INTO korkut.users
                                        (id, office_id, email, full_name, role, password_hash, two_factor_enabled,
                                         security_stamp, token_epoch, failed_access_count, created_at)
                                        VALUES (v_user, v_office, lower(p_email), p_full_name, 'owner', p_password_hash, false,
                                                gen_random_uuid(), 0, 0, p_now);
                
                                    RETURN QUERY SELECT v_office, v_user;
                                END;
                                $fn$;
                                ALTER FUNCTION korkut.provision_office_local(text, text, text, text, timestamptz) OWNER TO korkut_resolver;
                                REVOKE ALL ON FUNCTION korkut.provision_office_local(text, text, text, text, timestamptz) FROM PUBLIC;
                                GRANT EXECUTE ON FUNCTION korkut.provision_office_local(text, text, text, text, timestamptz) TO korkut_app;");

            // Least privilege restored: the resolver role only needed CREATE while function
            // ownership was being transferred above (Cloud SQL, non-superuser migrator).
            migrationBuilder.Sql("REVOKE CREATE ON SCHEMA korkut FROM korkut_resolver;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "mail_preferences",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "communication_notes",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "correspondence",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "courts",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "deadlines",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "document_extractions",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "document_relations",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "expenses",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "inbox_entries",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "invites",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "job_audit",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "matter_hearings",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "memory_items",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "office_custom_roles",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "office_persona",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "offices",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "style_profiles",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "user_totp",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "users",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "matters",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "korkut");
        }
    }
}
