# Bottle Tycoon Microservice: Learn Microservices, Observability, and .NET 9

## 🎮 Project Overview

Bottle Tycoon is an **educational microservices project** designed to teach modern software architecture principles through a fun, interactive game. Manage a virtual bottle recycling network with multiple microservices, each handling different business domains, all monitored with OpenTelemetry for complete observability.

### 🎯 Learning Objectives
- Build scalable microservices using **ASP.NET Core 9**
- Implement event-driven architecture with **RabbitMQ/MassTransit**
- Complete observability with **OpenTelemetry, Jaeger, Prometheus, Loki, and Grafana**
- Container orchestration with **Docker and Docker Compose**
- Real-time frontend with **React and DaisyUI**
- Distributed tracing and correlation IDs
- Health checks and service communication patterns
- Database per service pattern

---

## 📊 Architecture Overview

### Service Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                     React Frontend (3000)                    │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│           API Gateway (Port 5000)                            │
│  - Route requests  - JWT validation  - Rate limiting         │
└─┬──────────┬──────────┬──────────┬──────────┬──────────┬────┘
  │          │          │          │          │          │
┌─▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐
│Gm  │   │Rc   │   │Tk   │   │HQ   │   │Pln  │   │Auth │
│Svc │   │Svc  │   │Svc  │   │Svc  │   │Svc  │   │Svc  │
└────┘   └─────┘   └─────┘   └─────┘   └─────┘   └─────┘

Infrastructure:
┌─────────────┬──────────────┬─────────────┬──────────────┐
│ PostgreSQL  │ Redis        │ RabbitMQ    │ Observability│
│             │              │             │              │
│ Game DB     │ Cache/State  │ Event Bus   │ Jaeger       │
│ etc         │              │             │ Prometheus   │
└─────────────┴──────────────┴─────────────┴──────────────┘
```

### Services Breakdown

| Service | Purpose | Port | Container Count |
|---------|---------|------|-----------------|
| **Game Service** | Player state, credits, upgrades | 5001 | 1 |
| **Recycler Service** | Bottle collection, capacity tracking | 5002 | 3-5 (scalable) |
| **Truck Service** | Fleet management, deliveries | 5003 | 3-5 (scalable) |
| **Headquarters** | Dispatch coordination | 5004 | 1 |
| **Recycling Plant** | Credit calculation | 5005 | 1 |
| **API Gateway** | Request routing | 5000 | 1 |

---

## 🚀 Quick Start

### Prerequisites
- **Docker Desktop** (includes Docker & Docker Compose)
- **.NET 9 SDK** (for local development)
- **Node.js 18+** (for React frontend)
- **Git**

### Installation & Running

**Option 1: Docker Compose (Recommended - Everything in containers)**
```bash
# Clone the repository
git clone https://github.com/RangerChris/bottle-tycoon-microservice.git
cd bottle-tycoon-microservice

# Start all services
docker-compose up -d

# Wait 30 seconds for services to initialize
sleep 30

# Access the application
Frontend:        http://localhost:3000
API Gateway:     http://localhost:5000
Jaeger UI:       http://localhost:16686
Prometheus:      http://localhost:9090
Grafana:         http://localhost:3001 (admin/admin)
```

**Option 2: Local Development (Services running on host)**
```bash
# Terminal 1: PostgreSQL & Infrastructure
docker-compose up postgres redis rabbitmq jaeger prometheus loki grafana -d

# Terminal 2-7: Run each service
cd src/GameService && dotnet run
cd src/RecyclerService && dotnet run
# ... etc for other services

# Terminal 8: Frontend
cd src/Frontend && npm install && npm start
```

### Verify Services Are Running
```bash
# Check all containers
docker-compose ps

# Check API Gateway health
curl http://localhost:5000/health

