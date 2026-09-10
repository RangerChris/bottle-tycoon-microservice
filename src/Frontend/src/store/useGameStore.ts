import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import { BottleCounts, Recycler, Truck, LogEntry } from '../types'
import { operatingCostFor } from '../world/truckSim'
import { arrivalDelaySeconds } from '../world/arrival'

// helper for unique ids used in logs and other transient entries
function uid() {
  return `${Date.now()}-${Math.random().toString(36).slice(2,9)}`
}

// helper to get friendly recycler display name
function getRecyclerDisplayName(recycler: Recycler): string {
  return recycler.name || `Recycler ${recycler.id}`
}

// helper to get friendly truck display name
function getTruckDisplayName(truck: Truck): string {
  return truck.model || `Truck ${truck.id}`
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
  const recyclingPlantBase = base.includes('5001') || base.includes('gameservice')
    ? (isDocker ? 'http://recyclingplantservice' : base.replace('5001', '5005'))
    : 'http://recyclingplantservice'

  return { gameServiceBase, recyclerBase, truckBase, recyclingPlantBase }
}

async function postTruckTelemetry(truckId: number | string, currentLoad: number, capacity: number, status: string) {
  const { truckBase } = getApiBaseUrls()
  const baseUrl = truckBase.replace(/\/$/, '')

  try {
    await fetch(`${baseUrl}/trucks/${truckId}/telemetry`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        currentLoad,
        capacity,
        status
      })
    })
  } catch {
    // Best-effort telemetry only
  }
}

async function callTruckTelemetry(truckId: number | string, currentLoad: number, capacity: number, status: string): Promise<void> {
  const { truckBase } = getApiBaseUrls()
  const baseUrl = truckBase.replace(/\/$/, '')
  const response = await fetch(`${baseUrl}/trucks/${truckId}/telemetry`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ currentLoad, capacity, status })
  })
  if (!response.ok) {
    throw new Error(`Truck service returned ${response.status}`)
  }
}

const truckContactErrorTimestamps = new Map<number | string, number>()

function shouldLogTruckContactError(truckId: number | string): boolean {
  const last = truckContactErrorTimestamps.get(truckId) ?? 0
  if (Date.now() - last > 30000) {
    truckContactErrorTimestamps.set(truckId, Date.now())
    return true
  }
  return false
}

const recyclerContactErrorTimestamps = new Map<number | string, number>()
const unreachableRecyclers = new Set<number | string>()

function shouldLogRecyclerContactError(recyclerId: number | string): boolean {
  const last = recyclerContactErrorTimestamps.get(recyclerId) ?? 0
  if (Date.now() - last > 30000) {
    recyclerContactErrorTimestamps.set(recyclerId, Date.now())
    return true
  }
  return false
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
  buyRecyclerAt: (tile: { x: number; y: number }) => Promise<boolean>
  buyTruck: () => void
  sellRecycler: (recyclerId: number | string) => void
  sellTruck: (truckId: number | string) => void
  deliverBottlesRandom: (recyclerId: number | string) => void
  upgradeRecycler: (recyclerId: number | string) => void
  upgradeTruck: (truckId: number | string) => void
  attemptSmartDispatch: () => void
  deliverToPlant: (truckId: number | string, distanceTiles?: number) => void
  depositTick: () => void
  createVisitorForRecycler: (recyclerId: number | string) => void
  scheduleNextArrival: (recyclerId: number | string, minSec?: number, maxSec?: number) => void
  markVisitorArrived: (recyclerId: number | string, visitorId: number | string) => void
  reportRecyclerTelemetry: () => Promise<void>
  reportTruckTelemetry: () => Promise<void>
  reportGameTelemetry: () => Promise<void>
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

// helper: parse the service's "x,y" location string into tile coords
function parseLocation(location: unknown): { x: number; y: number } | null {
  if (typeof location !== 'string') return null
  const idx = location.indexOf(',')
  if (idx <= 0) return null
  const x = Number(location.slice(0, idx))
  const y = Number(location.slice(idx + 1))
  if (!Number.isFinite(x) || !Number.isFinite(y)) return null
  return { x, y }
}

// time multipliers mapping used by the frontend game loop
const timeMultipliers: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 }

// Telemetry reporting interval
let telemetryReportingInterval: number | null = null

