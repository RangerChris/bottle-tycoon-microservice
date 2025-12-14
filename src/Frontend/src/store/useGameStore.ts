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
  // actions
  setTimeLevel: (level: number) => void
  addLog: (message: string, type?: LogEntry['type']) => void
  buyRecycler: () => void
  buyTruck: () => void
  deliverBottlesRandom: (recyclerId: number) => void
  upgradeRecycler: (recyclerId: number) => void
  upgradeTruck: (truckId: number) => void
  attemptSmartDispatch: () => void
  depositTick: (mult: number) => void
  createVisitorForRecycler: (recyclerId: number) => void
  scheduleNextArrival: (recyclerId: number, minSec?: number, maxSec?: number) => void
  // internal helpers for init
  init: () => void
}

// helper: calculate capacity based on level
function calculateCapacity(base: number, level: number) {
  return Math.floor(base * Math.pow(1.25, level))
}

// time multipliers mapping used by the frontend game loop
const timeMultipliers: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 }

// Track scheduled arrival timers to avoid duplicates per recycler
const scheduledArrivalTimers = new Map<number, number>()

// Watchdog interval id to ensure scheduling is active
let arrivalsWatchdog: number | null = null

const useGameStore = create(immer<GameState>((set, get) => ({
  credits: 1000,
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

  setTimeLevel: (level: number) => set((draft: any) => { draft.timeLevel = Math.max(1, Math.min(5, level)) }),

  addLog: (message: string, type: any = 'info') => set((draft: any) => {
    draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type, message })
    if (draft.logs.length > 50) draft.logs.pop()
  }),

  buyRecycler: () => {
    const state = get()
    if (state.buyingRecycler) return
    set((draft: any) => { draft.buyingRecycler = true })
    const cost = 500
    setTimeout(() => {
      const s = get()
      if (s.recyclers.length >= 10) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Cannot purchase more recyclers.' }) }); return }
      if (s.credits < cost) { set((draft: any) => { draft.buyingRecycler = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits to buy recycler!' }) }); return }
      set((draft: any) => {
        draft.credits -= cost
        const newId = draft.recyclers.reduce((m: number, r: any) => Math.max(m, r.id), 0) + 1
        draft.recyclers.push({ id: newId, level: 0, capacity: 100, currentBottles: { glass: 0, metal: 0, plastic: 0 }, visitor: null })
        draft.buyingRecycler = false
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased Recycler #${newId}` })
      })
      // schedule initial visitor for new recycler
      get().scheduleNextArrival(get().recyclers.slice(-1)[0].id, 1, 8)
    }, 120)
  },

  buyTruck: () => {
    const state = get()
    if (state.buyingTruck) return
    set((draft: any) => { draft.buyingTruck = true })
    const cost = 800
    setTimeout(() => {
      const s = get()
      if (s.trucks.length >= 10) { set((draft: any) => { draft.buyingTruck = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Cannot purchase more trucks.' }) }); return }
      if (s.credits < cost) { set((draft: any) => { draft.buyingTruck = false; draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits to buy truck!' }) }); return }
      set((draft: any) => {
        draft.credits -= cost
        const newId = draft.trucks.reduce((m: number, t: any) => Math.max(m, t.id), 0) + 1
        draft.trucks.push({ id: newId, level: 0, capacity: 45, currentLoad: 0, status: 'idle', targetRecyclerId: null, cargo: null })
        draft.buyingTruck = false
        draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Purchased Truck #${newId}` })
      })
    }, 120)
  },

  deliverBottlesRandom: (recyclerId: number) => {
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

  upgradeRecycler: (recyclerId: number) => set((draft: any) => {
    const r = draft.recyclers.find((x: any) => x.id === recyclerId)
    if (!r) return
    if (r.level >= 3) { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Recycler already at max level!' }); return }
    const cost = 200 * (r.level + 1)
    if (draft.credits < cost) { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }); return }
    draft.credits -= cost
    r.level++
    draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Recycler #${recyclerId} upgraded to Level ${r.level}` })
  }),

  upgradeTruck: (truckId: number) => set((draft: any) => {
    const t = draft.trucks.find((x: any) => x.id === truckId)
    if (!t) return
    if (t.level >= 3) { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Truck already at max level!' }); return }
    const cost = 300 * (t.level + 1)
    if (draft.credits < cost) { draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: 'Not enough credits for upgrade!' }); return }
    draft.credits -= cost
    t.level++
    draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Truck #${truckId} upgraded to Level ${t.level}` })
  }),

  // depositTick: perform per-second deposit using multiplier from time control
  depositTick: (mult: number) => {
    if (mult <= 0) return
    // debug logging removed
    set((draft: any) => {
      let updated = false
      for (const recycler of draft.recyclers) {
        const v = recycler.visitor
        for (let i = 0; i < mult; i++) {
          if (v && v.remaining > 0) {
            const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
            const capacity = calculateCapacity(recycler.capacity, recycler.level)
            if (totalBottles >= capacity) {
              if (!v.waiting) {
                v.waiting = true
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'warning', message: `Visitor at Recycler #${recycler.id} waiting - recycler is full (${totalBottles}/${capacity})` })
              }
              break
            }
            if (v.waiting) {
              v.waiting = false
              draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor at Recycler #${recycler.id} can now deposit - space available` })
            }
            const types = ['glass', 'metal', 'plastic'].filter((t: any) => v.bottles[t] > 0)
            if (types.length === 0) { v.remaining = 0; break }
            const t = types[Math.floor(Math.random() * types.length)]
            v.bottles[t]--
            v.remaining--
            recycler.currentBottles[t] = (recycler.currentBottles[t] || 0) + 1
            updated = true
          } else {
            break
          }
        }
        if (v && v.remaining <= 0) {
          draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor finished at Recycler #${recycler.id}` })
          recycler.visitor = null
          // scheduleNextArrival will be called outside of the immer set to avoid nested setTimeout in mutation
        }
      }
      // always update lastTick so UI reflects the tick regardless of whether bottles changed
      try { draft.lastTick = Date.now() } catch {}
    })

    // After state changes, schedule next arrivals for any recycler without visitor
    const s = get()
    s.recyclers.forEach((r) => {
      if (!r.visitor) {
        // only schedule if there's no existing pending arrival timer for this recycler
        if (!scheduledArrivalTimers.has(r.id)) {
          get().scheduleNextArrival(r.id, 3, 8)
        }
      }
    })

    // Trigger smart dispatch if any changes occurred
    get().attemptSmartDispatch()
  },

  createVisitorForRecycler: (recyclerId: number) => {
    // clear any scheduled arrival timer for this recycler
    const existing = scheduledArrivalTimers.get(recyclerId)
    if (existing) {
      window.clearTimeout(existing)
      scheduledArrivalTimers.delete(recyclerId)
    }

    const recycler = get().recyclers.find(r => r.id === recyclerId)
    if (!recycler) return
    const total = Math.floor(Math.random() * 21) + 5
    const glass = Math.floor(Math.random() * (total + 1))
    const metal = Math.floor(Math.random() * (total - glass + 1))
    const plastic = total - glass - metal
    // debug logging removed
    set((draft: any) => {
      const rr = draft.recyclers.find((x: any) => x.id === recyclerId)
      if (!rr) return
      rr.visitor = { id: uid(), total, remaining: total, bottles: { glass, metal, plastic } }
      draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Visitor arrived at Recycler #${recyclerId} with ${total} bottles (G:${glass}, M:${metal}, P:${plastic})` })
    })
  },

  scheduleNextArrival: (recyclerId: number, minSec = 3, maxSec = 8) => {
    // clear any existing scheduled timer so we always (re)schedule a fresh arrival
    const existing = scheduledArrivalTimers.get(recyclerId)
    if (existing) {
      try { window.clearTimeout(existing) } catch {}
      scheduledArrivalTimers.delete(recyclerId)
    }

    const baseDelay = (Math.floor(Math.random() * (maxSec - minSec + 1)) + minSec) * 1000
    // debug logging removed

    // factor current time multiplier to make arrivals faster on increased speed
    const mult = timeMultipliers[get().timeLevel] ?? 1
    if (mult === 0) {
      // paused: retry later (shorter retry so visitors resume promptly when unpaused)
      const t = window.setTimeout(() => {
        scheduledArrivalTimers.delete(recyclerId)
        get().scheduleNextArrival(recyclerId, 1, 3)
      }, 2000)
      scheduledArrivalTimers.set(recyclerId, t)
      return
    }

    const effectiveDelay = Math.max(200, Math.floor(baseDelay / mult))
    // debug logging removed
    const timerId = window.setTimeout(() => {
      scheduledArrivalTimers.delete(recyclerId)
      // debug logging removed
      const recycler = get().recyclers.find(r => r.id === recyclerId)
      if (!recycler) return
      if (recycler.visitor) {
        // already a visitor, try again later (shorter retry)
        get().scheduleNextArrival(recyclerId, 3, 8)
        return
      }
      get().createVisitorForRecycler(recyclerId)
    }, effectiveDelay)

    scheduledArrivalTimers.set(recyclerId, timerId)
  },

  attemptSmartDispatch: () => {
    const s = get()
    const idleTrucks = s.trucks.filter((t: any) => t.status === 'idle')
    if (idleTrucks.length === 0) return
    const recyclerNeeds = s.recyclers.map((r: any) => {
      const capacity = calculateCapacity(r.capacity, r.level)
      const totalBottles = r.currentBottles.glass + r.currentBottles.metal + r.currentBottles.plastic
      const threshold80 = Math.floor(capacity * 0.8)
      return { id: r.id, recycler: r, totalBottles, capacity, threshold80 }
    }).filter((x: any) => x.totalBottles > 0)
    for (const truck of idleTrucks) {
      const suitable = recyclerNeeds.filter((r: any) => r.totalBottles >= r.threshold80)
      if (suitable.length > 0) {
        // pick recycler with the most bottles
        const target = suitable.reduce((max: any, r: any) => r.totalBottles > max.totalBottles ? r : max)
         // perform dispatch asynchronously to simulate travel
        set((draft: any) => {
          const tt = draft.trucks.find((x: any) => x.id === truck.id)
          if (!tt) return
          tt.status = 'to_recycler'
          tt.targetRecyclerId = target.recycler.id
          draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Truck #${tt.id} dispatched to Recycler #${target.recycler.id} (${target.totalBottles} bottles available)` })
        })
        // simulate arrival after travel (uses scheduleWithTime)
        scheduleWithTime(() => {
          // loading
          set((draft: any) => {
            const tt = draft.trucks.find((x: any) => x.id === truck.id)
            if (!tt) return
            if (tt.status !== 'to_recycler') return
            tt.status = 'loading'
            draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Truck #${tt.id} arrived at Recycler #${tt.targetRecyclerId}` })
          })

          scheduleWithTime(() => {
            // pick up
            set((draft: any) => {
              const tt = draft.trucks.find((x: any) => x.id === truck.id)
              if (!tt) return
              const recycler = draft.recyclers.find((r: any) => r.id === tt.targetRecyclerId)
              if (!recycler) { tt.status = 'idle'; return }
              const truckCapacity = Math.floor(tt.capacity * Math.pow(1.25, tt.level))
              const available = { ...recycler.currentBottles }
              const picked = { glass: 0, metal: 0, plastic: 0 }
              let pickedCount = 0
              while (pickedCount < truckCapacity) {
                if (available.glass > 0) { picked.glass++; available.glass--; pickedCount++ }
                else if (available.metal > 0) { picked.metal++; available.metal--; pickedCount++ }
                else if (available.plastic > 0) { picked.plastic++; available.plastic--; pickedCount++ }
                else break
              }
              const pickedTotal = picked.glass + picked.metal + picked.plastic
              const remainingTotal = available.glass + available.metal + available.plastic
              draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Truck #${tt.id} picked up ${pickedTotal} bottles (G:${picked.glass}, M:${picked.metal}, P:${picked.plastic}) from Recycler #${recycler.id}` })
              if (remainingTotal > 0) draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Recycler #${recycler.id} still has ${remainingTotal} bottles remaining` })
              recycler.currentBottles = available
              tt.cargo = picked
              tt.status = 'to_plant'
              tt.targetRecyclerId = null
            })
            // ensure we schedule the next visitor for that recycler so visitors keep arriving (shorter interval)
            try { get().scheduleNextArrival(target.recycler.id, 3, 8) } catch {}
            // also schedule a quick check to create a visitor shortly after pickup if nothing is pending
            try {
              const rid = target.recycler.id
              if (!scheduledArrivalTimers.has(rid)) {
                const quick = window.setTimeout(() => {
                  scheduledArrivalTimers.delete(rid)
                  const rr = get().recyclers.find((x: any) => x.id === rid)
                  if (rr && !rr.visitor) get().createVisitorForRecycler(rid)
                }, 500)
                scheduledArrivalTimers.set(rid, quick)
              }
            } catch (e) {
              // ignore
            }

            // travel to plant
            scheduleWithTime(() => {
              set((draft: any) => {
                const tt = draft.trucks.find((x: any) => x.id === truck.id)
                if (!tt) return
                if (!tt.cargo) return
                tt.status = 'delivering'
                const deliverTotal = tt.cargo.glass + tt.cargo.metal + tt.cargo.plastic
                draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'info', message: `Truck #${tt.id} arrived at Recycling Plant with ${deliverTotal} bottles (G:${tt.cargo.glass}, M:${tt.cargo.metal}, P:${tt.cargo.plastic})` })
              })

              // deliver
              scheduleWithTime(() => {
                set((draft: any) => {
                  const tt = draft.trucks.find((x: any) => x.id === truck.id)
                  if (!tt || !tt.cargo) return
                  const value = (tt.cargo.glass * 4) + (tt.cargo.metal * 2.5) + (tt.cargo.plastic * 1.75)
                  draft.credits += Math.floor(value)
                  draft.totalEarnings += Math.floor(value)
                  draft.chartPoints.push({ time: Date.now(), bottles: tt.cargo })
                  if (draft.chartPoints.length > 10) draft.chartPoints.shift()
                  draft.logs.unshift({ id: uid(), time: new Date().toLocaleTimeString(), type: 'success', message: `Truck #${tt.id} delivered ${tt.cargo.glass + tt.cargo.metal + tt.cargo.plastic} bottles earning ${Math.floor(value)} credits` })
                  tt.cargo = null
                  tt.currentLoad = 0
                  tt.status = 'idle'
                })
                // trigger next dispatch
                get().attemptSmartDispatch()
              }, 2000)
            }, 4500)
          }, 1500)
        }, 3500)
      }
    }
  },

  init: () => {
    // Call initialize endpoint to reset game state
    const controller = new AbortController()
    const initializeGame = async () => {
      try {
        const env = (import.meta as any).env || {}
        const envBase = env?.VITE_API_BASE_URL
        const defaultBase = (typeof window !== 'undefined' && window.location.hostname === 'localhost')
          ? 'http://localhost:5001'
          : 'http://apigateway:5000'
        const base = envBase || defaultBase
        const url = `${base.replace(/\/$/, '')}/initialize`
        await fetch(url, { method: 'POST', signal: controller.signal })
      } catch (e) {
        // Ignore errors, perhaps log later
      }
    }
    initializeGame()

    // schedule initial visitor arrivals for all existing recyclers
    setTimeout(() => {
      const s = get()
      s.recyclers.forEach(r => {
        // create an immediate visitor for a lively demo, then schedule next arrivals
        if (!r.visitor) get().createVisitorForRecycler(r.id)
        get().scheduleNextArrival(r.id, 1, 8)
      })

      // start a watchdog that ensures any recycler without a visitor has a scheduled arrival
      try {
        if (arrivalsWatchdog == null) {
          arrivalsWatchdog = window.setInterval(() => {
            try {
              const state = get()
              state.recyclers.forEach(r => {
                if (!r.visitor && !scheduledArrivalTimers.has(r.id)) {
                  // schedule with short bounds to keep the game lively
                  get().scheduleNextArrival(r.id, 3, 8)
                }
              })
            } catch (e) {
              // ignore
            }
          }, 2000)
        }
      } catch (e) {
        // ignore in non-browser env
      }
    }, 200)
  }
})))

export default useGameStore

// Dev helpers (attach to window for easy debugging in browser console)
if (typeof window !== 'undefined') {
  ;(window as any).forceVisitor = (recyclerId: number) => {
    try { (useGameStore as any).getState().createVisitorForRecycler(recyclerId) } catch (e) { }
  }
  ;(window as any).scheduleVisitor = (recyclerId: number, minSec = 1, maxSec = 5) => {
    try { (useGameStore as any).getState().scheduleNextArrival(recyclerId, minSec, maxSec) } catch (e) { }
  }
  ;(window as any).listScheduledArrivals = () => {
    try { return Array.from(scheduledArrivalTimers.entries()) } catch (e) { return [] }
  }
  ;(window as any).clearArrivalWatchdog = () => {
    try { if (arrivalsWatchdog != null) { clearInterval(arrivalsWatchdog); arrivalsWatchdog = null } } catch (e) { }
  }
  ;(window as any).startAutoVisitors = (periodMs = 3000) => {
    try {
      if ((window as any)._autoVisitorsInterval) return
      (window as any)._autoVisitorsInterval = window.setInterval(() => {
        try {
          const s = (useGameStore as any).getState()
          s.recyclers.forEach((r: any) => { if (!r.visitor) (useGameStore as any).getState().createVisitorForRecycler(r.id) })
        } catch (e) { }
      }, periodMs)
    } catch (e) { }
  }
  ;(window as any).stopAutoVisitors = () => {
    try { if ((window as any)._autoVisitorsInterval) { clearInterval((window as any)._autoVisitorsInterval); (window as any)._autoVisitorsInterval = null } } catch (e) { }
  }
}

// helper to schedule timeouts that respect current time speed (multiplier). When paused, retries later.
function scheduleWithTime(fn: () => void, baseMs: number) {
  try {
    const stateTimeLevel = (useGameStore as any).getState().timeLevel as number | undefined
    const stateMult = timeMultipliers[stateTimeLevel ?? 2] ?? 1
    if (stateMult === 0) {
      return window.setTimeout(() => scheduleWithTime(fn, baseMs), 1000)
    }
    const effective = Math.max(150, Math.floor(baseMs / stateMult))
    return window.setTimeout(fn, effective)
  } catch (e) {
    const effective = Math.max(150, Math.floor(baseMs))
    return window.setTimeout(fn, effective)
  }
}