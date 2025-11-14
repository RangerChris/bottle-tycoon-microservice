let credits = 1000;
let totalEarnings = 0;
const MAX_RECYCLERS = 10;
const MAX_TRUCKS = 10;
let recyclers = [
    {
        id: 1,
        level: 0,
        capacity: 100,
        currentBottles: {
            glass: 15,
            metal: 10,
            plastic: 20
        }
    }
];
let trucks = [
    {
        id: 1,
        level: 0,
        capacity: 45, // changed from 100 to 45
        currentLoad: 0,
        status: 'idle'
    }
];
// Guards to prevent re-entrant purchase calls when multiple handlers exist
let buyingRecycler = false;
let buyingTruck = false;

// Chart data tracking
let bottlesChart = null;
let chartData = {
    labels: [],
    glass: [],
    metal: [],
    plastic: []
};
const MAX_CHART_POINTS = 10;

// Time control: 1=paused, 2=normal (x1), 3=x2, 4=x4, 5=x5
let timeLevel = 2; // start at normal
const timeMultipliers = {
    1: 0,
    2: 1,
    3: 2,
    4: 4,
    5: 5
};

// Ensure setTimeLevel exists before handlers use it
function setTimeLevel(level) {
    timeLevel = Math.max(1, Math.min(5, level));
    const mult = timeMultipliers[timeLevel];
    const label = document.getElementById('time-speed');
    if (label) {
        label.textContent = `Speed: ${mult === 0 ? 'Paused' : 'x' + mult}`;
    }
    // expose to global for inline handlers
    try { window.timeLevel = timeLevel; window.setTimeLevel = setTimeLevel; } catch (e) {}
    console.log('setTimeLevel ->', timeLevel, 'multiplier:', mult);
}

// expose setter globally in case inline onclick is used
try { window.setTimeLevel = setTimeLevel; } catch (e) {}

// Automatic visitor simulation
function createVisitorForRecycler(recyclerId) {
    const recycler = recyclers.find(r => r.id === recyclerId);
    if (!recycler) return null;
    const total = Math.floor(Math.random() * 21) + 5; // between 5 and 25 bottles
    const glass = Math.floor(Math.random() * (total + 1));
    const metal = Math.floor(Math.random() * (total - glass + 1));
    const plastic = total - glass - metal;
    const visitor = {
        id: Date.now() + Math.random(),
        total,
        remaining: total,
        bottles: { glass, metal, plastic }
    };
    recycler.visitor = visitor;
    addLogEntry(`Visitor arrived at Recycler #${recyclerId} with ${total} bottles (G:${glass}, M:${metal}, P:${plastic})`, 'info');
    return visitor;
}

function scheduleNextArrival(recyclerId, minSec = 5, maxSec = 20) {
    const delay = (Math.floor(Math.random() * (maxSec - minSec + 1)) + minSec) * 1000;
    setTimeout(() => {
        const recycler = recyclers.find(r => r.id === recyclerId);
        if (!recycler) return;
        // Don't create visitors when game is paused
        if (timeLevel === 1) {
            // Retry after a short delay when paused
            scheduleNextArrival(recyclerId, 2, 5);
            return;
        }
        if (recycler.visitor) {
            // already a visitor, try again later
            scheduleNextArrival(recyclerId, 3, 12);
            return;
        }
        createVisitorForRecycler(recyclerId);
    }, delay);
}

