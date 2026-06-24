# Signal Bot Refactor and Production Hardening Prompt

## Short Instruction

Work in small, safe phases. First analyze and report. Then design the target architecture. Then refactor/rewrite cleanly. Existing backward compatibility is not required, but the final application must be functional, tested, secure, maintainable, and production-ready on Linux.

---

# Mission

You are a senior Python software architect, security engineer, DevOps engineer, database designer, and refactoring specialist.

Your task is to analyze, clean up, refactor, restructure, test, secure, and prepare for Linux production hosting a Python Signal bot application.

The current application is messy and contains spaghetti code. Storage/database logic is weak or unstructured. The project may also contain an Astro landing page mixed inside the same repository.

Backward compatibility is not required.

You are allowed to redesign the application architecture, database schema, folder structure, configuration system, and internal APIs as long as the final app preserves the intended product functionality and is easier to maintain.

The final result must be:

- Clean
- SOLID
- Testable
- Secure
- Performant
- Linux-hostable
- Properly documented
- Split into separate backend and landing page repositories

---

# Project Context

The repository contains:

- Python Signal bot backend
- Storage/database logic
- Possibly hardcoded configuration/secrets
- Possibly unstructured folder layout
- Possibly missing tests
- Possibly weak error handling
- Possibly weak logging
- Possibly poor DB design
- Astro landing page mixed into backend repository
- Target deployment: Linux server

The goal is not just formatting code.

The goal is a high-quality production-ready refactor/rewrite.

---

# Non-Negotiable Rules

Do not blindly rewrite before understanding the app.

Do not leave the application in a non-runnable state.

Do not hardcode secrets.

Do not store tokens, credentials, private keys, Signal credentials, database passwords, API keys, or admin identifiers directly in source code.

Do not commit `.env` files.

Do not mix landing page code with backend code.

Do not skip tests.

Do not introduce unnecessary frameworks.

Do not over-engineer.

Do not keep spaghetti code just for compatibility.

Do not keep bad abstractions if a clean redesign is safer.

Do not use raw string-concatenated SQL.

Do not swallow exceptions silently.

Do not log sensitive Signal message contents, phone numbers, tokens, or private data unless redacted.

Do not run production containers as root.

Every important change must be documented.

---

# Important Difference From Legacy Refactor

Backward compatibility is not required.

That means you may:

- Rename modules
- Rename classes
- Rename functions
- Redesign internal APIs
- Redesign database schema
- Create migrations
- Replace poor storage logic
- Replace messy command handling
- Replace global state
- Split huge files
- Remove dead code
- Separate frontend and backend repositories
- Introduce clean interfaces
- Replace fragile implementation details

However, the final application must still satisfy the intended product behavior.

Before removing any feature, document it and explain why it is removed.

---

# Phase 1 — Full Repository Analysis

Before editing, inspect the full repository.

Analyze:

- Entry points
- Runtime flow
- Bot startup process
- Signal integration method
- Message receiving flow
- Message sending flow
- Commands/features
- Storage/database layer
- Current schema or file storage model
- Configuration handling
- Secrets handling
- Logging
- Error handling
- Background jobs
- Scheduling logic
- External integrations
- Dependency list
- Astro landing page
- Docker/systemd/deployment files
- Test coverage
- Security risks
- Performance bottlenecks
- Data model problems
- Linux hosting requirements

Produce this report before implementation:

## Current State Report

Include:

1. What the app currently does
2. Current runtime flow
3. Current folder structure
4. Main modules and responsibilities
5. Signal integration method
6. Current command/message flow
7. Current database/storage design
8. Current configuration/secrets handling
9. Current logging/error handling
10. Current Astro landing page structure
11. Existing tests
12. Missing tests
13. Security issues
14. Performance issues
15. Dead code or duplicated code
16. Refactor/rewrite risks
17. Recommended target architecture

Do not start the implementation before completing this report.

---

# Phase 2 — Target Architecture Decision

After the analysis, design the target architecture.

The backend should follow clean architecture principles.

