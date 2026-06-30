.PHONY: help setup build test lint run docker-up
.DEFAULT_GOAL := help

VENV := .venv

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

setup:  ## Install backend + frontend (runs scripts/install.sh)
	./scripts/install.sh

build:  ## Build the frontend SPA into the backend static dir
	cd frontend && npm run build

test:  ## Run backend tests, lint and type checks
	cd backend && ../$(VENV)/bin/python -m pytest
	cd backend && ../$(VENV)/bin/ruff check .
	cd backend && ../$(VENV)/bin/mypy

lint:  ## Lint backend (ruff) and frontend (eslint)
	cd backend && ../$(VENV)/bin/ruff check . && ../$(VENV)/bin/ruff format --check .
	cd frontend && npm run lint

run:  ## Start the API server on :8080
	cd backend && ../$(VENV)/bin/python -m korkut.api

docker-up:  ## Build and start the full stack via docker compose
	docker compose -f deploy/docker-compose.yml up -d --build
