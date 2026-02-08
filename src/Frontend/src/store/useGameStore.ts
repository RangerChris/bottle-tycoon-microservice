import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import { BottleCounts, Recycler, Truck, LogEntry } from '../types'

// helper for unique ids used in logs and other transient entries
function uid() {
  return `${Date.now()}-${Math.random().toString(36).slice(2,9)}`
}

// helper to get correct API base URLs based on environment
function getApiBaseUrls() {
  const env = (import.meta as any).env || {}
  let base = env?.VITE_API_BASE_URL || 'http://localhost:5001'

  // When running in Docker, services communicate via container names, not localhost
  // Check if we're in a Docker environment by looking at the location
  const isDocker = typeof window !== 'undefined' && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1'

  if (isDocker) {
    // Use container service names for inter-container communication
    base = 'http://gameservice'
  }

  const gameServiceBase = base
  const recyclerBase = base.includes('5001') || base.includes('gameservice')
    ? (isDocker ? 'http://recyclerservice' : base.replace('5001', '5002'))
    : 'http://recyclerservice'
  const truckBase = base.includes('5001') || base.includes('gameservice')
    ? (isDocker ? 'http://truckservice' : base.replace('5001', '5003'))
    : 'http://truckservice'

  return { gameServiceBase, recyclerBase, truckBase }
}


export type GameState = {
  credits: number
  totalEarnings: number
  recyclers: Recycler[]
  trucks: Truck[]
  logs: LogEntry[]
  chartPoints: { time: number; bottles: BottleCounts }[]
  lastTick: number | null
  timeLevel: number
  buyingRecycler: boolean
  buyingTruck: boolean
  playerId: string | null
  // actions
  setTimeLevel: (level: number) => void
  addLog: (message: string, type?: LogEntry['type']) => void
  buyRecycler: () => void
  buyTruck: () => void
  deliverBottlesRandom: (recyclerId: number | string) => void
  upgradeRecycler: (recyclerId: number | string) => void
  upgradeTruck: (truckId: number | string) => void
  attemptSmartDispatch: () => void
  deliverToPlant: (truckId: number | string) => void
  depositTick: () => void
  createVisitorForRecycler: (recyclerId: number | string) => void
  scheduleNextArrival: (recyclerId: number | string, minSec?: number, maxSec?: number) => void
  reportRecyclerTelemetry: () => Promise<void>
  reportTruckTelemetry: () => Promise<void>
  // internal helpers for init
  init: () => Promise<void>
  fetchPlayer: () => Promise<void>
  initializeServices: () => Promise<void>
  fetchRecyclers: () => Promise<void>
  fetchTrucks: () => Promise<void>
}

// helper: calculate capacity based on level
function calculateCapacity(base: number, level: number) {
  return Math.floor(base * Math.pow(1.25, level))
}

// time multipliers mapping used by the frontend game loop
const timeMultipliers: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 }

// Track scheduled arrival timers to avoid duplicates per recycler
const scheduledArrivalTimers = new Map<number | string, any>()

// Watchdog interval id to ensure scheduling is active
let arrivalsWatchdog: number | null = null

