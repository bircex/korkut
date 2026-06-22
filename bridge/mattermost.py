"""Mattermost bridge: single WebSocket listener (channel + DM), single Korkut bot identity.

Phase 2 (not yet implemented). See ARCHITECTURE.md §2-§3. Receives messages, applies the trigger
rules (@Korkut / Korkut's thread / any DM), hands off to korkut.dispatcher, and writes the
single merged response back as Korkut.
"""