Preferred structure:

```txt
signal-bot-backend/
  app/
    __init__.py
    main.py

    core/
      __init__.py
      config.py
      logging.py
      errors.py
      security.py
      container.py

    domain/
      __init__.py
      models/
        __init__.py
      services/
        __init__.py
      value_objects/
        __init__.py

    application/
      __init__.py
      commands/
        __init__.py
      handlers/
        __init__.py
      services/
        __init__.py
      use_cases/
        __init__.py
      ports/
        __init__.py

    infrastructure/
      __init__.py
      signal/
        __init__.py
        client.py
        adapter.py
      persistence/
        __init__.py
        database.py
        repositories.py
        migrations/
      scheduler/
        __init__.py
      external/
        __init__.py

    interfaces/
      __init__.py
      bot/
        __init__.py
        router.py
        parser.py

  tests/
    unit/
    integration/
    security/
    regression/

  scripts/
    run.sh
    migrate.sh
    backup.sh
    restore.sh

  docker/
    Dockerfile
    docker-compose.yml

  deploy/
    systemd/
      signal-bot.service

  pyproject.toml
  README.md
  .env.example
  .gitignore
```

Adjust names if needed, but preserve clean separation.

---

# Phase 3 — SOLID Principles

Apply SOLID principles strictly.

## Single Responsibility Principle

Each module/class/function should have one reason to change.

Avoid code that handles Signal input, command parsing, business logic, DB writes, logging, and replies in the same function.

Target separation:

```txt
Signal Adapter
Message Normalizer
Router
Command Parser
Command Handler
Use Case
Repository
Database
Response Sender
```

## Open/Closed Principle

New bot commands should be added without editing one massive `if/elif` chain.

Use command registration.

Example:

```python
from typing import Protocol

class CommandHandler(Protocol):
    command: str

    async def handle(self, message: "IncomingMessage") -> "OutgoingMessage":
        ...
```

## Liskov Substitution Principle

Interfaces must be replaceable.

For example, SQLite and PostgreSQL repositories should both satisfy the same repository contract.

## Interface Segregation Principle

Avoid giant interfaces.

Prefer focused protocols:

```python
class UserRepository(Protocol):
    async def get_by_signal_id(self, signal_id: str) -> "User | None":
        ...

    async def save(self, user: "User") -> None:
        ...


class MessageRepository(Protocol):
    async def save_incoming(self, message: "IncomingMessage") -> None:
        ...


class SettingsRepository(Protocol):
    async def get_user_settings(self, user_id: str) -> "UserSettings":
        ...
```

## Dependency Inversion Principle

Application and domain layers must depend on abstractions, not concrete infrastructure.

Use dependency injection through constructors or a small application container.

---

# Phase 4 — Bot Runtime Architecture

Refactor the bot flow into this architecture:

```txt
Incoming Signal message
        ↓
Signal adapter
        ↓
Message normalizer
        ↓
Router
        ↓
Command parser
        ↓
Command handler
        ↓
Use case/service
        ↓
Repository / external service
        ↓
Outgoing response
        ↓
Signal adapter sends reply
```

Required abstractions:

```python
from typing import Protocol

class SignalClient(Protocol):
    async def send_message(self, recipient: str, message: str) -> None:
        ...


class CommandHandler(Protocol):
    command: str

    async def handle(self, message: "IncomingMessage") -> "OutgoingMessage":
        ...
```

Requirements:

- Clean command registry
- Unknown command behavior
- Validation error behavior
- Authorization failure behavior
- Safe user-facing error messages
- Internal errors logged safely
- No giant message handler function
- No direct DB calls from Signal adapter
- No business logic inside infrastructure code

---

# Phase 5 — Database and Storage Redesign

Analyze the existing DB/storage.

Then design a clean persistence model.

Backward compatibility is not required, but migration must be considered if real data exists.

Identify:

