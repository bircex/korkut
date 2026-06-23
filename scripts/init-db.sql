-- Runs once on first Postgres init. Mattermost uses the default `mattermost` DB
-- (POSTGRES_DB); this adds the separate Korkut application DB (ARCHITECTURE.md §1).
CREATE DATABASE korkut OWNER korkut;