// Smart dispatch: find best recycler-truck matches
function attemptSmartDispatch() {
    const idleTrucks = trucks.filter(t => t.status === 'idle');
    if (idleTrucks.length === 0) return;

    // Calculate available bottles at each recycler
    const recyclerNeeds = recyclers.map(recycler => {
        const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic;
        return {
            id: recycler.id,
            recycler,
            totalBottles
        };
    }).filter(r => r.totalBottles > 0); // Only recyclers with bottles

    // Match trucks to recyclers - ONLY if recycler can fill truck completely
    for (const truck of idleTrucks) {
        const truckCapacity = calculateCapacity(truck.capacity, truck.level);

        // Find recyclers that have AT LEAST truck capacity worth of bottles (by count)
        const suitableRecyclers = recyclerNeeds.filter(r => r.totalBottles >= truckCapacity);

        if (suitableRecyclers.length > 0) {
            // Pick the one with most bottles
            const targetRecycler = suitableRecyclers.reduce((max, r) => r.totalBottles > max.totalBottles ? r : max);
            dispatchTruckToRecycler(truck, targetRecycler.recycler);
        }
        // If no recycler has enough bottles to fill the truck, truck stays idle
    }
}

// Dispatch a specific truck to a specific recycler
function dispatchTruckToRecycler(truck, recycler) {
    truck.status = 'to_recycler';
    truck.targetRecyclerId = recycler.id;
    const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic;
    addLogEntry(`Truck #${truck.id} dispatched to Recycler #${recycler.id} (${totalBottles} bottles available)`, 'info');
    renderTrucks();

    // Simulate travel to recycler
    setTimeout(() => {
        // Find the truck again to ensure we have current state
        const currentTruck = trucks.find(t => t.id === truck.id);
        if (!currentTruck || currentTruck.status !== 'to_recycler') return;

        const currentRecycler = recyclers.find(r => r.id === currentTruck.targetRecyclerId);
        if (!currentRecycler) {
            currentTruck.status = 'idle';
            renderTrucks();
            return;
        }

        addLogEntry(`Truck #${currentTruck.id} arrived at Recycler #${currentRecycler.id}`, 'info');
        currentTruck.status = 'loading';
        renderTrucks();

        // Simulate loading
        setTimeout(() => {
            // Calculate truck capacity (number of bottles, not units)
            const truckCapacity = calculateCapacity(currentTruck.capacity, currentTruck.level);
            console.log(`Truck #${currentTruck.id} loading: base capacity=${currentTruck.capacity}, level=${currentTruck.level}, calculated capacity=${truckCapacity} bottles`);
            const availableBottles = { ...currentRecycler.currentBottles };

            // Pick up bottles up to truck capacity (by bottle count)
            const picked = { glass: 0, metal: 0, plastic: 0 };
            let pickedCount = 0;

            // Pick bottles until we reach capacity (count-based, not weight-based)
            while (pickedCount < truckCapacity) {
                // Try to add one bottle of any available type
                if (availableBottles.glass > 0) {
                    picked.glass++;
                    availableBottles.glass--;
                    pickedCount++;
                } else if (availableBottles.metal > 0) {
                    picked.metal++;
                    availableBottles.metal--;
                    pickedCount++;
                } else if (availableBottles.plastic > 0) {
                    picked.plastic++;
                    availableBottles.plastic--;
                    pickedCount++;
                } else {
                    // No bottles left to pick
                    break;
                }
            }

            const pickedTotal = picked.glass + picked.metal + picked.plastic;
            const remainingTotal = availableBottles.glass + availableBottles.metal + availableBottles.plastic;

            // Log pickup with capacity info
            addLogEntry(`Truck #${currentTruck.id} picked up ${pickedTotal} bottles (G:${picked.glass}, M:${picked.metal}, P:${picked.plastic}) from Recycler #${currentRecycler.id}`, 'success');
            if (remainingTotal > 0) {
                addLogEntry(`Recycler #${currentRecycler.id} still has ${remainingTotal} bottles remaining`, 'info');
            }

            // Update recycler with remaining bottles
            currentRecycler.currentBottles = availableBottles;
            renderRecyclers();

            currentTruck.cargo = picked;
            currentTruck.status = 'to_plant';
            currentTruck.targetRecyclerId = null;
            renderTrucks();
            addLogEntry(`Truck #${currentTruck.id} heading to Recycling Plant with ${pickedTotal} bottles`, 'info');

            // Travel to plant
            setTimeout(() => {
                const plantTruck = trucks.find(t => t.id === currentTruck.id);
                if (!plantTruck || plantTruck.status !== 'to_plant') return;

                plantTruck.status = 'delivering';
                renderTrucks();
                const deliverTotal = plantTruck.cargo.glass + plantTruck.cargo.metal + plantTruck.cargo.plastic;
                addLogEntry(`Truck #${plantTruck.id} arrived at Recycling Plant with ${deliverTotal} bottles (G:${plantTruck.cargo.glass}, M:${plantTruck.cargo.metal}, P:${plantTruck.cargo.plastic})`, 'info');

                // Delivery processing
                setTimeout(() => {
                    const finalTruck = trucks.find(t => t.id === plantTruck.id);
                    if (!finalTruck || !finalTruck.cargo) return;

                    // Update chart with delivered bottles before clearing cargo
                    updateChart(finalTruck.cargo);
                    const value = calculateBottleValue(finalTruck.cargo);
                    updateCredits(Math.floor(value));
                    totalEarnings += Math.floor(value);
                    document.getElementById('total-earnings').textContent = totalEarnings.toLocaleString();
                    addLogEntry(`Truck #${finalTruck.id} delivered ${deliverTotal} bottles earning ${Math.floor(value)} credits`, 'success');

                    finalTruck.cargo = null;
                    finalTruck.currentLoad = 0;
                    finalTruck.status = 'idle';
                    renderTrucks();
                    renderRecyclers();

                    // Trigger smart dispatch for next pickup
                    attemptSmartDispatch();
                }, getTravelTime(2000)); // unloading time with traffic variance
            }, getTravelTime(4500)); // travel to plant with traffic variance
        }, getTravelTime(1500)); // loading time with traffic variance
    }, getTravelTime(3500)); // travel to recycler with traffic variance
}