const useGameStore = create(immer<GameState>((set, get) => ({
  credits: 0,
  totalEarnings: 0,
  recyclers: [
    { id: 1, level: 0, capacity: 100, currentBottles: { glass: 0, metal: 0, plastic: 0 }, visitors: [], targetedByTruckId: null }
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

  buyRecyclerAt: async (tile) => {
    const state = get()
    if (state.buyingRecycler) return false
    set((draft: any) => { draft.buyingRecycler = true })
    const cost = 500

    if (state.recyclers.length >= 10) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Cannot purchase more recyclers.' }) }); return false }
    if (state.credits < cost) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits to buy recycler!' }) }); return false }

    try {
        const { recyclerBase } = getApiBaseUrls()

        const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId,
                name: `Recycler ${state.recyclers.length + 1}`,
                capacity: 100,
                location: `${tile.x},${tile.y}`
            })
        })

        if (!response.ok) {
            set((draft: any) => {
                draft.buyingRecycler = false
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase recycler.' })
            })
            return false
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
                visitors: [],
                targetedByTruckId: null,
                location: tile
            })
            draft.buyingRecycler = false
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased ${newRecycler.name}` })
        })

        get().scheduleNextArrival(newRecycler.id, 1, 8)
        return true

    } catch (error) {
        set((draft: any) => {
            draft.buyingRecycler = false;
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase recycler.' })
        });
        return false
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
    } catch (error) {
        set((draft: any) => {
            draft.buyingTruck = false;
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase truck.' })
        });
    }
  },

  sellRecycler: async (recyclerId: number | string) => {
    const state = get()
    const recycler = state.recyclers.find(r => r.id == recyclerId)
    if (!recycler) return

    if (recycler.isBlockedForSale) {
      return
    }

    const recyclerName = recycler.name

    set((draft: any) => {
      draft.recyclers = draft.recyclers.filter((r: any) => r.id != recyclerId)
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Selling ${recyclerName}...` })
    })

    try {
      const { recyclerBase } = getApiBaseUrls()

      const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/sell`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          playerId: state.playerId,
          recyclerId: recyclerId
        })
      })

      if (!response.ok) {
        const errorText = await response.text()
        set((draft: any) => {
          draft.recyclers.push(recycler)
          draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: `Failed to sell recycler: ${errorText}` })
        })
        return
      }

      const result = await response.json()

      set((draft: any) => {
        draft.credits += result.creditsAwarded
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Sold ${result.recyclerName} for ${result.creditsAwarded} credits` })
      })

      await get().reportRecyclerTelemetry()
    } catch (error) {
      set((draft: any) => {
        draft.recyclers.push(recycler)
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to sell recycler' })
      })
    }
  },

  sellTruck: async (truckId: number | string) => {
    const state = get()
    const truck = state.trucks.find(t => t.id == truckId)
    if (!truck) return

    if (truck.isBlockedForSale) {
      return
    }

    const truckModel = truck.model

    set((draft: any) => {
      draft.trucks = draft.trucks.filter((t: any) => t.id != truckId)
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Selling ${truckModel}...` })
    })

    try {
      const { truckBase } = getApiBaseUrls()

      const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck/${truckId}/sell`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          playerId: state.playerId
        })
      })

      if (!response.ok) {
        const errorText = await response.text()
        set((draft: any) => {
          draft.trucks.push(truck)
          draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: `Failed to sell truck: ${errorText}` })
        })
        return
      }

      const result = await response.json()

      set((draft: any) => {
        draft.credits += result.creditsAwarded
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Sold ${result.truckModel} for ${result.creditsAwarded} credits` })
      })

    } catch (error) {
      set((draft: any) => {
        draft.trucks.push(truck)
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to sell truck' })
      })
    }
  },

  deliverBottlesRandom: async (recyclerId: number | string) => {
    const picked = { glass: Math.floor(Math.random() * 20) + 5, metal: Math.floor(Math.random() * 15) + 5, plastic: Math.floor(Math.random() * 25) + 10 }
    const total = picked.glass + picked.metal + picked.plastic
    const recyclerName = getRecyclerDisplayName(get().recyclers.find(r => r.id == recyclerId) ?? { id: recyclerId } as any)

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
        unreachableRecyclers.add(recyclerId)
        if (shouldLogRecyclerContactError(recyclerId)) {
          get().addLog(`${recyclerName} cannot be contacted.`, 'error')
        }
        return
      }
    } catch {
      unreachableRecyclers.add(recyclerId)
      if (shouldLogRecyclerContactError(recyclerId)) {
        get().addLog(`${recyclerName} cannot be contacted.`, 'error')
      }
      return
    }

    unreachableRecyclers.delete(recyclerId)
    recyclerContactErrorTimestamps.delete(recyclerId)

    set((draft: any) => {
      const r = draft.recyclers.find((x: any) => x.id == recyclerId)
      if (!r) return
      r.currentBottles.glass += picked.glass
      r.currentBottles.metal += picked.metal
      r.currentBottles.plastic += picked.plastic
      const recyclerName = getRecyclerDisplayName(r)
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Delivered ${total} bottles to ${recyclerName}` })
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
    void (async () => {
      const state = get()

      const availableRecyclers = state.recyclers
        .filter((recycler) => {
          const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
          const actualCapacity = calculateCapacity(100, recycler.level)
          const fillPercentage = (totalBottles / actualCapacity) * 100
          return fillPercentage >= 80 && !recycler.targetedByTruckId
        })
        .map((recycler) => {
          const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
          const actualCapacity = calculateCapacity(100, recycler.level)
          const fillPercentage = (totalBottles / actualCapacity) * 100
          return { recycler, totalBottles, fillPercentage }
        })
        .sort((a, b) => b.fillPercentage - a.fillPercentage)

      const idleTrucks = state.trucks
        .filter((truck) => truck.status === 'idle')
        .map((truck) => {
          const actualCapacity = calculateCapacity(45, truck.level)
          return { truck, actualCapacity }
        })
        .sort((a, b) => b.actualCapacity - a.actualCapacity)

      const matches = Math.min(availableRecyclers.length, idleTrucks.length)
      if (matches === 0) return

      for (let i = 0; i < matches; i++) {
        const { recycler } = availableRecyclers[i]
        const { truck, actualCapacity } = idleTrucks[i]
        const truckName = getTruckDisplayName(truck)

        try {
          await callTruckTelemetry(truck.id, 0, actualCapacity, 'transporting')
          truckContactErrorTimestamps.delete(truck.id)
        } catch {
          if (shouldLogTruckContactError(truck.id)) {
            get().addLog(`${truckName} cannot be contacted.`, 'error')
          }
          continue
        }

        set((draft: any) => {
          const updatedTruck = draft.trucks.find((t: any) => t.id == truck.id)
          const updatedRecycler = draft.recyclers.find((r: any) => r.id == recycler.id)

          if (updatedTruck && updatedRecycler) {
            updatedTruck.status = 'en route'
            updatedTruck.targetRecyclerId = updatedRecycler.id
            updatedTruck.capacity = actualCapacity
            updatedTruck.cargo = { glass: 0, metal: 0, plastic: 0 }
            updatedRecycler.targetedByTruckId = updatedTruck.id

            let remainingCapacity = actualCapacity

            const glassToLoad = Math.min(remainingCapacity, updatedRecycler.currentBottles.glass)
            updatedTruck.cargo.glass = glassToLoad
            updatedRecycler.currentBottles.glass -= glassToLoad
            remainingCapacity -= glassToLoad

            const metalToLoad = Math.min(remainingCapacity, updatedRecycler.currentBottles.metal)
            updatedTruck.cargo.metal = metalToLoad
            updatedRecycler.currentBottles.metal -= metalToLoad
            remainingCapacity -= metalToLoad

            const plasticToLoad = Math.min(remainingCapacity, updatedRecycler.currentBottles.plastic)
            updatedTruck.cargo.plastic = plasticToLoad
            updatedRecycler.currentBottles.plastic -= plasticToLoad

            updatedTruck.currentLoad = updatedTruck.cargo.glass + updatedTruck.cargo.metal + updatedTruck.cargo.plastic

            const recyclerName = getRecyclerDisplayName(updatedRecycler)
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `${truckName} dispatched to ${recyclerName}` })

            const currentLoad = updatedRecycler.currentBottles.glass + updatedRecycler.currentBottles.metal + updatedRecycler.currentBottles.plastic
            const recyclerCapacity = calculateCapacity(100, updatedRecycler.level)
            if (updatedRecycler.visitors.length > 0 && updatedRecycler.visitors[0].waiting && currentLoad < recyclerCapacity) {
              updatedRecycler.visitors[0].waiting = false
              draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor resumed depositing at ${recyclerName}` })
            }
          }
        })


        // The world journey fires deliverToPlant on visual arrival at the
        // plant (see store/worldBridge). This watchdog only backstops if the
        // world layer never starts a journey (e.g. unroutable recycler).
        setTimeout(() => {
          const t = get().trucks.find((x: any) => x.id == truck.id)
          if (t && t.cargo) get().deliverToPlant(truck.id)
        }, 30000)
      }
    })()
  },

  deliverToPlant: async (truckId: number | string, distanceTiles?: number) => {
    const state = get()
    const truck = state.trucks.find((t) => t.id == truckId)
    if (!truck || !truck.cargo) return

    const truckName = getTruckDisplayName(truck)

    try {
      await callTruckTelemetry(truckId, truck.currentLoad, truck.capacity, 'delivering')
      truckContactErrorTimestamps.delete(truckId)
    } catch {
      if (shouldLogTruckContactError(truckId)) {
        get().addLog(`${truckName} cannot be contacted.`, 'error')
      }
      setTimeout(() => get().deliverToPlant(truckId), 5000)
      return
    }


    const totalBottles = truck.cargo.glass + truck.cargo.metal + truck.cargo.plastic

    try {
        const { recyclingPlantBase, gameServiceBase } = getApiBaseUrls()

        // Process delivery at recycling plant
        const plantResponse = await fetch(`${recyclingPlantBase.replace(/\/$/, '')}/deliveries`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                truckId: truck.id,
                truckName: truck.model || `Truck ${truck.id}`,
                playerId: state.playerId,
                loadByType: {
                    glass: truck.cargo.glass,
                    metal: truck.cargo.metal,
                    plastic: truck.cargo.plastic
                },
                // Distance the world journey actually drove (0 on fallback timers).
                operatingCost: operatingCostFor(distanceTiles ?? 0, truck.level ?? 0)
            })
        })

        if (!plantResponse.ok) {
            get().addLog('Failed to process delivery at recycling plant.', 'error')
            return
        }

        const plantData = await plantResponse.json()
        const earnings = plantData.netEarnings

        // Credit earnings to player
        await fetch(`${gameServiceBase.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: earnings,
                Reason: 'Earnings from recycling'
            })
        })

        set((draft: any) => {
          const updatedTruck = draft.trucks.find((t: any) => t.id == truckId)
          if (updatedTruck) {
            const targetRecyclerId = updatedTruck.targetRecyclerId

            updatedTruck.status = 'idle'
            updatedTruck.targetRecyclerId = null
            updatedTruck.currentLoad = 0
            updatedTruck.cargo = null

            if (targetRecyclerId) {
              const targetRecycler = draft.recyclers.find((r: any) => r.id == targetRecyclerId)
              if (targetRecycler) {
                targetRecycler.targetedByTruckId = null
              }
            }

            draft.credits += earnings
            draft.totalEarnings += earnings
            draft.chartPoints.push({ time: Date.now(), bottles: truck.cargo })

            const truckName = getTruckDisplayName(updatedTruck)
            const cost = operatingCostFor(distanceTiles ?? 0, truck.level ?? 0)
            const costNote = cost > 0 ? ` (−${cost} operating cost)` : ''
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `${truckName} delivered ${totalBottles} bottles to the recycling plant and earned ${earnings} credits${costNote}.` })
          }
        })

        void postTruckTelemetry(truck.id, 0, truck.capacity || 45, 'idle')

        // Report telemetry immediately after delivery
        // Use setTimeout to ensure state has been updated
        setTimeout(() => {
          get().reportGameTelemetry()
        }, 0)
    } catch (error) {
        get().addLog('Failed to process delivery.', 'error')
    }
  },

  depositTick: () => {
    const state = get()
    if (!state.playerId) return // Wait for player to be initialized

    const mult = timeMultipliers[state.timeLevel] || 1
    const toSpawn: (number | string)[] = []

    set((draft: any) => {
      for (const recycler of draft.recyclers) {
        if (unreachableRecyclers.has(recycler.id)) continue
        // Customers only visit recyclers that exist in the world. The
        // /initialize seed recyclers (and bought-but-unplaced ones) stay
        // dormant until the player places them next to a road.
        if (!recycler.location) continue

        // Arrival countdown runs on the shared game clock (mult-aware), so
        // customers pause with the trucks instead of leaking through timeouts.
        if (recycler.nextArrivalIn === undefined) {
          recycler.nextArrivalIn = arrivalDelaySeconds(recycler.visitors.length, recycler.arrivalMinSec ?? 2, recycler.arrivalMaxSec ?? 8)
        } else {
          recycler.nextArrivalIn -= mult
        }
        if (recycler.nextArrivalIn <= 0) {
          toSpawn.push(recycler.id)
          recycler.nextArrivalIn = arrivalDelaySeconds(recycler.visitors.length, recycler.arrivalMinSec ?? 2, recycler.arrivalMaxSec ?? 8)
        }

        // Release a visitor whose walker never showed up (30s real-time backstop).
        const head = recycler.visitors[0]
        if (head && head.arrived === false && Date.now() - (head.arrivedPendingSince ?? Date.now()) > 30000) {
          head.arrived = true
        }

        if (recycler.visitors.length > 0 && recycler.visitors[0].arrived !== false && recycler.visitors[0].remaining > 0) {
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
              const recyclerName = getRecyclerDisplayName(recycler)
              draft.logs.unshift({
                id: uid(),
                time: new Date().toLocaleTimeString(),
                type: 'success',
                message: `Visitor finished depositing bottles at ${recyclerName}`
              })
              recycler.visitors.shift()
              // Schedule next visitor arrival
              get().scheduleNextArrival(recycler.id)
            }
          } else {
            // Recycler is full, mark visitor as waiting
            if (!recycler.visitors[0].waiting) {
              recycler.visitors[0].waiting = true
              const recyclerName = getRecyclerDisplayName(recycler)
              draft.logs.unshift({
                id: uid(),
                time: new Date().toLocaleTimeString(),
                type: 'warning',
                message: `Visitor waiting at full ${recyclerName}`
              })
            }
          }
        }
      }
    })

    // Spawn after the state update so the visitors array is settled.
    for (const id of toSpawn) void get().createVisitorForRecycler(id)
  },

  createVisitorForRecycler: async (recyclerId: number | string) => {
    const state = get()
    const recycler = state.recyclers.find((r) => r.id == recyclerId)
    if (!recycler || !recycler.location) return

    const totalBottles = Math.floor(Math.random() * 21) + 5
    const glass = Math.floor(Math.random() * (totalBottles + 1))
    const metal = Math.floor(Math.random() * (totalBottles - glass + 1))
    const plastic = totalBottles - glass - metal
    const recyclerName = getRecyclerDisplayName(recycler)

    try {
      const { recyclerBase } = getApiBaseUrls()
      const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/visitors`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          Glass: glass,
          Metal: metal,
          Plastic: plastic,
          VisitorType: 'Regular'
        })
      })

      if (!response.ok) {
        unreachableRecyclers.add(recyclerId)
        if (shouldLogRecyclerContactError(recyclerId)) {
          get().addLog(`${recyclerName} cannot be contacted.`, 'error')
        }
        return
      }
    } catch {
      unreachableRecyclers.add(recyclerId)
      if (shouldLogRecyclerContactError(recyclerId)) {
        get().addLog(`${recyclerName} cannot be contacted.`, 'error')
      }
      return
    }

    unreachableRecyclers.delete(recyclerId)
    recyclerContactErrorTimestamps.delete(recyclerId)

    set((draft: any) => {
      const visitor = {
        id: uid(),
        total: totalBottles,
        remaining: totalBottles,
        bottles: { glass, metal, plastic },
        waiting: false,
        // Deposits only start once the walker visually reaches the recycler
        // (world bridge calls markVisitorArrived) — or the 30s watchdog fires.
        arrived: false,
        arrivedPendingSince: Date.now()
      }
      const r = draft.recyclers.find((x: any) => x.id == recyclerId)
      if (r) {
        r.visitors.push(visitor)
        // The walker spawns at the map edge now; "arrived" is logged when it
        // actually reaches the recycler (see markVisitorArrived).
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Customer heading to ${recyclerName} with ${totalBottles} bottles` })
      }
    })
  },

  // Resets the arrival countdown for a recycler. depositTick decrements it on
  // the shared clock (pause/speed), so no setTimeout chains are needed.
  scheduleNextArrival: (recyclerId: number | string, minSec: number = 2, maxSec: number = 8) => {
    set((draft: any) => {
      const recycler = draft.recyclers.find((x: any) => x.id == recyclerId)
      if (!recycler) return
      recycler.arrivalMinSec = minSec
      recycler.arrivalMaxSec = maxSec
      recycler.nextArrivalIn = arrivalDelaySeconds(recycler.visitors.length, minSec, maxSec)
    })
  },

  // Called by the world bridge when a visitor walker physically reaches its
  // recycler stop — deposits for that visitor start from the next tick.
  markVisitorArrived: (recyclerId: number | string, visitorId: number | string) => {
    set((draft: any) => {
      const recycler = draft.recyclers.find((x: any) => x.id == recyclerId)
      const visitor = recycler?.visitors.find((v: any) => String(v.id) === String(visitorId))
      if (!visitor || visitor.arrived) return
      visitor.arrived = true
      const recyclerName = recycler ? getRecyclerDisplayName(recycler) : 'recycler'
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor arrived at ${recyclerName} with ${visitor.total} bottles` })
    })
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
        // Only recyclers placed in the world participate in the game. The
        // /initialize seed recycler has no location and stays invisible.
        draft.recyclers = recyclers.filter((r: any) => r.location).map((r: any) => ({
          id: r.id,
          name: r.name,
          level: r.capacityLevel ?? 0,
          capacity: r.capacity ?? 100,
          currentBottles: { glass: 0, metal: 0, plastic: 0 },
          visitors: [],
          targetedByTruckId: null,
          location: parseLocation(r.location)
        }))
      })

      // Arrival countdowns initialize lazily in depositTick — no timers needed.
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
    await get().reportRecyclerTelemetry()
    await get().reportGameTelemetry()

    if (telemetryReportingInterval === null) {
      telemetryReportingInterval = window.setInterval(() => {
        get().reportRecyclerTelemetry()
        get().reportGameTelemetry()
      }, 5000)
    }
  },

  reportRecyclerTelemetry: async () => {
    const state = get()
    const { recyclerBase } = getApiBaseUrls()
    const baseUrl = recyclerBase.replace(/\/$/, '')

    if (state.recyclers.length === 0) return

    await Promise.all(state.recyclers.map(async (r) => {
      const bottles = r.currentBottles || { glass: 0, metal: 0, plastic: 0 }
      const visitorCount = r.visitors?.length || 0
      try {
        const response = await fetch(`${baseUrl}/recyclers/${r.id}/telemetry`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            bottleCounts: { glass: bottles.glass, metal: bottles.metal, plastic: bottles.plastic },
            visitorCount,
            queueDepth: visitorCount
          })
        })
        if (!response.ok) throw new Error(`Recycler service returned ${response.status}`)
        unreachableRecyclers.delete(r.id)
        recyclerContactErrorTimestamps.delete(r.id)
      } catch {
        unreachableRecyclers.add(r.id)
        if (shouldLogRecyclerContactError(r.id)) {
          get().addLog(`${getRecyclerDisplayName(r)} cannot be contacted.`, 'error')
        }
      }
    }))
  },

  reportTruckTelemetry: async () => {
    const state = get()
    if (state.trucks.length === 0) return

    await Promise.all(state.trucks.map(async (truck) => {
      try {
        await callTruckTelemetry(
          truck.id,
          truck.currentLoad || 0,
          truck.capacity || 45,
          truck.status || 'idle'
        )
        truckContactErrorTimestamps.delete(truck.id)
      } catch {
        if (shouldLogTruckContactError(truck.id)) {
          get().addLog(`${getTruckDisplayName(truck)} cannot be contacted.`, 'error')
        }
      }
    }))
  },

  reportGameTelemetry: async () => {
    const state = get()
    if (!state.playerId) {
      return
    }

    const { gameServiceBase } = getApiBaseUrls()
    const baseUrl = gameServiceBase.replace(/\/$/, '')

    try {
      await fetch(`${baseUrl}/player/${state.playerId}/telemetry`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          totalEarnings: state.totalEarnings
        })
      })
    } catch (error) {
      get().addLog('Failed to report game telemetry.', 'error')
    }
  }
})))

export default useGameStore