# Check all services health
curl http://localhost:5000/health/ready
```

---

## 🎮 Game Mechanics

### Goal
Manage a bottle recycling network. Start with 1 recycler and 1 truck, grow your network by earning credits and upgrading equipment.

### Starting Resources
- 1 Recycler (capacity: 100 bottles)
- 1 Truck (capacity: 100 units)
- 1,000 starting credits

### Bottle Types & Values
| Type | Weight | Sell Price |
|------|--------|-----------|
| Glass | 2 units | 4 credits |
| Metal | 1 unit | 2.5 credits |
| Plastic | 1.4 units | 1.75 credits |

### Truck Capacity Calculation
```
Load = (Glass × 2) + (Metal × 1) + (Plastic × 1.4)
```

### Game Flow
1. **Deliver bottles** to recyclers
2. **Recycler reaches 90% capacity** → auto-requests truck
3. **Truck dispatches** from headquarters, picks up bottles
4. **Truck delivers** to recycling plant
5. **Credits earned** and added to player account
6. **Purchase upgrades** to increase recycler/truck capacity

### Upgrades
Each service can be upgraded 3 times. Each upgrade improves capacity by **+25%**.

**Example - Recycler Upgrades:**
- Level 0: 100 bottles
- Level 1: 125 bottles (+25%)
- Level 2: 156.25 bottles (+25%)
- Level 3: 195.3125 bottles (+25%)

---

## 🏗️ Technology Stack

### Backend Services
- **Runtime**: ASP.NET Core 9, C# 13
- **ORM**: Entity Framework Core
- **APIs**: Minimal APIs, OpenAPI/Swagger
- **Message Bus**: MassTransit (RabbitMQ)

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

---

## 📡 Observability & Monitoring

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

---

## 📚 Project Structure

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

## 🧪 Testing

### Test Coverage Target: >80%

**Unit Tests**
- Business logic: Capacity calculations, credit math, upgrades
- Run: `dotnet test`

**Integration Tests**
- Database operations, event publishing
- Service-to-service communication
- Run: `dotnet test --filter Category=Integration`

**End-to-End Tests**
- Full game flows: Create player → deliver bottles → earn credits
- Run: `npm test` (from Frontend directory)

**Load Tests**
- Multiple players simultaneously
- Rapid delivery sequences
- Tools: k6, Apache JMeter

---

## 🤝 Contributing

We welcome contributions! This is an educational project, and we'd love your help.

### Getting Started with Contributions
1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Make your changes**
4. **Add tests** for new functionality
5. **Run tests locally**: `docker-compose up && dotnet test`
6. **Commit with clear messages**: `git commit -m 'Add amazing feature'`
7. **Push to branch**: `git push origin feature/amazing-feature`
8. **Open a Pull Request**

### Areas to Contribute
- [ ] Additional services
- [ ] Performance optimizations
- [ ] Documentation improvements
- [ ] Test coverage
- [ ] UI/UX enhancements
- [ ] Bug fixes

See [CONTRIBUTING.md](CONTRIBUTING.md) for more details.

---

## 📖 Learn More

- [Architecture Documentation](docs/ARCHITECTURE.md) - Deep dive into design decisions
- [Service Specifications](docs/SERVICE_SPECS.md) - Detailed service specs
- [API Documentation](docs/API_DOCUMENTATION.md) - All endpoints
- [Observability Guide](docs/OBSERVABILITY.md) - How to use monitoring tools
- [Deployment Guide](docs/DEPLOYMENT.md) - Production setup

### Articles & Resources
- [Microservices Patterns](https://microservices.io/)
- [OpenTelemetry Documentation](https://opentelemetry.io/)
- [ASP.NET Core Docs](https://docs.microsoft.com/dotnet/core/)
- [React Documentation](https://react.dev)

---

## 🐛 Troubleshooting

### Services won't start
```bash
# Check Docker is running
docker --version

# Check ports aren't in use
lsof -i :5000,5001,5002,5003,5004,5005

# View logs
docker-compose logs -f [service_name]
```

### Database connection errors
```bash
# Ensure PostgreSQL is running
docker-compose ps postgres

# Check connection string in appsettings.json
# Reset database: docker-compose down -v && docker-compose up
```

### RabbitMQ not accepting connections
```bash
# Check RabbitMQ is healthy
docker-compose logs rabbitmq

# Access RabbitMQ management: http://localhost:15672 (guest/guest)
```

### Frontend not loading
```bash
# Check Node.js version
node --version  # Should be 18+

# Rebuild dependencies
cd src/Frontend && npm ci

# Clear cache and restart
npm run build && npm start
```

---

## 📝 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🌟 Acknowledgments

Built as an educational project to demonstrate:
- Microservices architecture patterns
- Distributed system design
- Observable systems with OpenTelemetry
- Container orchestration
- Event-driven architecture
- Modern .NET development practices

---

## 📞 Support & Questions

- **Issues**: [GitHub Issues](https://github.com/RangerChris/bottle-tycoon-microservice/issues)
- **Discussions**: [GitHub Discussions](https://github.com/RangerChris/bottle-tycoon-microservice/discussions)
- **Documentation**: Check [docs/](docs/) folder

---

**Happy learning! 🚀 Start contributing and building amazing microservices!**