// Per-second deposit loop: each second, active visitors deposit 1 bottle into their recycler
(function startDepositLoop() {
    console.log('Deposit loop started (1s tick)');
    setInterval(() => {
        const mult = timeMultipliers[timeLevel];
        console.debug('Deposit tick, current timeLevel:', timeLevel, 'mult:', mult);
        if (mult === 0) return; // paused

        let updated = false;
        recyclers.forEach(recycler => {
            const v = recycler.visitor;
            // deposit up to `mult` bottles this tick
            for (let i = 0; i < mult; i++) {
                if (v && v.remaining > 0) {
                    // Check if recycler has room
                    const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic;
                    const capacity = calculateCapacity(recycler.capacity, recycler.level);

                    if (totalBottles >= capacity) {
                        // Recycler is full, visitor must wait
                        if (!v.waiting) {
                            v.waiting = true;
                            addLogEntry(`Visitor at Recycler #${recycler.id} waiting - recycler is full (${totalBottles}/${capacity})`, 'warning');
                        }
                        break; // Stop trying to deposit for this recycler
                    }

                    // Reset waiting flag if there's room now
                    if (v.waiting) {
                        v.waiting = false;
                        addLogEntry(`Visitor at Recycler #${recycler.id} can now deposit - space available`, 'info');
                    }

                    const types = ['glass', 'metal', 'plastic'].filter(t => v.bottles[t] > 0);
                    if (types.length === 0) {
                        v.remaining = 0;
                        break;
                    }
                    const t = types[Math.floor(Math.random() * types.length)];
                    v.bottles[t]--;
                    v.remaining--;
                    recycler.currentBottles[t] = (recycler.currentBottles[t] || 0) + 1;
                    updated = true;
                } else {
                    break;
                }
            }

            if (v && v.remaining <= 0) {
                addLogEntry(`Visitor finished at Recycler #${recycler.id}`, 'info');
                recycler.visitor = null;
                scheduleNextArrival(recycler.id);
            }
        });

        if (updated) {
            renderRecyclers();
            // Check if any trucks should be dispatched
            attemptSmartDispatch();
        }
    }, 1000);
})();