- Current tables/collections/files
- Current schema
- Data ownership
- Duplicate data
- Missing indexes
- Missing constraints
- Missing timestamps
- Query patterns
- Slow queries
- Race conditions
- Transaction risks
- Serialization issues
- Backup/restore requirements
- PII/security concerns

Choose the correct database.

SQLite is acceptable if:

- The bot is small
- Single-server deployment is expected
- Write concurrency is low
- Simplicity is preferred

PostgreSQL is preferred if:

- Multiple users are expected
- Concurrent writes are expected
- Reliability matters
- Future scaling matters
- Admin/reporting/querying matters

Recommended stack for serious production:

```txt
PostgreSQL
SQLAlchemy 2.x
Alembic
asyncpg if async DB access is needed
```

Recommended stack for small/simple production:

```txt
SQLite
SQLAlchemy 2.x
Alembic
WAL mode
foreign_keys = ON
busy_timeout
regular backups
```

Required DB practices:

- Repository pattern
- Migrations
- Schema versioning
- Indexes for frequent lookups
- Unique constraints where needed
- Foreign keys where applicable
- Created/updated timestamps
- Transaction boundaries
- Input validation before persistence
- No raw string-concatenated SQL
- No secrets stored unless encrypted
- Backup strategy
- Restore strategy
- Migration rollback notes

Create:

```txt
app/infrastructure/persistence/database.py
app/infrastructure/persistence/repositories.py
app/infrastructure/persistence/migrations/
```

Add database commands:

```bash
python -m app.infrastructure.persistence.migrate
```

or Alembic commands if Alembic is used.

---

# Phase 6 — Tests

Add a proper test suite.

Use pytest.

Required categories:

```txt
tests/unit/
tests/integration/
tests/security/
tests/regression/
```

Test:

- Command parsing
- Message routing
- Command handlers
- Use cases
- Repository behavior
- Database migrations
- Config loading
- Authorization logic
- Rate limiting logic if implemented
- Safe error handling
- Signal client adapter with mocks
- Unknown commands
- Invalid inputs
- Admin-only commands
- Persistence edge cases
- Duplicate message/idempotency handling if needed

Use:

```txt
pytest
pytest-cov
pytest-asyncio
```

If async code exists, test async code properly.

Do not test Signal external API directly in unit tests.

Mock the Signal client.

Commands:

```bash
pytest
pytest --cov=app --cov-report=term-missing
```

---

# Phase 7 — Code Quality Tooling

Create or update `pyproject.toml`.

Use Python 3.12+ unless the project requires another version.

Recommended tooling:

```txt
ruff
mypy
pytest
pytest-cov
pytest-asyncio
bandit
pip-audit
```

Recommended `pyproject.toml`:

```toml
[project]
requires-python = ">=3.12"

[tool.ruff]
line-length = 100
target-version = "py312"

[tool.ruff.lint]
select = ["E", "F", "I", "B", "UP", "SIM", "C4", "T20", "ARG", "PL"]
ignore = []

[tool.ruff.format]
quote-style = "double"

[tool.mypy]
python_version = "3.12"
strict = true
warn_unused_configs = true
warn_return_any = true
warn_unused_ignores = true
disallow_untyped_defs = true
no_implicit_optional = true

[tool.pytest.ini_options]
testpaths = ["tests"]
asyncio_mode = "auto"
```

If strict mypy cannot be enabled immediately, document why and create a gradual plan.

Commands:

```bash
ruff check .
ruff format .
mypy .
pytest
pytest --cov=app --cov-report=term-missing
bandit -r app
pip-audit
```

---

# Phase 8 — Security Review and Hardening

Perform a full security audit.

Check:

- Hardcoded secrets
- Secrets in config files
- Secrets in git history if possible
- Unsafe eval/exec
- Unsafe subprocess usage
- Shell injection
- SQL injection
- Path traversal
- Weak file permissions
- Sensitive logs
- Phone number/message leakage
- Missing input validation
- Unbounded message size
- Missing authorization for admin commands
- Missing rate limits
- Unsafe file writes
- Unsafe temp files
- Insecure Docker configuration
- Running as root
- Missing dependency vulnerability checks
- Debug mode in production
- Publicly exposed admin endpoints
- Overly broad Linux permissions

