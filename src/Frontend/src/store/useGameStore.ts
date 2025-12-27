import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import { BottleCounts, Recycler, Truck, LogEntry } from '../types'

// helper for unique ids used in logs and other transient entries
function uid() {
  return `${Date.now()}-${Math.random().toString(36).slice(2,9)}`
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
  // internal helpers for init
  init: () => Promise<void>
  fetchPlayer: () => Promise<void>
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
    { id: 1, level: 0, capacity: 100, currentBottles: { glass: 15, metal: 10, plastic: 20 }, visitor: null }
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
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const base = envBase || 'http://localhost:5001'

        // Debit credits
        const debitResponse = await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deduct`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: cost,
                Reason: 'Purchased recycler'
            })
        })

        if (!debitResponse.ok) {
            throw new Error('Failed to debit credits')
        }

        const recyclerBase = base.includes('5001') ? base.replace('5001', '5002') : 'http://localhost:5002'

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
            // If recycler creation fails, credit back
            await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    Amount: cost,
                    Reason: 'Refund recycler purchase'
                })
            })
            throw new Error('Failed to buy recycler')
        }

        const newRecycler = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            draft.recyclers.push({
                id: newRecycler.id,
                level: 0,
                capacity: newRecycler.capacity,
                currentBottles: { glass: 0, metal: 0, plastic: 0 },
                visitor: null
            })
            draft.buyingRecycler = false
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased Recycler #${newRecycler.id.toString().substring(0, 8)}` })
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
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const base = envBase || 'http://localhost:5001'

        // Debit credits
        const debitResponse = await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deduct`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: cost,
                Reason: 'Purchased truck'
            })
        })

        if (!debitResponse.ok) {
            throw new Error('Failed to debit credits')
        }

        const truckBase = base.includes('5001') ? base.replace('5001', '5003') : 'http://localhost:5003'

        const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId,
                id: crypto.randomUUID(),
                model: 'Standard Truck',
                isActive: true
            })
        })

        if (!response.ok) {
            // If truck creation fails, credit back
            await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    PlayerId: state.playerId,
                    Amount: cost,
                    Reason: 'Refund truck purchase'
                })
            })
            throw new Error('Failed to buy truck')
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
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased Truck #${newTruck.id.toString().substring(0, 8)}` })
        })
    } catch (error) {
        set((draft: any) => {
            draft.buyingTruck = false;
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to purchase truck.' })
        });
    }
  },

  deliverBottlesRandom: (recyclerId: number | string) => {
    const picked = { glass: Math.floor(Math.random() * 20) + 5, metal: Math.floor(Math.random() * 15) + 5, plastic: Math.floor(Math.random() * 25) + 10 }
    const total = picked.glass + picked.metal + picked.plastic
    set((draft: any) => {
      const r = draft.recyclers.find((x: any) => x.id === recyclerId)
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
    const r = state.recyclers.find((x: any) => x.id === recyclerId)
    if (!r) return
    if (r.level >= 3) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Recycler already at max level!' }) }); return }
    const cost = 200 * (r.level + 1)
    if (state.credits < cost) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }) }); return }

    try {
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const base = envBase || 'http://localhost:5001'

        // Debit credits
        const debitResponse = await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deduct`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: cost,
                Reason: 'Upgraded recycler'
            })
        })

        if (!debitResponse.ok) {
            throw new Error('Failed to debit credits')
        }

        const recyclerBase = base.includes('5001') ? base.replace('5001', '5002') : 'http://localhost:5002'

        const response = await fetch(`${recyclerBase.replace(/\/$/, '')}/recyclers/${recyclerId}/upgrade`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId
            })
        })

        if (!response.ok) {
            // If upgrade fails, credit back
            await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    PlayerId: state.playerId,
                    Amount: cost,
                    Reason: 'Refund recycler upgrade'
                })
            })
            throw new Error('Failed to upgrade recycler')
        }

        const updatedRecycler = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            const recycler = draft.recyclers.find((x: any) => x.id === recyclerId)
            if (recycler) {
                recycler.level = updatedRecycler.capacityLevel
                recycler.capacity = updatedRecycler.capacity
            }
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Recycler #${recyclerId} upgraded to Level ${updatedRecycler.capacityLevel}` })
        })
    } catch (error) {
        set((draft: any) => {
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade recycler.' })
        })
    }
  },

  upgradeTruck: async (truckId: number | string) => {
    const state = get()
    const t = state.trucks.find((x: any) => x.id === truckId)
    if (!t) return
    if (t.level >= 3) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Truck already at max level!' }) }); return }
    const cost = 300 * (t.level + 1)
    if (state.credits < cost) { set((draft: any) => { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }) }); return }

    try {
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const base = envBase || 'http://localhost:5001'

        // Debit credits
        const debitResponse = await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deduct`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                PlayerId: state.playerId,
                Amount: cost,
                Reason: 'Upgraded truck'
            })
        })

        if (!debitResponse.ok) {
            throw new Error('Failed to debit credits')
        }

        const truckBase = base.includes('5001') ? base.replace('5001', '5003') : 'http://localhost:5003'

        const response = await fetch(`${truckBase.replace(/\/$/, '')}/truck/${truckId}/upgrade`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                playerId: state.playerId
            })
        })

        if (!response.ok) {
            // If upgrade fails, credit back
            await fetch(`${base.replace(/\/$/, '')}/player/${state.playerId}/deposit`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    PlayerId: state.playerId,
                    Amount: cost,
                    Reason: 'Refund truck upgrade'
                })
            })
            throw new Error('Failed to upgrade truck')
        }

        const updatedTruck = await response.json()

        set((draft: any) => {
            draft.credits -= cost
            const truck = draft.trucks.find((x: any) => x.id === truckId)
            if (truck) {
                truck.level = updatedTruck.Level
                truck.capacity = calculateCapacity(45, truck.level)
                truck.model = updatedTruck.Model
            }
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Truck #${truckId} upgraded to Level ${updatedTruck.Level}` })
        })
    } catch (error) {
        set((draft: any) => {
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to upgrade truck.' })
        })
    }
  },

  attemptSmartDispatch: () => {
    const state = get()
    for (const truck of state.trucks) {
      if (truck.status === 'idle') {
        const suitableRecycler = state.recyclers.find((recycler) => recycler.currentBottles.glass > 0 || recycler.currentBottles.metal > 0 || recycler.currentBottles.plastic > 0)
        if (suitableRecycler) {
          set((draft: any) => {
            const updatedTruck = draft.trucks.find((t: any) => t.id === truck.id)
            const updatedRecycler = draft.recyclers.find((r: any) => r.id === suitableRecycler.id)
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
              if (updatedRecycler.visitor && updatedRecycler.visitor.waiting && currentLoad < updatedRecycler.capacity) {
                updatedRecycler.visitor.waiting = false
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor resumed depositing at Recycler #${suitableRecycler.id}` })
              }
            }
          })

          // Schedule arrival at plant after 10 seconds
          setTimeout(() => get().deliverToPlant(truck.id), 10000)
        }
      }
    }
  },

  deliverToPlant: async (truckId: number | string) => {
    const state = get()
    const truck = state.trucks.find((t) => t.id === truckId)
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
        console.error('Failed to credit earnings:', error)
    }

    set((draft: any) => {
      const updatedTruck = draft.trucks.find((t: any) => t.id === truckId)
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

    set((draft: any) => {
      for (const recycler of draft.recyclers) {
        if (recycler.visitor && recycler.visitor.remaining > 0) {
          const currentLoad = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
          const hasSpace = currentLoad < recycler.capacity

          if (hasSpace) {
            // Deposit one bottle - randomly choose type from visitor's remaining bottles
            const visitorBottles = recycler.visitor.bottles
            const availableTypes = []
            if (visitorBottles.glass > 0) availableTypes.push('glass')
            if (visitorBottles.metal > 0) availableTypes.push('metal')
            if (visitorBottles.plastic > 0) availableTypes.push('plastic')

            if (availableTypes.length > 0) {
              const randomType = availableTypes[Math.floor(Math.random() * availableTypes.length)]
              recycler.currentBottles[randomType] += 1
              recycler.visitor.bottles[randomType] -= 1
              recycler.visitor.remaining -= 1

              // Log the deposit
              draft.logs.unshift({
                id: uid(),
                time: new Date().toLocaleTimeString(),
                type: 'info',
                message: `Visitor deposited 1 ${randomType} bottle at Recycler #${recycler.id}`
              })

              // If visitor is done depositing, remove them and schedule next arrival
              if (recycler.visitor.remaining === 0) {
                draft.logs.unshift({
                  id: uid(),
                  time: new Date().toLocaleTimeString(),
                  type: 'success',
                  message: `Visitor finished depositing bottles at Recycler #${recycler.id}`
                })
                recycler.visitor = null
                // Schedule next visitor arrival
                setTimeout(() => get().scheduleNextArrival(recycler.id), 100)
              }
            }
          } else {
            // Recycler is full, mark visitor as waiting
            if (!recycler.visitor.waiting) {
              recycler.visitor.waiting = true
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

  createVisitorForRecycler: (recyclerId: number | string) => {
    const state = get()
    const recycler = state.recyclers.find((r) => r.id === recyclerId)
    if (!recycler) return

    // Generate random bottles for visitor (5-25 total bottles)
    const totalBottles = Math.floor(Math.random() * 21) + 5 // 5-25 bottles
    const glass = Math.floor(Math.random() * (totalBottles + 1))
    const metal = Math.floor(Math.random() * (totalBottles - glass + 1))
    const plastic = totalBottles - glass - metal

    set((draft: any) => {
      const visitor = {
        id: uid(),
        total: totalBottles,
        remaining: totalBottles,
        bottles: { glass, metal, plastic },
        waiting: false
      }
      draft.recyclers.find((r: any) => r.id === recyclerId)!.visitor = visitor
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor arrived at Recycler #${recyclerId} with ${totalBottles} bottles` })
    })
  },

  scheduleNextArrival: (recyclerId: number | string, minSec: number = 5, maxSec: number = 15) => {
    const state = get()
    const recycler = state.recyclers.find((r) => r.id === recyclerId)
    if (!recycler) return

    const existingTimer = scheduledArrivalTimers.get(recyclerId)
    if (existingTimer) {
      clearTimeout(existingTimer)
      scheduledArrivalTimers.delete(recyclerId)
    }

    const randomDelay = Math.floor(Math.random() * (maxSec - minSec + 1) + minSec) * 1000
    const timer = setTimeout(() => {
      get().createVisitorForRecycler(recyclerId)
      scheduledArrivalTimers.delete(recyclerId)
    }, randomDelay)

    scheduledArrivalTimers.set(recyclerId, timer)
  },

  init: async () => {
    set((draft: any) => {
      draft.totalEarnings = 0
      draft.recyclers = []
      draft.trucks = []
      draft.logs = []
      draft.chartPoints = []
      draft.lastTick = null
      draft.timeLevel = 2
      draft.buyingRecycler = false
      draft.buyingTruck = false
      draft.playerId = null
    })

    await get().fetchPlayer()
  },

  fetchPlayer: async () => {
    const state = get()
    if (state.playerId) return

    try {
      const env = (import.meta as any).env || {}
      const envBase = env?.VITE_API_BASE_URL
      const base = envBase || 'http://localhost:5001'

      // Initialize to create default player
      await fetch(`${base.replace(/\/$/, '')}/initialize`, {
        method: 'POST'
      })

      // Get all players
      const playersResponse = await fetch(`${base.replace(/\/$/, '')}/player`)
      if (!playersResponse.ok) {
        throw new Error('Failed to get players')
      }
      const players = await playersResponse.json()
      if (players.length === 0) {
        throw new Error('No players found')
      }
      const player = players[0]

      set((draft: any) => {
        draft.playerId = player.id
        draft.credits = player.credits
      })
    } catch (error) {
      set((draft: any) => {
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'error', message: 'Failed to initialize player data.' })
      })
    }
  }
})))

export default useGameStore