// Periodic smart dispatch check (every 2 seconds)
(function startSmartDispatchLoop() {
    setInterval(() => {
        if (timeLevel === 1) return; // paused
        attemptSmartDispatch();
    }, 2000);
})();

// Ensure initial scheduling for all existing recyclers
function initVisitorScheduling() {
    recyclers.forEach(r => scheduleNextArrival(r.id, 1, 8));
}

function updateCredits(amount) {
    credits += amount;
    document.getElementById('credits').textContent = credits.toLocaleString();
}

function calculateBottleValue(bottles) {
    return (bottles.glass * 4) + (bottles.metal * 2.5) + (bottles.plastic * 1.75);
}

function calculateCapacity(baseCapacity, level) {
    return Math.floor(baseCapacity * Math.pow(1.25, level));
}

// Generate random travel time with variance to simulate traffic
function getTravelTime(baseTime) {
    // Add ±30% variance to simulate traffic conditions
    const variance = 0.3;
    const min = baseTime * (1 - variance);
    const max = baseTime * (1 + variance);
    return Math.floor(Math.random() * (max - min + 1)) + min;
}


function addLogEntry(message, type = 'info') {
    const logContainer = document.getElementById('activity-log');
    const entry = document.createElement('div');
    entry.className = `log-entry log-${type}`;

    const now = new Date();
    const timeString = now.toLocaleTimeString('en-US', { hour12: false });

    entry.innerHTML = `
        <span class="log-time">${timeString}</span>
        <span class="log-message">${message}</span>
    `;

    logContainer.insertBefore(entry, logContainer.firstChild);

    if (logContainer.children.length > 10) {
        logContainer.removeChild(logContainer.lastChild);
    }
}

function deliverBottles(recyclerId) {
    const randomBottles = {
        glass: Math.floor(Math.random() * 20) + 5,
        metal: Math.floor(Math.random() * 15) + 5,
        plastic: Math.floor(Math.random() * 25) + 10
    };

    const totalBottles = randomBottles.glass + randomBottles.metal + randomBottles.plastic;

    addLogEntry(`Delivered ${totalBottles} bottles to Recycler #${recyclerId}`, 'success');

    const recycler = recyclers.find(r => r.id === recyclerId);
    if (recycler) {
        recycler.currentBottles.glass += randomBottles.glass;
        recycler.currentBottles.metal += randomBottles.metal;
        recycler.currentBottles.plastic += randomBottles.plastic;
        renderRecyclers();

        // Check if trucks should be dispatched
        attemptSmartDispatch();
    }
}

function dispatchTruck(recyclerId) {
    const availableTruck = trucks.find(t => t.status === 'idle');
    if (!availableTruck) {
        addLogEntry('No trucks available!', 'warning');
        return;
    }

    availableTruck.status = 'picking';
    renderTrucks();

    setTimeout(() => {
        const recycler = recyclers.find(r => r.id === recyclerId);
        if (recycler) {
            const value = calculateBottleValue(recycler.currentBottles);
            updateCredits(Math.floor(value));
            totalEarnings += Math.floor(value);
            document.getElementById('total-earnings').textContent = totalEarnings.toLocaleString();

            addLogEntry(`Truck earned ${Math.floor(value)} credits from delivery!`, 'success');

            // Update chart with processed bottles
            updateChart(recycler.currentBottles);

            recycler.currentBottles = { glass: 0, metal: 0, plastic: 0 };
            availableTruck.status = 'idle';
            availableTruck.currentLoad = 0;

            renderRecyclers();
            renderTrucks();
        }
    }, 3000);
}

function upgradeRecycler(recyclerId) {
    const recycler = recyclers.find(r => r.id === recyclerId);
    if (!recycler) return;

    if (recycler.level >= 3) {
        addLogEntry('Recycler already at max level!', 'warning');
        return;
    }

    const upgradeCost = 200 * (recycler.level + 1);
    if (credits < upgradeCost) {
        addLogEntry('Not enough credits for upgrade!', 'warning');
        return;
    }

    updateCredits(-upgradeCost);
    recycler.level++;
    addLogEntry(`Recycler #${recyclerId} upgraded to Level ${recycler.level}`, 'success');
    renderRecyclers();
}

