# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Calendar Versioning](https://calver.org/) (`YYYY.MM.PATCH`). The release pipeline
(`.github/workflows/release.yml`) moves the **Unreleased** section into a dated release.

## [Unreleased]

## [2026.7.0] - 2026-07-02

### Added
- **Matter-centric domain**: case files (matters), clients, hearings, and procedural deadlines —
  with office-scoped agent tools, REST endpoints, and a "Dosyalar" UI.
- **Unified agenda**: hearings + pending deadlines in one calendar view, surfaced proactively.
- **`prepare_hearing`** tool: assembles a hearing-prep pack (matter + agenda + linked documents).
- **Document ↔ matter linking** and **correspondence ↔ matter linking**.
- Open-source project setup: MIT license, contribution/security/conduct docs, issue & PR
  templates, install scripts, release pipeline, and security scanning.

### Changed
- **Model-driven intent**: removed the keyword-based intake gate; the model now decides when to
  draft via the tool contract (fixes topical questions being treated as petition requests).
- **Consolidated legal-search tools** from 18 near-identical tools to 3 source-routed tools,
  cutting the per-request token footprint.

[Unreleased]: https://github.com/bircex/korkut/commits/main
