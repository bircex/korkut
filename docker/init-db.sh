#!/bin/bash
# Deploy-time DB bootstrap (Phase 11). Runs once on first Postgres init (mounted into
# /docker-entrypoint-initdb.d). Creates the least-privileged app-role LOGIN the hosts connect
# as; the operational TenancyRls migration then GRANTs it schema privileges and every schema
# (operational / agent / knowledge) FORCEs row-level security, so the app role is subject to RLS.
# The migrator connects as the superuser (POSTGRES_USER) to apply schema. The password comes from
# the compose environment (KORKUT_APP_DB_PASSWORD). Never grant this role BYPASSRLS or ownership.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
	DO \$\$
	BEGIN
	    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_app') THEN
	        EXECUTE format('CREATE ROLE korkut_app LOGIN PASSWORD %L', '${KORKUT_APP_DB_PASSWORD}');
	    END IF;
	END
	\$\$;
	GRANT CONNECT ON DATABASE "$POSTGRES_DB" TO korkut_app;
EOSQL
