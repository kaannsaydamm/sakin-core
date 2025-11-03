# Sakin Platform - Development Environment Makefile
# This Makefile provides convenient commands for local development

.PHONY: help up down restart logs ps test build clean lint format bootstrap verify init stop-clean

# Default target
.DEFAULT_GOAL := help

# Colors for output
BLUE := \033[0;34m
GREEN := \033[0;32m
YELLOW := \033[1;33m
RED := \033[0;31m
NC := \033[0m

# Project paths
PROJECT_ROOT := $(shell cd .. && cd .. && pwd)
COMPOSE_FILE := ../docker-compose.dev.yml
SOLUTION_FILE := $(PROJECT_ROOT)/SAKINCore-CS.sln

help: ## Show this help message
	@echo "$(BLUE)Sakin Platform - Development Tools$(NC)"
	@echo "====================================="
	@echo ""
	@echo "$(GREEN)Available targets:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(YELLOW)%-15s$(NC) %s\n", $$1, $$2}'
	@echo ""

up: ## Start all infrastructure services
	@echo "$(BLUE)🚀 Starting Sakin development environment...$(NC)"
	@cd .. && docker compose -f docker-compose.dev.yml up -d
	@echo "$(YELLOW)⏳ Waiting for services to be healthy...$(NC)"
	@sleep 5
	@$(MAKE) verify

down: ## Stop all services (preserves data)
	@echo "$(YELLOW)🛑 Stopping Sakin development environment...$(NC)"
	@cd .. && docker compose -f docker-compose.dev.yml down
	@echo "$(GREEN)✅ Services stopped (data preserved)$(NC)"

stop-clean: ## Stop all services and remove volumes (clean slate)
	@echo "$(RED)⚠️  Stopping services and removing all data...$(NC)"
	@cd .. && docker compose -f docker-compose.dev.yml down -v
	@echo "$(GREEN)✅ Services stopped and data removed$(NC)"

restart: ## Restart all services
	@$(MAKE) down
	@$(MAKE) up

logs: ## Show logs from all services
	@cd .. && docker compose -f docker-compose.dev.yml logs -f

ps: ## Show status of all services
	@cd .. && docker compose -f docker-compose.dev.yml ps

verify: ## Verify all services are healthy
	@echo "$(BLUE)🔍 Verifying services...$(NC)"
	@bash ../scripts/verify-services.sh || true

init: ## Initialize infrastructure (run after first up)
	@echo "$(BLUE)📊 Initializing infrastructure...$(NC)"
	@if [ -f "../scripts/opensearch/init-indices.sh" ]; then \
		OPENSEARCH_HOST=localhost:9200 bash ../scripts/opensearch/init-indices.sh; \
	else \
		echo "$(YELLOW)⚠️  OpenSearch init script not found$(NC)"; \
	fi
	@echo "$(GREEN)✅ Infrastructure initialized$(NC)"

build: ## Build all .NET projects
	@echo "$(BLUE)🔨 Building all .NET projects...$(NC)"
	@cd $(PROJECT_ROOT) && dotnet build $(SOLUTION_FILE)
	@echo "$(GREEN)✅ Build complete$(NC)"

test: ## Run all tests
	@echo "$(BLUE)🧪 Running all tests...$(NC)"
	@cd $(PROJECT_ROOT) && dotnet test $(SOLUTION_FILE)
	@echo "$(GREEN)✅ Tests complete$(NC)"

lint: ## Check code formatting
	@echo "$(BLUE)🔍 Checking code formatting...$(NC)"
	@cd $(PROJECT_ROOT) && dotnet format $(SOLUTION_FILE) --verify-no-changes || \
		(echo "$(YELLOW)⚠️  Code formatting issues found. Run 'make format' to fix.$(NC)" && exit 1)

format: ## Format code
	@echo "$(BLUE)✨ Formatting code...$(NC)"
	@cd $(PROJECT_ROOT) && dotnet format $(SOLUTION_FILE)
	@echo "$(GREEN)✅ Code formatted$(NC)"

clean: ## Clean build artifacts
	@echo "$(BLUE)🧹 Cleaning build artifacts...$(NC)"
	@cd $(PROJECT_ROOT) && dotnet clean $(SOLUTION_FILE)
	@find $(PROJECT_ROOT) -type d -name "bin" -o -name "obj" | xargs rm -rf
	@echo "$(GREEN)✅ Clean complete$(NC)"

bootstrap: ## Download MaxMind GeoLite DB placeholder
	@echo "$(BLUE)📦 Running bootstrap script...$(NC)"
	@bash ./bootstrap.sh
	@echo "$(GREEN)✅ Bootstrap complete$(NC)"

all: bootstrap build test ## Run bootstrap, build, and test

dev-setup: bootstrap up init ## Complete development setup (bootstrap + up + init)
	@echo ""
	@echo "$(GREEN)🎉 Development environment is ready!$(NC)"
	@echo ""
	@echo "$(BLUE)Next steps:$(NC)"
	@echo "  1. Run network sensor:"
	@echo "     cd $(PROJECT_ROOT)/sakin-core/services/network-sensor"
	@echo "     export Database__Host=localhost"
	@echo "     export Database__Password=postgres_dev_password"
	@echo "     sudo dotnet run"
	@echo ""
	@echo "  2. Access services:"
	@echo "     - OpenSearch Dashboards: http://localhost:5601"
	@echo "     - PostgreSQL: localhost:5432"
	@echo ""

check: lint test ## Run linting and tests

rebuild: clean build ## Clean and rebuild all projects

# Docker service management
db-shell: ## Open PostgreSQL shell
	@docker exec -it sakin-postgres psql -U postgres -d network_db

redis-cli: ## Open Redis CLI
	@docker exec -it sakin-redis redis-cli -a redis_dev_password

kafka-topics: ## List Kafka topics
	@docker exec -it sakin-kafka kafka-topics --bootstrap-server localhost:9092 --list

opensearch-health: ## Check OpenSearch cluster health
	@curl -s http://localhost:9200/_cluster/health?pretty

clickhouse-client: ## Open ClickHouse client
	@docker exec -it sakin-clickhouse clickhouse-client --user clickhouse --password clickhouse_dev_password