function upgradeTruck(truckId) {
    const truck = trucks.find(t => t.id === truckId);
    if (!truck) return;

    if (truck.level >= 3) {
        addLogEntry('Truck already at max level!', 'warning');
        return;
    }

    const upgradeCost = 300 * (truck.level + 1);
    if (credits < upgradeCost) {
        addLogEntry('Not enough credits for upgrade!', 'warning');
        return;
    }

    updateCredits(-upgradeCost);
    truck.level++;
    addLogEntry(`Truck #${truckId} upgraded to Level ${truck.level}`, 'success');
    renderTrucks();
}

function buyRecycler() {
    if (buyingRecycler) {
        console.warn('buyRecycler: purchase already in progress, ignoring duplicate call');
        return;
    }
    buyingRecycler = true;
    const cost = 500;
    addLogEntry('Buy Recycler button pressed', 'info');
    console.log('buyRecycler clicked. Current recyclers:', recyclers.length, 'credits:', credits);

    const buyBtn = document.getElementById('buy-recycler');
    if (buyBtn) {
        buyBtn.disabled = true;
        const originalText = buyBtn.textContent;
        buyBtn.textContent = 'Buying...';

        setTimeout(() => {
            // proceed with purchase logic after tiny delay to show feedback
            if (recyclers.length >= MAX_RECYCLERS) {
                addLogEntry(`Cannot purchase more than ${MAX_RECYCLERS} recyclers.`, 'warning');
                updateRecyclerControls();
                if (buyBtn) { buyBtn.disabled = false; buyBtn.textContent = originalText; }
                buyingRecycler = false;
                return;
            }

            if (credits < cost) {
                addLogEntry('Not enough credits to buy recycler!', 'warning');
                if (buyBtn) { buyBtn.disabled = false; buyBtn.textContent = originalText; }
                buyingRecycler = false;
                return;
            }

            updateCredits(-cost);
            const newId = recyclers.reduce((maxId, r) => Math.max(maxId, r.id), 0) + 1;
            recyclers.push({
                id: newId,
                level: 0,
                capacity: 100,
                currentBottles: { glass: 0, metal: 0, plastic: 0 }
            });

            // schedule visitor arrivals for the newly created recycler
            console.log(`Scheduling initial visitor for new Recycler #${newId}`);
            scheduleNextArrival(newId, 1, 8);

            addLogEntry(`Purchased Recycler #${newId}`, 'success');
            // keep UI in sync
            updateRecyclerControls();
            renderRecyclers();

            if (buyBtn) { buyBtn.disabled = false; buyBtn.textContent = originalText; }
            buyingRecycler = false;
        }, 120);
    } else {
        // ensure flag is reset appropriately in fallback path
        // fallback if button not available
        if (recyclers.length >= MAX_RECYCLERS) {
            addLogEntry(`Cannot purchase more than ${MAX_RECYCLERS} recyclers.`, 'warning');
            updateRecyclerControls();
            buyingRecycler = false;
            return;
        }

        if (credits < cost) {
            addLogEntry('Not enough credits to buy recycler!', 'warning');
            buyingRecycler = false;
            return;
        }

        updateCredits(-cost);
        const newId = recyclers.reduce((maxId, r) => Math.max(maxId, r.id), 0) + 1;
        recyclers.push({
            id: newId,
            level: 0,
            capacity: 100,
            currentBottles: { glass: 0, metal: 0, plastic: 0 }
        });

        // schedule visitor arrivals for the newly created recycler (fallback path)
        console.log(`Scheduling initial visitor for new Recycler #${newId} (fallback)`);
        scheduleNextArrival(newId, 1, 8);

        addLogEntry(`Purchased Recycler #${newId}`, 'success');
        updateRecyclerControls();
        renderRecyclers();
        buyingRecycler = false;
    }
}

