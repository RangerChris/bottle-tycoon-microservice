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

### UI
The frontend displays:
- Current credits
- Recycler status (current load, capacity)
- Truck status (current load, capacity)
- Upgrade options with costs
- Real-time updates on deliveries and earnings
- Charts showing earnings over time
- Buttons for buying upgrades, buying new recyclers/trucks