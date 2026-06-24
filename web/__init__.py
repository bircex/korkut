"""Korkut web app — the self-hosted chat UI.

A thin FastAPI layer over the brain (`korkut.brain.agent.answer`): it authenticates office
users, stores their conversation with Korkut, and serves a single-page chat UI. Everything
stays on the office machine; only the redacted, cloud-bound traffic the router chooses to
send ever leaves (docs/ARCHITECTURE.md §4, §13).
"""