function buyTruck() {
    if (buyingTruck) {
        console.warn('buyTruck: purchase already in progress, ignoring duplicate call');
        return;
    }
    buyingTruck = true;
    const cost = 800;
    if (trucks.length >= MAX_TRUCKS) {
        addLogEntry(`Cannot purchase more than ${MAX_TRUCKS} trucks.`, 'warning');
        updateTruckControls();
        buyingTruck = false;
        return;
    }
    if (credits < cost) {
        addLogEntry('Not enough credits to buy truck!', 'warning');
        buyingTruck = false;
        return;
    }

    updateCredits(-cost);
    const newId = trucks.reduce((maxId, t) => Math.max(maxId, t.id), 0) + 1;
    trucks.push({
        id: newId,
        level: 0,
        capacity: 45, // changed from 100 to 45
        currentLoad: 0,
        status: 'idle'
    });

    addLogEntry(`Purchased Truck #${newId}`, 'success');
    // keep UI in sync
    updateTruckControls();
    renderTrucks();
    buyingTruck = false;
}

function renderRecyclers() {
    console.log('renderRecyclers() called. recyclers:', recyclers.length);
    const container = document.getElementById('recyclers-container');
    if (!container) {
        console.error('renderRecyclers: #recyclers-container not found in DOM');
        return;
    }
    container.innerHTML = '';

    recyclers.forEach(recycler => {
        const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic;
        const capacity = calculateCapacity(recycler.capacity, recycler.level);
        const percentage = Math.min((totalBottles / capacity) * 100, 100);

        const card = document.createElement('div');
        card.className = 'bg-gray-800 p-4 rounded-lg border border-gray-700';
        card.dataset.id = recycler.id;
        card.innerHTML = `
            <div class="flex items-start justify-between">
                <div>
                    <h3 class="text-lg font-semibold">Recycler #${recycler.id}</h3>
                    <div class="text-sm text-gray-400">Level ${recycler.level}</div>
                </div>
                <div class="text-right">
                    <div class="text-sm text-gray-400">${Math.floor(percentage)}%</div>
                    <div class="text-xs text-gray-300">${totalBottles} / ${capacity} bottles</div>
                </div>
            </div>
            <div class="mt-3 bg-gray-700 h-3 rounded overflow-hidden">
                <div class="bg-emerald-500 h-full" style="width: ${percentage}%"></div>
            </div>
            <div class="mt-3 text-sm text-gray-300 space-y-1">
                <div>🟢 Glass: ${recycler.currentBottles.glass}</div>
                <div>⚪ Metal: ${recycler.currentBottles.metal}</div>
                <div>🔵 Plastic: ${recycler.currentBottles.plastic}</div>
            </div>
            <div class="mt-4 flex gap-2">
                <button class="btn btn-sm btn-success flex-1" onclick="deliverBottles(${recycler.id})">📦 Add bottles</button>
                <button class="btn btn-sm btn-warning" onclick="upgradeRecycler(${recycler.id})" ${recycler.level >= 3 ? 'disabled' : ''}>⬆️ Upgrade (${200 * (recycler.level + 1)})</button>
            </div>
        `;
        container.appendChild(card);
    });

    // keep totals and controls up-to-date whenever recyclers are rendered
    updateRecyclerControls();
}

// New: add debug logs in updateRecyclerControls
function updateRecyclerControls() {
    console.log('updateRecyclerControls(): recyclers=', recyclers.length, 'trucks=', trucks.length);
    const totalElement = document.getElementById('total-recyclers');
    const buyButton = document.getElementById('buy-recycler');
    if (totalElement) {
        totalElement.textContent = recyclers.length;
    } else {
        console.warn('updateRecyclerControls: #total-recyclers not found');
    }
    if (buyButton) {
        if (recyclers.length >= MAX_RECYCLERS) {
            buyButton.disabled = true;
            buyButton.classList.add('disabled');
            buyButton.textContent = `Max Recyclers (${MAX_RECYCLERS})`;
            buyButton.title = `You can have at most ${MAX_RECYCLERS} recyclers.`;
        } else {
            buyButton.disabled = false;
            buyButton.classList.remove('disabled');
            buyButton.textContent = `+ Buy Recycler (500 credits)`;
            buyButton.title = 'Purchase a new recycler (500 credits)';
        }
    } else {
        console.warn('updateRecyclerControls: #buy-recycler not found');
    }
}

