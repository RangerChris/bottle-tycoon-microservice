﻿## 🎮 Game Mechanics

### Goal
Manage a bottle recycling network. Start with 1 recycler and 1 truck, grow your network by earning credits and upgrading equipment.

### Starting Resources
- 1 Recycler (capacity: 100 bottles)
- 1 Truck (capacity: 45 bottles)
- 1,000 starting credits

### Bottle Types & Values
| Type | Sell Price |
|------|-----------|
| Glass | 4 credits |
| Metal | 2.5 credits |
| Plastic | 1.75 credits |

### Game Flow
1. **Customers arrive** at recyclers automatically at random intervals (2-8 seconds)
   - Each customer brings 5-25 bottles (random mix of glass, metal, plastic)
   - Customers deposit one bottle per second (affected by time speed)
   - **If recycler is full**, customer must wait until space becomes available
   - Waiting customers resume depositing once trucks pick up bottles
2. **Auto-dispatch system** monitors all recyclers and dispatches idle trucks when a recycler reaches **80% capacity**
3. **Truck pickup** removes bottles from recycler inventory and loads cargo
4. **Truck lifecycle with traffic simulation:**
   - Dispatched to recycler (2.5-4.5 seconds with traffic variance)
   - Arrives and loads bottles (1-2 seconds, picks up available bottles up to capacity)
   - Travels to recycling plant (3-6 seconds with traffic variance)
   - Delivers and earns credits (1.5-2.5 seconds unloading)
   - Returns to idle state
   - **Each trip varies** due to simulated traffic conditions (±30% time variance)
5. **Credits earned** from delivered bottles are added to player's account
6. **Purchase upgrades** for recyclers and trucks to increase capacity and efficiency

### Time Controls
- **Pause** - Stop all game activity
- **Normal (x1)** - Real-time speed
- **Fast (x2, x4, x5)** - Accelerate customer deposits and time

### Upgrades
Each service can be upgraded 3 times. Each upgrade improves capacity by **+25%**.

**Example - Truck Upgrades:**
- Level 0: 45 bottles
- Level 1: 56 bottles (+25%)
- Level 2: 70 bottles (+25%)
- Level 3: 87 bottles (+25%)

**Example - Recycler Upgrades:**
- Level 0: 100 bottles
- Level 1: 125 bottles (+25%)
- Level 2: 156 bottles (+25%)
- Level 3: 195 bottles (+25%)

### Purchasing
- **New Recycler:** 500 credits (max 10)
- **New Truck:** 800 credits (max 10)
- **Recycler Upgrade:** 200 × (level + 1) credits
- **Truck Upgrade:** 300 × (level + 1) credits

### UI Features
The frontend displays:
- **Credits and earnings** tracker
- **Time controls** (pause, slow, normal, fast)
- **Recycler cards** showing:
  - Current bottles by type (glass, metal, plastic)
  - Capacity and fill percentage
  - Level and upgrade options
  - Visual feedback when full (customers waiting)
- **Truck cards** showing:
  - Current load and capacity
  - Status (Idle, To Recycler, Loading, To Plant, Delivering)
  - Level and upgrade options
  - Dynamic travel times simulating traffic
- **Activity log** with real-time game events:
  - Customer arrivals and deposits
  - Customer waiting when recyclers are full
  - Truck dispatch and lifecycle events
  - Bottles picked up and delivered
  - Credits earned from deliveries
- **Line graph** tracking bottles processed over time by type
- **Help modal** with game rules and bottle values

### Game Mechanics Features
- **Customer queuing**: Customers wait when recyclers reach capacity and resume when space is available
- **Auto-dispatch at 80%**: Trucks dispatch automatically when recycler reaches 80% full
- **Traffic simulation**: Truck travel times vary ±30% each trip to simulate real-world traffic conditions
- **Real-time feedback**: Activity log shows all important events including waiting customers and traffic delays
- **Strategic depth**: Players must balance recycler capacity, truck count, and upgrade timing to maximize efficiency

---

This repository contains a simplified microservices demo for Bottle Tycoon. The architecture emphasizes direct HTTP APIs between services (no API Gateway), no message broker, and no Redis in the default development stack. Each service is an ASP.NET Core 10 app using Entity Framework Core and OpenAPI. Use `docker-compose.yml` for local orchestration; see `README.md` for run instructions.

Game mechanics and UI documentation are stored in the `docs/` folder. Services live under `src/` and each service exposes OpenAPI/Swagger UI when running in development.