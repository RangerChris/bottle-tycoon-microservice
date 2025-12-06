import React from 'react'
import { Truck } from '../types'
import useGameStore, { GameState } from '../store/useGameStore'

export default function TruckCard({ truck }: { truck: Truck }) {
  const upgradeTruck = useGameStore((s: GameState) => s.upgradeTruck)

  const capacity = Math.floor(truck.capacity * Math.pow(1.25, truck.level))
  const loadBottles = truck.cargo ? (truck.cargo.glass + truck.cargo.metal + truck.cargo.plastic) : truck.currentLoad
  const percentage = Math.min((loadBottles / capacity) * 100, 100)

  let statusText = 'Idle'
  let statusClass = 'badge badge-outline'
  if (truck.status === 'to_recycler') { statusText = 'To Recycler'; statusClass = 'badge badge-info' }
  else if (truck.status === 'loading') { statusText = 'Loading'; statusClass = 'badge badge-warning' }
  else if (truck.status === 'to_plant') { statusText = 'To Plant'; statusClass = 'badge badge-info' }
  else if (truck.status === 'delivering') { statusText = 'Delivering'; statusClass = 'badge badge-success' }

  return (
    <div className="bg-gray-800 p-4 rounded-lg border border-gray-700">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="text-lg font-semibold">Truck #{truck.id}</h3>
          <div className="text-sm text-gray-400">Level {truck.level}</div>
        </div>
        <div className="text-right">
          <span className={statusClass}>{statusText}</span>
          <div className="text-xs text-gray-300 mt-1">{loadBottles} / {capacity} bottles</div>
        </div>
      </div>

      <div className="mt-3">
        <div className="capacity-bar">
          <div className="capacity-fill" style={{ width: `${percentage}%` }} />
          <div className="capacity-text">{Math.floor(percentage)}%</div>
        </div>
      </div>

      <div className="mt-4 flex gap-2">
        <button className="btn btn-sm btn-warning flex-1" onClick={() => upgradeTruck(truck.id)} disabled={truck.level >= 3}>⬆️ Upgrade ({300 * (truck.level + 1)})</button>
      </div>
    </div>
  )
}