function renderTrucks() {
    const container = document.getElementById('trucks-container');
    container.innerHTML = '';
    trucks.forEach(truck => {
        const capacity = calculateCapacity(truck.capacity, truck.level);
        const loadBottles = truck.cargo ? (truck.cargo.glass + truck.cargo.metal + truck.cargo.plastic) : truck.currentLoad;
        const percentage = Math.min((loadBottles / capacity) * 100, 100);
        let statusText = 'Idle';
        let statusClass = 'badge badge-outline';
        if (truck.status === 'to_recycler') { statusText = 'To Recycler'; statusClass = 'badge badge-info'; }
        else if (truck.status === 'loading') { statusText = 'Loading'; statusClass = 'badge badge-warning'; }
        else if (truck.status === 'to_plant') { statusText = 'To Plant'; statusClass = 'badge badge-info'; }
        else if (truck.status === 'delivering') { statusText = 'Delivering'; statusClass = 'badge badge-success'; }
        const card = document.createElement('div');
        card.className = 'bg-gray-800 p-4 rounded-lg border border-gray-700';
        card.dataset.id = truck.id;
        card.innerHTML = `
            <div class="flex items-start justify-between">
                <div>
                    <h3 class="text-lg font-semibold">Truck #${truck.id}</h3>
                    <div class="text-sm text-gray-400">Level ${truck.level}</div>
                </div>
                <div class="text-right">
                    <span class="${statusClass}">${statusText}</span>
                    <div class="text-xs text-gray-300 mt-1">${loadBottles} / ${capacity} bottles</div>
                </div>
            </div>
            <div class="mt-3 bg-gray-700 h-3 rounded overflow-hidden">
                <div class="bg-emerald-500 h-full" style="width: ${percentage}%"></div>
            </div>
            <div class="mt-4 flex gap-2">
                <button class="btn btn-sm btn-warning flex-1" onclick="upgradeTruck(${truck.id})" ${truck.level >= 3 ? 'disabled' : ''}>⬆️ Upgrade (${300 * (truck.level + 1)})</button>
            </div>
        `;
        container.appendChild(card);
    });
    updateTruckControls();
}

// Helper to keep the truck count and buy button state in sync
function updateTruckControls() {
    console.log('updateTruckControls(): trucks=', trucks.length);
    const totalElement = document.getElementById('total-trucks');
    const buyButton = document.getElementById('buy-truck');
    if (totalElement) {
        totalElement.textContent = trucks.length;
    } else {
        console.warn('updateTruckControls: #total-trucks not found');
    }
    if (buyButton) {
        if (trucks.length >= MAX_TRUCKS) {
            buyButton.disabled = true;
            buyButton.classList.add('disabled');
            buyButton.textContent = `Max Trucks (${MAX_TRUCKS})`;
            buyButton.title = `You can have at most ${MAX_TRUCKS} trucks.`;
        } else {
            buyButton.disabled = false;
            buyButton.classList.remove('disabled');
            buyButton.textContent = `+ Buy Truck (800 credits)`;
            buyButton.title = 'Purchase a new truck (800 credits)';
        }
    } else {
        console.warn('updateTruckControls: #buy-truck not found');
    }
}