Implement:

- Environment-based config
- `.env.example`
- Strict `.gitignore`
- Secret redaction in logs
- Admin allowlist
- Message size limits
- Optional per-user rate limiting
- Safe subprocess wrapper if subprocess is needed
- Input validation
- Structured logging
- Dependency audit
- Bandit scan
- pip-audit scan
- Non-root Docker user
- Systemd hardening
- Linux file permission guidance

Example `.env.example`:

```env
APP_ENV=production
LOG_LEVEL=INFO
DATABASE_URL=
SIGNAL_PHONE_NUMBER=
SIGNAL_CLI_CONFIG_DIR=
SIGNAL_CLI_SOCKET=
ADMIN_SIGNAL_IDS=
MAX_MESSAGE_LENGTH=4096
RATE_LIMIT_PER_MINUTE=20
```

Admin commands must reject unknown users.

---

# Phase 9 — Configuration

Centralize configuration.

Create:

```txt
app/core/config.py
```

Requirements:

- Read config from environment variables
- Validate required config at startup
- Safe defaults only for local development
- Fail fast when required production config is missing
- No scattered `os.getenv`
- `.env.example` documents all keys
- Real `.env` is ignored by Git
- Config object is injected into services

Recommended:

```python
from pydantic_settings import BaseSettings
```

Use it if dependency addition is acceptable.

---

# Phase 10 — Logging and Observability

Replace print statements with structured logging.

Create:

```txt
app/core/logging.py
```

Requirements:

- Log startup
- Log shutdown
- Log handled command names
- Log errors with correlation/message id
- Redact secrets
- Redact sensitive identifiers
- Avoid full private message logs
- Include environment
- Include app version if available
- Prefer JSON logs in production
- Log to stdout/stderr for Docker/systemd

Use standard Python logging unless another dependency is justified.

---

# Phase 11 — Error Handling

Create clear error types.

Create:

```txt
app/core/errors.py
```

Define:

```txt
AppError
ConfigurationError
StorageError
SignalClientError
ValidationError
AuthorizationError
RateLimitError
CommandError
```

Rules:

- Do not swallow exceptions silently
- Do not expose stack traces to Signal users
- Map internal errors to safe user-facing replies
- Log internal error details safely
- Use typed exceptions for expected failures

---

# Phase 12 — Performance Review

Analyze and improve:

- Blocking I/O inside async handlers
- Repeated DB connections
- Missing DB indexes
- N+1 queries
- Large in-memory lists
- Unbounded queues
- Infinite loops without backoff
- Excessive logging
- Slow startup
- File-based storage contention
- Signal client connection reuse
- Message retry behavior
- Duplicate message processing
- Memory leaks
- Long-running task cancellation
- Graceful shutdown

Add:

- Connection reuse
- Async where appropriate
- Bounded queues
- Backoff/retry policy
- Idempotency for duplicate messages if needed
- DB indexes
- Efficient serialization
- Timeouts
- Graceful shutdown
- Health check command or endpoint

If the app exposes HTTP, add:

```txt
/readiness
/liveness
```

If not, add a CLI health check:

```bash
python -m app.main healthcheck
```

---

# Phase 13 — Linux Hosting Readiness

Prepare for Linux production hosting.

Support at least one deployment mode:

1. Docker Compose
2. systemd service

Prefer Docker Compose if Signal dependencies and database dependencies are easier to isolate.

Required files:

```txt
.env.example
README.md
docker/Dockerfile
docker/docker-compose.yml
scripts/run.sh
scripts/migrate.sh
```

If production data exists, add:

```txt
scripts/backup.sh
scripts/restore.sh
```

If systemd is used, include:

```txt
deploy/systemd/signal-bot.service
```

Systemd hardening example:

