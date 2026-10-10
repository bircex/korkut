.PHONY: help up down build test run migrate ui-dev ui-test website-dev
.DEFAULT_GOAL := help

SLN := Korkut.slnx

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

up:  ## Build and start the whole app with Docker Compose (http://localhost:8090)
	docker compose up -d --build

down:  ## Stop the app (data volumes are kept)
	docker compose down

build:  ## Restore + build the .NET solution
	dotnet build $(SLN)

test:  ## Run the full .NET test suite (needs Docker for the Postgres integration tests)
	dotnet test $(SLN)

run:  ## Start the app host on :8080 against your own Postgres (KORKUT_DB_URL)
	dotnet run --project src/services/Korkut.Api

migrate:  ## Apply EF Core migrations (uses KORKUT_ADMIN_DB_URL)
	dotnet run --project src/services/Korkut.Migrator -- migrate

ui-dev:  ## Vite dev server with hot reload, proxying /api to VITE_DEV_API_TARGET
	cd ui && npm install && npm run dev

ui-test:  ## Type-check and unit-test the React app
	cd ui && npm ci && npm run typecheck && npm test

website-dev:  ## Astro dev server for the website
	cd website && npm install && npm run dev