// Initialize Chart.js
function initChart() {
    const ctx = document.getElementById('bottlesChart');
    if (!ctx) {
        console.warn('Chart canvas not found');
        return;
    }

    if (typeof Chart === 'undefined') {
        console.warn('Chart.js not available, skipping chart initialization.');
        return;
    }

    try {
        bottlesChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: chartData.labels,
                datasets: [
                    {
                        label: '🟢 Glass Bottles',
                        data: chartData.glass,
                        borderColor: 'rgb(34, 197, 94)',
                        backgroundColor: 'rgba(34, 197, 94, 0.1)',
                        tension: 0.4,
                        fill: true
                    },
                    {
                        label: '⚪ Metal Bottles',
                        data: chartData.metal,
                        borderColor: 'rgb(148, 163, 184)',
                        backgroundColor: 'rgba(148, 163, 184, 0.1)',
                        tension: 0.4,
                        fill: true
                    },
                    {
                        label: '🔵 Plastic Bottles',
                        data: chartData.plastic,
                        borderColor: 'rgb(59, 130, 246)',
                        backgroundColor: 'rgba(59, 130, 246, 0.1)',
                        tension: 0.4,
                        fill: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: 'rgb(209, 213, 219)',
                            font: {
                                size: 12
                            }
                        }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        backgroundColor: 'rgba(17, 24, 39, 0.9)',
                        titleColor: 'rgb(243, 244, 246)',
                        bodyColor: 'rgb(209, 213, 219)',
                        borderColor: 'rgb(75, 85, 99)',
                        borderWidth: 1
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: 'rgba(75, 85, 99, 0.3)'
                        },
                        ticks: {
                            color: 'rgb(156, 163, 175)'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(75, 85, 99, 0.3)'
                        },
                        ticks: {
                            color: 'rgb(156, 163, 175)',
                            stepSize: 10
                        }
                    }
                },
                interaction: {
                    mode: 'nearest',
                    axis: 'x',
                    intersect: false
                }
            }
        });
    } catch (e) {
        console.warn('Failed to initialize Chart.js:', e);
    }
}

// Update chart with new bottle data
function updateChart(bottles) {
    if (!bottlesChart) return;

    const now = new Date();
    const timeLabel = now.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });

    chartData.labels.push(timeLabel);
    chartData.glass.push(bottles.glass);
    chartData.metal.push(bottles.metal);
    chartData.plastic.push(bottles.plastic);

    // Keep only the last MAX_CHART_POINTS
    if (chartData.labels.length > MAX_CHART_POINTS) {
        chartData.labels.shift();
        chartData.glass.shift();
        chartData.metal.shift();
        chartData.plastic.shift();
    }

    bottlesChart.update();
}

// Attach event listeners defensively
(function attachControls() {
    function bind() {
        const br = document.getElementById('buy-recycler');
        const bt = document.getElementById('buy-truck');
        if (br) br.addEventListener('click', buyRecycler);
        if (bt) bt.addEventListener('click', buyTruck);

        // wire help buttons in the same binding to ensure elements exist
        const openHelp = document.getElementById('open-help');
        const modalCheckbox = document.getElementById('help-modal');
        if (openHelp) openHelp.addEventListener('click', () => { if(modalCheckbox) modalCheckbox.checked = true; });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bind);
    } else {
        bind();
    }
})();

// Wire time control buttons (defensive)
(function attachTimeControls() {
    function bindTime() {
        const slower = document.getElementById('time-slower');
        const pause = document.getElementById('time-pause');
        const faster = document.getElementById('time-faster');

        if (slower) slower.addEventListener('click', () => {
            setTimeLevel(Math.max(1, timeLevel - 1));
        });
        if (pause) pause.addEventListener('click', () => {
            setTimeLevel(1);
        });
        if (faster) faster.addEventListener('click', () => {
            setTimeLevel(Math.min(5, timeLevel + 1));
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindTime);
    } else {
        bindTime();
    }
})();

// set initial UI label
setTimeLevel(timeLevel);

// render and initial control sync
renderRecyclers();
renderTrucks();

// ensure the controls reflect current state on load
updateRecyclerControls();
updateTruckControls();

initChart(); // Initialize the chart
initVisitorScheduling(); // start visitor scheduling
addLogEntry('Welcome to Bottle Tycoon! Start delivering bottles to grow your empire.', 'info');