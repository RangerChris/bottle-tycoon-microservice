# Copilot Development Guidelines

## Purpose
The purpose of these guidelines is to provide a comprehensive framework for using GitHub Copilot effectively within the bottle-tycoon-microservice repository. This document outlines best practices and strategies to integrate Copilot into our development workflow.

## Copilot behavior
- **General behavior:** Do not explain anything unless the user asks for it. Always keep explanations concise and to the point. If you don't know, ask, do not come up with an answer where you guess.
- **Code Suggestions:** Copilot assists by providing intelligent code completion and suggestions, allowing developers to focus on higher-level design rather than syntax.
- **Faster Iteration:** By generating boilerplate code and repetitive patterns, Copilot speeds up the coding process and enhances productivity.
- **Error Reduction:** With the AI's context awareness, Copilot helps decrease common coding errors, improving overall code quality.
- **Unknown API or code:** Use context7 MCP server to enhance Copilot's understanding of the codebase or the specific nugetpackage/API.
- **Logging:** Always write to log, never to console.

## Coding Conventions
- **Consistency:** Adhere to established coding styles and conventions to maintain readability and uniformity across the codebase.
- **Naming Conventions:** Use descriptive names for classes, methods, and variables to enhance code understandability.
- **Documentation:** Ensure that any code generated or modified with Copilot is well-documented, including comments that explain the purpose and functionality.
- **Simplicity:** Favor clear and straightforward code over complex or clever solutions. Prioritize maintainability and small methods and classes.
- **Comments:** When creating code, do not add any comments unless absolutely necessary.
- **Terminal:** When using Copilot in the terminal, assume that it is running in a PowerShell environment on a Windows 11 PC. You can not use && to chain commands. Multiline commands should be separated by semicolons (;).

## Architecture Patterns
- **Microservices:** Follow microservices architecture where services are independently deployable and scalable, promoting loose coupling.
- **Event-Driven Architecture:** Use event-driven patterns to allow services to communicate asynchronously, enhancing responsiveness and scalability.
- **API-First Design:** Design APIs first to ensure that all services interact through well-defined interfaces.
- **Separation of Concerns:** Ensure that each service has a single responsibility, making it easier to manage and evolve.
- **Project Structure:** Maintain a clear and organized project structure as outlined in the repository documentation based on .
- **Package Management:** Use centralized package management for dependencies to ensure consistency across services.

## Testing Approach
- **Unit Testing:** Write comprehensive unit tests for all new features and significant changes introduced via Copilot.
- **Integration Testing:** Ensure that interactions between services are tested and validated.
- **Code Coverage:** Aim for high code coverage and identify critical areas needing additional tests.

## Security Measures
- **Code Review:** Implement thorough code reviews for all contributions influenced by Copilot to catch potential security vulnerabilities.
- **Dependency Management:** Regularly update dependencies and utilize tools to monitor vulnerabilities in third-party libraries.
- **Security Best Practices:** Follow security best practices, including input validation and proper authentication mechanisms.

## Observability Requirements
- **Logging:** Implement structured logging across services to facilitate performance tracking and issue diagnostics.
- **Monitoring:** Use monitoring tools to observe the health and performance of services in real-time.
- **Alerting:** Set up alerts for critical failures and performance thresholds to enable rapid responses.

## Quality Checklist
- [ ] Code follows agreed-upon coding conventions.
- [ ] Sufficient tests are written and pass.
- [ ] Code is reviewed and approved by at least one other developer.
- [ ] Documentation is updated for any changes made.
- [ ] Compliance with security measures is confirmed.

## Technology Stack

### Backend Services
- **Runtime**: ASP.NET Core 9, C# 13
- **ORM**: Entity Framework Core
- **APIs**: FastEndpoints, OpenAPI/Swagger, Serilog
- **Message Bus**: MassTransit (RabbitMQ)
- **Testing**: xUnit (xunit.v3), Moq, Shouldly (v4.3.0)

### Data & Caching
- **Primary DB**: PostgreSQL
- **Cache**: Redis
- **Message Broker**: RabbitMQ

### Frontend
- **Framework**: React 19
- **Styling**: Tailwind CSS + DaisyUI
- **Data Fetching**: TanStack Query (React Query)
- **State Management**: Zustand
- **Charting**: Recharts
- **Real-time**: Socket.io

### Observability
- **Distributed Tracing**: OpenTelemetry + Jaeger
- **Metrics**: Prometheus
- **Logging**: Structured logging + Loki
- **Visualization**: Grafana
- **Correlation**: W3C Trace Context

### Infrastructure
- **Containerization**: Docker
- **Orchestration**: Docker Compose
- **Networking**: Docker network (internal communication)

### Testing
- **Unit test**: Use xunit.v3 and testcontainers where applicable
- **Integration test**: Use docker-compose test environment with real dependencies
- **Test creation**: When working on new features, follow TDD approach: write tests first, then implement minimal code to pass tests, then refactor.

## Observability & Monitoring

### Key Monitoring Features

**Distributed Tracing**
- All requests traced end-to-end via OpenTelemetry
- View at: http://localhost:16686 (Jaeger UI)
- See service dependencies, latencies, errors

**Metrics Collection**
- Prometheus scrapes all services every 15 seconds
- Pre-built dashboards in Grafana
- Track request rates, errors, latencies, business metrics

**Structured Logging**
- All logs queryable in Loki
- Correlation IDs link related requests
- Access via Grafana Explore or Loki UI

**Health Checks**
- Liveness probe: `/health/live` - Is service running?
- Readiness probe: `/health/ready` - Ready to handle traffic?
- Dependency checks included

### Grafana Dashboards
Pre-configured dashboards for:
- Overall system health
- Per-service performance
- Business metrics (credits, deliveries, earnings)
- Infrastructure health (database, message broker)

Access Grafana: http://localhost:3001 (admin/admin)


## Project Structure

```
bottle-tycoon-microservice/
├── docs/
│   ├── ARCHITECTURE.md           # Detailed architecture docs
│   ├── SERVICE_SPECS.md          # Service specifications
│   ├── API_DOCUMENTATION.md      # API reference
│   ├── OBSERVABILITY.md          # Telemetry setup guide
│   └── DEPLOYMENT.md             # Production deployment
├── src/
│   ├── ApiGateway/
│   │   ├── ApiGateway.csproj
│   │   ├── Program.cs
│   │   ├── Middleware/
│   │   └── Routes/
│   ├── GameService/
│   │   ├── GameService.csproj
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Program.cs
│   ├── RecyclerService/
│   ├── TruckService/
│   ├── HeadquartersService/
│   ├── RecyclingPlantService/
│   ├── Shared/                   # Shared DTOs, interfaces
│   └── Frontend/
│       ├── src/
│       │   ├── components/
│       │   ├── hooks/
│       │   ├── services/
│       │   ├── store/
│       │   └── App.tsx
│       ├── package.json
│       └── vite.config.ts
├── docker-compose.yml            # Full stack orchestration
├── docker-compose.dev.yml        # Development setup
├── .env.example                  # Environment variables
├── Dockerfile                    # Multi-stage build
├── CONTRIBUTING.md               # Contribution guidelines
├── LICENSE                       # MIT License
└── README.md                     # This file
```

---