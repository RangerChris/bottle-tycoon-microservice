import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'

export default function ActivityLog() {
  const logs = useGameStore((s: GameState) => s.logs)

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <h3 className="card-title text-emerald-500">📜 Activity Log</h3>
        <div className="mt-4">
          <div className="log-container">
            {logs.map((l: any) => (
              <div key={l.id} className={`log-entry ${l.type === 'info' ? 'log-info' : l.type === 'success' ? 'log-success' : l.type === 'warning' ? 'log-warning' : 'log-danger'}`}>
                <span className="log-time">{l.time}</span>
                <span className="log-message ml-2">{l.message}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}