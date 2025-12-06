import React, { useEffect, useState } from 'react'
import useGameStore from '../store/useGameStore'

export default function DebugPanel() {
  const [open, setOpen] = useState(false)
  const [scheduled, setScheduled] = useState<Array<[number, number]>>([])
  const [lastTick, setLastTick] = useState<number | null>(null)
  const [recyclers, setRecyclers] = useState<any[]>([])

  useEffect(() => {
    const update = () => {
      try {
        const s = (useGameStore as any).getState()
        setLastTick(s?.lastTick ?? null)
        setRecyclers(s?.recyclers ?? [])
        const list = (window as any).listScheduledArrivals?.() ?? []
        setScheduled(list)
      } catch (e) {
        // ignore
      }
    }
    update()
    const id = window.setInterval(update, 1000)
    return () => window.clearInterval(id)
  }, [])

  const forceVisitor = (id?: number) => {
    try {
      const rid = id ?? (recyclers[0]?.id ?? 1)
      ;(window as any).forceVisitor?.(rid)
    } catch (e) {}
  }

  const scheduleVisitor = (id?: number) => {
    try {
      const rid = id ?? (recyclers[0]?.id ?? 1)
      ;(window as any).scheduleVisitor?.(rid, 1, 3)
    } catch (e) {}
  }

  return (
    <div className={`fixed right-4 bottom-4 z-50 ${open ? '' : 'h-8'} `}>
      <div className="card bg-base-200 shadow-xl w-72">
        <div className="card-body p-2">
          <div className="flex items-center justify-between">
            <h4 className="font-semibold text-sm">Dev Debug</h4>
            <div className="flex items-center gap-2">
              <button className="btn btn-xs btn-ghost" onClick={() => setOpen(!open)}>{open ? 'Hide' : 'Show'}</button>
            </div>
          </div>

          {open && (
            <div className="mt-2 text-xs space-y-2">
              <div>lastTick: <span className="font-mono">{lastTick ? new Date(lastTick).toLocaleTimeString() : '—'}</span></div>

              <div>
                <div className="font-semibold">Recyclers</div>
                <div className="max-h-32 overflow-auto mt-1">
                  {recyclers.map(r => (
                    <div key={r.id} className="flex justify-between items-center py-1">
                      <div className="truncate">#{r.id} • Bottles: {r.currentBottles.glass + r.currentBottles.metal + r.currentBottles.plastic}</div>
                      <div className="ml-2 text-right">
                        <div className="text-xs">visitor: {r.visitor ? 'yes' : 'no'}</div>
                        <div className="flex gap-1 mt-1">
                          <button className="btn btn-xs btn-primary" onClick={() => forceVisitor(r.id)}>Force</button>
                          <button className="btn btn-xs btn-secondary" onClick={() => scheduleVisitor(r.id)}>Sched</button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <div className="font-semibold">Scheduled timers</div>
                <div className="mt-1 text-xs">
                  {scheduled.length === 0 ? <div className="italic text-gray-400">none</div> : (
                    <ul className="list-disc list-inside">
                      {scheduled.map(([rid, tid]) => <li key={rid}>Recycler #{rid} • timerId={tid}</li>)}
                    </ul>
                  )}
                </div>
              </div>

              <div className="flex gap-2">
                <button className="btn btn-sm btn-success" onClick={() => (window as any).startAutoVisitors?.(2000)}>Start Auto</button>
                <button className="btn btn-sm btn-warning" onClick={() => (window as any).stopAutoVisitors?.()}>Stop Auto</button>
                <button className="btn btn-sm btn-outline" onClick={() => (window as any).clearArrivalWatchdog?.()}>Clear WD</button>
              </div>

            </div>
          )}
        </div>
      </div>
    </div>
  )
}