.PHONY: help build test run migrate docker-up
.DEFAULT_GOAL := help

SLN := Korkut.slnx

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

build:  ## Restore + build the .NET solution
	dotnet build $(SLN)

test:  ## Run the full .NET test suite (needs Docker for the Postgres integration tests)
	dotnet test $(SLN)

run:  ## Start the API server on :8080
	dotnet run --project src/Korkut.Api

migrate:  ## Apply EF Core migrations via the migrator (uses KORKUT_ADMIN_DB_URL)
	dotnet run --project src/Korkut.Migrator

docker-up:  ## Build the API/worker/migrator images from docker/
	docker build -f docker/Dockerfile.api -t korkut-api .
	docker build -f docker/Dockerfile.worker -t korkut-worker .
	docker build -f docker/Dockerfile.migrator -t korkut-migrator .
