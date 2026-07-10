.PHONY: help setup ui test lint run docker-up
.DEFAULT_GOAL := help

VENV := .venv

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

setup:  ## Install backend (runs scripts/install.sh)
	./scripts/install.sh

ui:  ## Rebuild the SPA from the sibling korkut-ui repo into the backend static dir
	cd ../korkut-ui && npm ci && npm run build
	rm -rf backend/src/korkut/api/static
	cp -r ../korkut-ui/dist backend/src/korkut/api/static

test:  ## Run backend tests, lint and type checks
	cd backend && ../$(VENV)/bin/python -m pytest
	cd backend && ../$(VENV)/bin/ruff check .
	cd backend && ../$(VENV)/bin/mypy

lint:  ## Lint backend (ruff)
	cd backend && ../$(VENV)/bin/ruff check . && ../$(VENV)/bin/ruff format --check .

run:  ## Start the API server on :8080
	cd backend && ../$(VENV)/bin/python -m korkut.api

docker-up:  ## Build and start the full stack via docker compose
	docker compose -f deploy/docker-compose.yml up -d --build
