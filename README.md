# Bottle Tycoon Microservice: Learn Microservices, Observability, and .NET 10

[![.NET CI](https://github.com/rangerchris/bottle-tycoon-microservice/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/rangerchris/bottle-tycoon-microservice/actions/workflows/dotnet-ci.yml)
[![coverage](https://img.shields.io/codecov/c/github/rangerchris/bottle-tycoon-microservice?logo=codecov&style=flat-square)](https://codecov.io/gh/rangerchris/bottle-tycoon-microservice)

Bottle Tycoon is an educational microservices project designed to teach modern architecture and observability practices through a simple, interactive game. The repository contains small, independently runnable ASP.NET Core services, a React frontend, and an observability stack (OpenTelemetry → Jaeger/Prometheus/Loki/Grafana).

Key differences from older versions
- The default development stack has been simplified: the API Gateway, RabbitMQ (MassTransit), and Redis are *removed from the default Docker Compose* setup. Services now communicate directly via HTTP endpoints for common flows. See `docs/ARCHITECTURE.md` for migration notes and guidance on reintroducing messaging or caching if needed.

Quick facts
- Runtime: .NET 10 (C# 14)
- Frontend: React
- Persistence: PostgreSQL (one container per service in compose)
- Observability: OpenTelemetry + Jaeger + Prometheus + Loki + Grafana

Services and local ports (compose, default)
- Frontend — http://localhost:3000
- Game Service — http://localhost:5001
- Recycler Service — http://localhost:5002
- Truck Service — http://localhost:5003
- Headquarters Service — http://localhost:5004
- Recycling Plant Service — http://localhost:5005
- Jaeger UI — http://localhost:16686
- Prometheus — http://localhost:9090
- Grafana — http://localhost:3001 (admin/admin)
- Loki — http://localhost:3100

Prerequisites
- Docker Desktop (with Docker Compose)
- .NET 10 SDK (for local host development)
- Node.js 18+ (for frontend dev)

Option A — Recommended: Run the full stack with Docker Compose
```bash
# from repository root
docker-compose up -d --build
# watch logs
docker-compose logs -f
```
Notes:
- The default `docker-compose.yml` runs each service and a per-service Postgres instance used for development/integration testing.
- The default compose file does NOT start RabbitMQ or Redis. If you need messaging/caching for a scenario, either update the compose file or run those services separately.

Option B — Local development (services on host)
```bash
# Start datastore & observability services
docker-compose up -d gameservicepostgres recyclerpostgres truckpostgres recyclingplantpostgres jaeger prometheus loki grafana

# Run services locally (in separate terminals)
cd src/GameService && dotnet run
cd src/RecyclerService && dotnet run
cd src/TruckService && dotnet run
cd src/HeadquartersService && dotnet run
cd src/RecyclingPlantService && dotnet run

# Frontend
cd src/Frontend && npm ci && npm start
```

Health checks & verification
```bash
# List running containers
docker-compose ps

# Check a service health endpoint (example Game Service)
curl http://localhost:5001/health
```

Observability
- Traces: Jaeger UI — http://localhost:16686
- Metrics: Prometheus — http://localhost:9090; each service exposes `/metrics` when instrumented
- Logs: Loki — http://localhost:3100; Grafana configured to visualize logs and metrics at http://localhost:3001

Grafana Dashboards (http://localhost:3001, admin/admin):
- **API health Overview** — Service health checks across all microservices
- **Database** — PostgreSQL metrics (availability, size, operations, connections)
- **Metrics Dashboard** — Grafana and Loki monitoring metrics
- **Recycler Bottles Metrics** — Bottles processed by type and customer arrival rates
- **Recyclers** — Current bottles by recycler (may require Grafana restart after first run)
- **Trucks** — Truck current load, capacity, and delivery metrics

> **Note**: If dashboards don't appear after starting Docker Compose, restart Grafana: `docker restart bottle-tycoon-grafana`

Testing
- Unit tests: `dotnet test` in each test project (xUnit v3)
- Integration tests: use the `docker-compose` integration setup in `docker-compose.yml` (the project is structured to allow integration tests to run against real Postgres containers)

Notes & next steps
- If you intend to restore the old event-driven flows, reintroduce RabbitMQ and MassTransit in a separate compose file or a production compose, and re-add shared contracts (or publish shared DTOs as a NuGet package). See `docs/ARCHITECTURE.md` for migration guidance.
- The repository previously referenced a `src/Shared` project for shared DTOs; that project is no longer present in the default layout. Use explicit HTTP contracts between services or publish shared contracts as a library if you need compile-time sharing.

Where to look next
- `docs/ARCHITECTURE.md` — architecture, ports, migration notes
- `docs/GAME_DESIGN.md` — game mechanics and UX design
- `src/` — service projects

License
This project is licensed under the MIT License - see the `LICENSE` file for details.