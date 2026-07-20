.PHONY: help build test run migrate docker-up
.DEFAULT_GOAL := help

SLN := dotnet/Korkut.slnx

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

build:  ## Restore + build the .NET solution
	dotnet build $(SLN)

test:  ## Run the full .NET test suite (needs Docker for Postgres/MinIO integration tests)
	dotnet test $(SLN)

run:  ## Start the API server on :8080
	dotnet run --project dotnet/src/Korkut.Api

migrate:  ## Apply EF Core migrations via the migrator (uses KORKUT_ADMIN_DB_URL)
	dotnet run --project dotnet/src/Korkut.Migrator

docker-up:  ## Build the API/worker/migrator images from dotnet/docker/
	docker build -f dotnet/docker/Dockerfile.api -t korkut-api dotnet
	docker build -f dotnet/docker/Dockerfile.worker -t korkut-worker dotnet
	docker build -f dotnet/docker/Dockerfile.migrator -t korkut-migrator dotnet
