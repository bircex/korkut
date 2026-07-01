# Legal Notice & Disclaimer

Korkut is **free, open-source software that you self-host** — it is **not** a service, a
SaaS platform, hosted product, or legal advice. It is provided **"AS IS"**, without
warranty of any kind, under the [MIT License](LICENSE).

This document is a general notice, **not legal advice.** Before using Korkut with real
client data, have a data-protection / IT lawyer review your deployment.

## 1. Not legal advice

Korkut does not provide legal counsel, representation, or any professional legal service.
Everything it produces — research, summaries, petition and contract drafts — is a **draft
intended for review by a qualified lawyer.**

AI output is probabilistic and can be **wrong, incomplete, outdated, or fabricated**
(including invented case numbers, dates, or statute/article references). **Verifying every
output before relying on it is solely the user's responsibility.** Decisions or filings
made on the basis of Korkut's output are made at the user's own risk.

## 2. Your data stays on your machine

By default, all data (chats, documents, accounts, case files) is stored **only on the
server you run Korkut on.** The project ships **no telemetry and no analytics** — the
developers receive nothing, and cannot access your deployment or your data.

## 3. You are the data controller

Whoever installs and operates Korkut is the **data controller** ("veri sorumlusu") under
Türkiye's Personal Data Protection Law (KVKK), and under the GDPR where applicable, for the
personal data they process. All related obligations — registry (VERBİS) registration,
privacy notices, obtaining consent, technical and organizational security measures, and
retention/erasure — are **the operator's responsibility.**

The developers of Korkut are **neither a data controller nor a data processor** of your
data.

## 4. Local by default; cloud models are optional and your responsibility

- **Local model (default / recommended).** Point Korkut at a **local** OpenAI-compatible
  model (e.g. Ollama, LM Studio) running on your own machine, and your inputs **do not
  leave the machine.**
- **Cloud / external model (optional).** If *you* choose to connect Korkut to an external
  provider (e.g. OpenAI, Google, or any third-party API), your inputs are transmitted to
  that provider — potentially **abroad**. Satisfying cross-border transfer requirements
  (KVKK Art. 9 / GDPR Chapter V) and assessing that provider's data-retention and
  model-training policies is **solely your responsibility.**

## 5. Best-effort privacy features — provided, but not guaranteed

Korkut includes privacy and security mechanisms, but they are **mitigations, not
guarantees**, and the developers **do not warrant their effectiveness**:

- **Redaction of cloud-bound text** is **best-effort** (pattern- and dictionary-based). It
  may miss personal data (names, addresses, identifiers, free-text facts). It is **not** a
  substitute for keeping data on a local model, and must not be relied upon as a complete
  de-identification measure.
- **Two-factor authentication (TOTP)** is enforced, but overall security depends on how
  *you* deploy the software — TLS, network hardening, backups, encryption at rest, and
  keeping it updated are **your responsibility.**

Use of these features does not shift any legal responsibility from the operator to the
developers.

## 6. Optional third-party integrations

Enabling integrations such as WhatsApp, Telegram, or e-mail sends the relevant data to
those third-party services, which are governed by **their own terms and policies.** The
lawfulness of such transfers and any required notice or consent are **the operator's
responsibility.**

## 7. Limitation of liability

To the maximum extent permitted by applicable law, the authors and contributors of Korkut
shall not be liable for any direct, indirect, incidental, special, or consequential
damages arising out of or in connection with the software or its use. This limitation does
**not** apply to damages caused by the developers' gross negligence or willful misconduct.

## 8. License

Korkut is distributed under the [MIT License](LICENSE). The above disclaimers supplement,
and do not replace, the warranty and liability terms of that license.
