# Security Policy

Korkut handles confidential client data for law offices, so we take security seriously.

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull
requests.**

Instead, use one of these private channels:

1. **GitHub Private Vulnerability Reporting** (preferred): open a report from the repository's
   **Security → Report a vulnerability** tab
   (<https://github.com/recepkizilarslan/korkut/security/advisories/new>).
2. **Email**: <recepkizilarslan@gmail.com> with the subject `SECURITY: Korkut`.

Please include:

- a description of the vulnerability and its impact,
- steps to reproduce (proof-of-concept if possible),
- affected version/commit, and any suggested remediation.

## What to expect

- **Acknowledgement** within 5 business days.
- An assessment and, for confirmed issues, a remediation plan with a target timeline.
- Credit in the release notes once a fix ships, unless you prefer to remain anonymous.

Please give us a reasonable window to release a fix before any public disclosure.

## Supported versions

Korkut is pre-1.0 and ships from `main`. Security fixes land on `main` and the latest release; older
releases are not maintained. Run the most recent version.

## Scope notes

- Secrets (`.env`), client data (`data/`), and backups must never be committed — `.gitignore`
  enforces this. If you find committed secrets, report them privately.
- Korkut redacts PII before any cloud model call (`core/redactor.py`); issues that defeat this
  boundary are in scope and high priority.
