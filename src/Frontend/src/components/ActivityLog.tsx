import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'

export default function ActivityLog({ embedded = false }: { embedded?: boolean }) {
  const logs = useGameStore((s: GameState) => s.logs)

  const logList = (
    <div className="log-container">
      {logs.map((l: any) => (
        <div key={l.id} className={`log-entry ${l.type === 'info' ? 'log-info' : l.type === 'success' ? 'log-success' : l.type === 'warning' ? 'log-warning' : 'log-danger'}`}>
          <span className="log-time">{l.time}</span>
          <span className="log-message ml-2">{l.message}</span>
        </div>
      ))}
    </div>
  )

  if (embedded) return logList

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <h3 className="card-title text-emerald-500">📜 Activity Log</h3>
        <div className="mt-4 max-h-[40rem] overflow-y-auto">
          {logList}
        </div>
      </div>
    </div>
  )
}