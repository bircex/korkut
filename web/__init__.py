"""Korkut web app — the self-hosted chat UI that replaces the Mattermost front door.

A thin FastAPI layer over the existing brain (`korkut.agent.answer`): it authenticates
office users, stores their conversation with Korkut, and serves a single-page chat UI.
Everything stays on the office machine; only the redacted, cloud-bound traffic the router
chooses to send ever leaves (ARCHITECTURE.md §4, §13).
"""