```ini
[Unit]
Description=Signal Bot
After=network-online.target
Wants=network-online.target

[Service]
User=signalbot
Group=signalbot
WorkingDirectory=/opt/signal-bot
EnvironmentFile=/etc/signal-bot/signal-bot.env
ExecStart=/opt/signal-bot/.venv/bin/python -m app.main
Restart=always
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/signal-bot
ReadWritePaths=/var/log/signal-bot

[Install]
WantedBy=multi-user.target
```

Docker requirements:

- Non-root user
- Minimal image
- No secrets baked into image
- Volumes for persistent DB/config
- Restart policy
- Healthcheck if possible
- Logs to stdout/stderr
- Resource limits if appropriate

---

# Phase 14 — Astro Landing Page Separation

Move the Astro landing page into a separate repository.

Target repository:

```txt
signal-bot-landing/
  src/
  public/
  package.json
  astro.config.mjs
  tsconfig.json
  README.md
  .gitignore
  .env.example
  .nvmrc
```

Tasks:

1. Identify all Astro-related files.
2. Move Astro code out of the Python backend repository.
3. Ensure the landing page builds independently.
4. Remove backend-only files from the landing repo.
5. Remove frontend-only files from the backend repo.
6. Update README files for both repositories.
7. Fix relative asset paths.
8. Ensure no secrets are copied into the landing repo.
9. Add `.nvmrc`.
10. Keep backend and landing deployment fully independent.

Landing commands:

```bash
npm install
npm run dev
npm run build
npm run preview
```

If using package managers like pnpm, npm, or yarn, standardize and document the chosen one.

---

# Phase 15 — Documentation

Create production-quality backend README:

```txt
# Signal Bot Backend

## Features
## Architecture
## Requirements
## Configuration
## Local Development
## Running Tests
## Linting
## Security Checks
## Database Migrations
## Running with Docker
## Running with systemd
## Backup and Restore
## Troubleshooting
```

Create landing page README:

```txt
# Signal Bot Landing Page

## Requirements
## Local Development
## Build
## Preview
## Deployment
```

Document:

- Architecture decisions
- Database choice
- Security assumptions
- Deployment requirements
- Required environment variables
- How to run locally
- How to run in production
- How to run tests
- How to migrate database
- How to back up and restore data

---

# Phase 16 — Implementation Strategy

Use this order:

1. Inspect repository
2. Produce current state report
3. Design target architecture
4. Split Astro landing page into separate repo
5. Create backend folder structure
6. Add centralized config
7. Add structured logging
8. Add error model
9. Add Signal client abstraction
10. Add command parser/router
11. Add use cases
12. Redesign persistence layer
13. Add migrations
14. Add tests
15. Add security hardening
16. Add performance improvements
17. Add Docker/systemd deployment
18. Add documentation
19. Run all checks
20. Fix failures
21. Produce final report

After each major step, verify the app still starts.

---

# Phase 17 — Acceptance Criteria

The task is complete only when:

- Backend and landing page are separated
- Backend has clean architecture
- Folder structure is understandable
- SOLID principles are applied
- Database/storage layer is redesigned cleanly
- Config is centralized
- Secrets are not hardcoded
- Logging is structured
- Error handling is safe
- Security checks are added
- Performance issues are addressed
- Tests are added and passing
- Docker or systemd deployment is available
- Linux hosting documentation is ready
- README files are complete
- Final app runs
- Final app can process Signal messages
- Final app can send Signal replies
- Remaining risks are documented

---

# Phase 18 — Final Output Format

When finished, output:

```txt
## Current State Summary

## Target Architecture

## Refactor/Rebuild Completed

## Backend Folder Structure

## Landing Page Repository

## Database Design

## Security Improvements

## Performance Improvements

## Tests

## Linux Hosting

## Commands

## Migration Notes

## Risks

## Next Steps
```

Also include exact commands:

```bash
pytest
pytest --cov=app --cov-report=term-missing
ruff check .
ruff format .
mypy .
bandit -r app
pip-audit
python -m app.main
python -m app.main healthcheck
```

For Astro:

```bash
npm install
npm run dev
npm run build
npm run preview
```