const useGameStore = create(immer<GameState>((set, get) => ({
  credits: 0,
  totalEarnings: 0,
  recyclers: [
    { id: 1, level: 0, capacity: 100, currentBottles: { glass: 0, metal: 0, plastic: 0 }, visitors: [] }
  ],
  trucks: [
    { id: 1, level: 0, capacity: 45, currentLoad: 0, status: 'idle', targetRecyclerId: null, cargo: null }
  ],
  logs: [ { id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: 'Welcome to Bottle Tycoon! Start delivering bottles to grow your empire.' } ],
  chartPoints: [],
  lastTick: null,
  timeLevel: 2,
  buyingRecycler: false,
  buyingTruck: false,
  playerId: null,

  setTimeLevel: (level: number) => set((draft: any) => { draft.timeLevel = Math.max(1, Math.min(5, level)) }),

  addLog: (message: string, type: any = 'info') => set((draft: any) => {
    draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type, message })
    if (draft.logs.length > 50) draft.logs.pop()
  }),

  buyRecycler: async () => {
    const state = get()
    if (state.buyingRecycler) return
    set((draft: any) => { draft.buyingRecycler = true })
    const cost = 500

    if (state.recyclers.length >= 10) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Cannot purchase more recyclers.' }) }); return }
    if (state.credits < cost) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits to buy recycler!' }) }); return }

    try {
        const { recyclerBase } = getApiBaseUrls()

        const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId,
                name: `Recycler ${state.recyclers.length + 1}`,
                capacity: 100,
                location: 'Default'
            })
        })

        if (!response.ok) {
            set((draft: any) => {
                draft.buyingRecycler = false
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase recycler.' })
            })
            return
        }

        const newRecycler = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            draft.recyclers.push({
                id: newRecycler.id,
                name: newRecycler.name,
                level: 0,
                capacity: newRecycler.capacity,
                currentBottles: { glass: 0, metal: 0, plastic: 0 },
                visitors: []
            })
            draft.buyingRecycler = false
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased ${newRecycler.name}` })
        })

        get().scheduleNextArrival(newRecycler.id, 1, 8)

    } catch (error) {
        set((draft: any) => {
            draft.buyingRecycler = false;
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase recycler.' })
        });
    }
  },

  buyTruck: async () => {
    const state = get()
    if (state.buyingTruck) return
    set((draft: any) => { draft.buyingTruck = true })
    const cost = 800

    if (state.trucks.length >= 10) { set((draft: any) => { draft.buyingTruck = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Cannot purchase more trucks.' }) }); return }
    if (state.credits < cost) { set((draft: any) => { draft.buyingTruck = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits to buy truck!' }) }); return }

    try {
        const { truckBase } = getApiBaseUrls()

        const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId,
                id: crypto.randomUUID(),
                model: `Truck ${state.trucks.length + 1}`,
                isActive: true
            })
        })

        if (!response.ok) {
            set((draft: any) => {
                draft.buyingTruck = false
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase truck.' })
            })
            return
        }

        const newTruck = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            draft.trucks.push({
                id: newTruck.id,
                model: newTruck.model,
                level: newTruck.level || 0,
                capacity: 45,
                currentLoad: 0,
                status: 'idle',
                targetRecyclerId: null,
                cargo: null
            })
            draft.buyingTruck = false
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased ${newTruck.model}` })
        })

        await get().reportTruckTelemetry()
    } catch (error) {
        set((draft: any) => {
            draft.buyingTruck = false;
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase truck.' })
        });
    }
  },

  deliverBottlesRandom: async (recyclerId: number | string) => {
    const picked = { glass: Math.floor(Math.random() * 20) + 5, metal: Math.floor(Math.random() * 15) + 5, plastic: Math.floor(Math.random() * 25) + 10 }
    const total = picked.glass + picked.metal + picked.plastic

    try {
      const { recyclerBase } = getApiBaseUrls()

      const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/customers`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          customerType: 'Delivery',
          bottleCounts: picked
        })
      })

      if (!response.ok) {
        set((draft: any) => {
          draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to deliver bottles to recycler' })
        })
        return
      }
    } catch (error) {
      set((draft: any) => {
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to deliver bottles to recycler' })
      })
      return
    }

    set((draft: any) => {
      const r = draft.recyclers.find((x: any) => x.id == recyclerId)
      if (!r) return
      r.currentBottles.glass += picked.glass
      r.currentBottles.metal += picked.metal
      r.currentBottles.plastic += picked.plastic
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Delivered ${total} bottles to Recycler #${recyclerId}` })
    })
    setTimeout(() => get().attemptSmartDispatch(), 50)
  },

  upgradeRecycler: async (recyclerId: number | string) => {
    const state = get()
    const r = state.recyclers.find((x: any) => x.id == recyclerId)
    if (!r) return
    if (r.level >= 3) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Recycler already at max level!' }) }); return }
    const cost = 200 * (r.level + 1)
    if (state.credits < cost) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }) }); return }

    try {
        const { recyclerBase } = getApiBaseUrls()

        const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/upgrade`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId
            })
        })

        if (!response.ok) {
            set((draft: any) => {
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade recycler.' })
            })
            return
        }

        const updatedRecycler = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            const recycler = draft.recyclers.find((x: any) => x.id == recyclerId)
            if (recycler) {
                recycler.level = updatedRecycler.capacityLevel
                recycler.capacity = updatedRecycler.capacity
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `${recycler.name} upgraded to Level ${updatedRecycler.capacityLevel}` })
            }
        })
    } catch (error) {
        set((draft: any) => {
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade recycler.' })
        })
    }
  },

  upgradeTruck: async (truckId: number | string) => {
    const state = get()
    const t = state.trucks.find((x: any) => x.id == truckId)
    if (!t) return
    if (t.level >= 3) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Truck already at max level!' }) }); return }
    const cost = 300 * (t.level + 1)
    if (state.credits < cost) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }) }); return }

    try {
        const { truckBase } = getApiBaseUrls()

        const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck/${truckId}/upgrade`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId
            })
        })

        if (!response.ok) {
            set((draft: any) => {
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade truck.' })
            })
            return
        }

        const updatedTruck = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            const truck = draft.trucks.find((x: any) => x.id == truckId)
            if (truck) {
                truck.level = updatedTruck.level
                truck.capacity = calculateCapacity(45, truck.level)
                truck.model = updatedTruck.model
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `${truck.model} upgraded to Level ${updatedTruck.level}` })
            }
        })
    } catch (error) {
        set((draft: any) => {
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade truck.' })
        })
    }
  },

  attemptSmartDispatch: () => {
    const state = get()
    const mult = timeMultipliers[state.timeLevel] || 1
    for (const truck of state.trucks) {
      if (truck.status === 'idle') {
        const suitableRecycler = state.recyclers.find((recycler) => {
          const currentLoad = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
          return currentLoad >= recycler.capacity * 0.8
        })
        if (suitableRecycler) {
          set((draft: any) => {
            const updatedTruck = draft.trucks.find((t: any) => t.id == truck.id)
            const updatedRecycler = draft.recyclers.find((r: any) => r.id == suitableRecycler.id)
            if (updatedTruck && updatedRecycler) {
              updatedTruck.status = 'en route'
              updatedTruck.targetRecyclerId = updatedRecycler.id
              updatedTruck.cargo = { glass: 0, metal: 0, plastic: 0 }

              const glassToLoad = Math.min(updatedTruck.capacity - updatedTruck.currentLoad, updatedRecycler.currentBottles.glass)
              updatedTruck.cargo.glass += glassToLoad
              updatedRecycler.currentBottles.glass -= glassToLoad

              const metalToLoad = Math.min(updatedTruck.capacity - updatedTruck.currentLoad, updatedRecycler.currentBottles.metal)
              updatedTruck.cargo.metal += metalToLoad
              updatedRecycler.currentBottles.metal -= metalToLoad

              const plasticToLoad = Math.min(updatedTruck.capacity - updatedTruck.currentLoad, updatedRecycler.currentBottles.plastic)
              updatedTruck.cargo.plastic += plasticToLoad
              updatedRecycler.currentBottles.plastic -= plasticToLoad

              updatedTruck.currentLoad = updatedTruck.cargo.glass + updatedTruck.cargo.metal + updatedTruck.cargo.plastic

              draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Truck #${truck.id} dispatched to Recycler #${suitableRecycler.id}` })

              // Check if this recycler had a waiting visitor and space is now available
              const currentLoad = updatedRecycler.currentBottles.glass + updatedRecycler.currentBottles.metal + updatedRecycler.currentBottles.plastic
              if (updatedRecycler.visitors.length > 0 && updatedRecycler.visitors[0].waiting && currentLoad < updatedRecycler.capacity) {
                updatedRecycler.visitors[0].waiting = false
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor resumed depositing at Recycler #${suitableRecycler.id}` })
              }
            }
          })

          // Schedule arrival at plant after 10 seconds, adjusted for time multiplier
          const deliveryTime = Math.max(1000, 10000 / mult) // Minimum 1 second
          setTimeout(() => get().deliverToPlant(truck.id), deliveryTime)
        }
      }
    }
  },

  deliverToPlant: async (truckId: number | string) => {
    const state = get()
    const truck = state.trucks.find((t) => t.id == truckId)
    if (!truck || !truck.cargo) return

    const totalBottles = truck.cargo.glass + truck.cargo.metal + truck.cargo.plastic
    const earnings = (truck.cargo.glass * 4) + (truck.cargo.metal * 2.5) + (truck.cargo.plastic * 1.75)

    try {
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const base = envBase || 'http://localhost:5001'

        // Credit earnings
        await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: earnings,
                Reason: 'Earnings from recycling'
            })
        })
    } catch (error) {
        get().addLog('Failed to credit earnings.', 'error')
    }

    set((draft: any) => {
      const updatedTruck = draft.trucks.find((t: any) => t.id == truckId)
      if (updatedTruck) {
        updatedTruck.status = 'idle'
        updatedTruck.targetRecyclerId = null
        updatedTruck.currentLoad = 0
        updatedTruck.cargo = null

        draft.credits += earnings
        draft.totalEarnings += earnings
        draft.chartPoints.push({ time: Date.now(), bottles: truck.cargo })

        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Truck #${truckId} delivered ${totalBottles} bottles and earned ${earnings} credits.` })
      }
    })
  },

  depositTick: () => {
    const state = get()
    if (!state.playerId) return // Wait for player to be initialized

    const mult = timeMultipliers[state.timeLevel] || 1

    set((draft: any) => {
      for (const recycler of draft.recyclers) {
        if (recycler.visitors.length > 0 && recycler.visitors[0].remaining > 0) {
          const currentLoad = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
          const hasSpace = currentLoad < recycler.capacity

          if (hasSpace) {
            // Deposit bottles based on time multiplier
            for (let i = 0; i < mult; i++) {
              if (recycler.visitors[0].remaining <= 0) break

              // Deposit one bottle - randomly choose type from visitor's remaining bottles
              const visitorBottles = recycler.visitors[0].bottles
              const availableTypes = []
              if (visitorBottles.glass > 0) availableTypes.push('glass')
              if (visitorBottles.metal > 0) availableTypes.push('metal')
              if (visitorBottles.plastic > 0) availableTypes.push('plastic')

              if (availableTypes.length > 0) {
                const randomType = availableTypes[Math.floor(Math.random() * availableTypes.length)]
                recycler.currentBottles[randomType] += 1
                recycler.visitors[0].bottles[randomType] -= 1
                recycler.visitors[0].remaining -= 1
              }
            }

            // If visitor is done depositing, remove them and schedule next arrival
            if (recycler.visitors[0].remaining === 0) {
              draft.logs.unshift({
                id: uid(),
                time: new Date().toLocaleTimeString(),
                type: 'success',
                message: `Visitor finished depositing bottles at Recycler #${recycler.id}`
              })
              recycler.visitors.shift()
              // Schedule next visitor arrival
              get().scheduleNextArrival(recycler.id)
            }
          } else {
            // Recycler is full, mark visitor as waiting
            if (!recycler.visitors[0].waiting) {
              recycler.visitors[0].waiting = true
              draft.logs.unshift({
                id: uid(),
                time: new Date().toLocaleTimeString(),
                type: 'warning',
                message: `Visitor waiting at full Recycler #${recycler.id}`
              })
            }
          }
        }
      }
    })
  },

  createVisitorForRecycler: async (recyclerId: number | string) => {
    const state = get()
    const recycler = state.recyclers.find((r) => r.id == recyclerId)
    if (!recycler) return

    const totalBottles = Math.floor(Math.random() * 21) + 5
    const glass = Math.floor(Math.random() * (totalBottles + 1))
    const metal = Math.floor(Math.random() * (totalBottles - glass + 1))
    const plastic = totalBottles - glass - metal

    try {
      const { recyclerBase } = getApiBaseUrls()
      await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/visitors`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          Glass: glass,
          Metal: metal,
          Plastic: plastic,
          VisitorType: 'Regular'
        })
      })
    } catch (error) {
      get().addLog('Failed to notify backend of visitor arrival.', 'error')
    }

    set((draft: any) => {
      const visitor = {
        id: uid(),
        total: totalBottles,
        remaining: totalBottles,
        bottles: { glass, metal, plastic },
        waiting: false
      }
      draft.recyclers.find((r: any) => r.id == recyclerId)!.visitors.push(visitor)
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor arrived at Recycler #${recyclerId} with ${totalBottles} bottles` })
    })
  },

  scheduleNextArrival: (recyclerId: number | string, minSec: number = 5, maxSec: number = 20) => {
    const existing = scheduledArrivalTimers.get(recyclerId)
    if (existing) {
      clearTimeout(existing)
      scheduledArrivalTimers.delete(recyclerId)
    }

    const clampedMin = Math.max(1, minSec)
    const clampedMax = Math.max(clampedMin, maxSec)
    const delayMs = (Math.floor(Math.random() * (clampedMax - clampedMin + 1)) + clampedMin) * 1000

    const timer = setTimeout(() => {
      scheduledArrivalTimers.delete(recyclerId)
      get().createVisitorForRecycler(recyclerId)
      get().scheduleNextArrival(recyclerId, minSec, maxSec)
    }, delayMs)

    scheduledArrivalTimers.set(recyclerId, timer)
  },

  fetchPlayer: async () => {
    const { gameServiceBase } = getApiBaseUrls()
    try {
      const response = await fetch(`${gameServiceBase.replace(/\/$/, '')}/player`)
      if (!response.ok) {
        get().addLog('Failed to fetch player.', 'error')
        return
      }

      const players = await response.json()
      const player = Array.isArray(players) ? players[0] : null

      if (!player) {
        get().addLog('Player not found.', 'error')
        return
      }

      set((draft: any) => {
        draft.playerId = player.id
        draft.credits = player.credits ?? draft.credits
      })
    } catch (error) {
      get().addLog('Failed to fetch player.', 'error')
    }
  },

  fetchRecyclers: async () => {
    const { recyclerBase } = getApiBaseUrls()
    try {
      const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers`)
      if (!response.ok) {
        get().addLog('Failed to fetch recyclers.', 'error')
        return
      }

      const recyclers = await response.json()
      if (!Array.isArray(recyclers)) return

      set((draft: any) => {
        draft.recyclers = recyclers.map((r: any) => ({
          id: r.id,
          name: r.name,
          level: r.capacityLevel ?? 0,
          capacity: r.capacity ?? 100,
          currentBottles: { glass: 0, metal: 0, plastic: 0 },
          visitors: []
        }))
      })

      for (const recycler of recyclers) {
        get().scheduleNextArrival(recycler.id)
      }
    } catch (error) {
      get().addLog('Failed to fetch recyclers.', 'error')
    }
  },

  fetchTrucks: async () => {
    const { truckBase } = getApiBaseUrls()
    try {
      const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck`)
      if (!response.ok) {
        get().addLog('Failed to fetch trucks.', 'error')
        return
      }

      const trucks = await response.json()
      if (!Array.isArray(trucks)) return

      set((draft: any) => {
        draft.trucks = trucks.map((t: any) => ({
          id: t.id,
          model: t.model,
          level: t.level ?? 0,
          capacity: calculateCapacity(45, t.level ?? 0),
          currentLoad: 0,
          status: 'idle',
          targetRecyclerId: null,
          cargo: null
        }))
      })
    } catch (error) {
      get().addLog('Failed to fetch trucks.', 'error')
    }
  },

  initializeServices: async () => {
    const { gameServiceBase } = getApiBaseUrls()
    try {
      const response = await fetch(`${gameServiceBase.replace(/\/$/, '')}/initialize`, { method: 'POST' })
      if (!response.ok) {
        get().addLog('Failed to initialize services.', 'error')
      }
    } catch (error) {
      get().addLog('Failed to initialize services.', 'error')
    }
  },

  init: async () => {
    await get().initializeServices()
    await get().fetchPlayer()
    await get().fetchRecyclers()
    await get().fetchTrucks()
    await get().reportTruckTelemetry()

    if (arrivalsWatchdog === null) {
      arrivalsWatchdog = window.setInterval(() => {
        const state = get()
        for (const recycler of state.recyclers) {
          if (!scheduledArrivalTimers.has(recycler.id)) {
            get().scheduleNextArrival(recycler.id)
          }
        }
      }, 10000)
    }
  },

  reportRecyclerTelemetry: async () => {
    const state = get()
    const { recyclerBase } = getApiBaseUrls()
    const baseUrl = recyclerBase.replace(/\/$/, '')

    const requests = state.recyclers
      .filter(r => typeof r.id === 'string')
      .map(r => {
        const bottles = r.currentBottles || { glass: 0, metal: 0, plastic: 0 }
        return fetch(`${baseUrl}/recyclers/${r.id}/telemetry`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            bottleCounts: {
              glass: bottles.glass,
              metal: bottles.metal,
              plastic: bottles.plastic
            }
          })
        })
      })

    if (requests.length === 0) return

    await Promise.allSettled(requests)
  },

  reportTruckTelemetry: async () => {
    const state = get()
    const { truckBase } = getApiBaseUrls()
    const baseUrl = truckBase.replace(/\/$/, '')

    const requests = state.trucks
      .filter(t => typeof t.id === 'string')
      .map(t => {
        const currentLoad = t.currentLoad || 0
        const capacity = t.capacity || 45
        const status = t.status || 'idle'
        return fetch(`${baseUrl}/trucks/${t.id}/telemetry`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            currentLoad,
            capacity,
            status
          })
        })
      })

    if (requests.length === 0) return

    await Promise.allSettled(requests)
  }
})))

export default useGameStore