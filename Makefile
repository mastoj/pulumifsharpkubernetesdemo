.PHONY: run-quiz
.SILENT: ;
.DEFAULT_GOAL := help

GIT_SHA:=$(shell git rev-parse --short HEAD)

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

restore-tool: ## Restore tools
	dotnet tool restore

restore-paket: ## Restore packages
	dotnet paket restore

restore: restore-tool restore-paket ## Restore all

build-deploy: restore ## Build deploy project
	dotnet build src/Deploy/Infra.fsproj

deploy: build-deploy ## Deploy with -y
	pulumi up -y -C src/Deploy

destroy: build-deploy ## Destroy with -y
	pulumi destroy -y -C src/Deploy
