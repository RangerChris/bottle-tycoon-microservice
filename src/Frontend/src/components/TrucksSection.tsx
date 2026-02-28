import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'
import TruckCard from './TruckCard'

export default function TrucksSection() {
  const trucks = useGameStore((s: GameState) => s.trucks)
  const buyTruck = useGameStore((s: GameState) => s.buyTruck)
  const buyingTruck = useGameStore((s: GameState) => s.buyingTruck)

  const atMaxLimit = trucks.length >= 10

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">🚚 Trucks</h2>
          <button className="btn btn-sm bg-blue-600 hover:bg-blue-700 text-white px-6 shadow-md no-outline-btn" onClick={buyTruck} disabled={buyingTruck || atMaxLimit}>+ Buy Truck (800)</button>
        </div>

        <div className="mt-4 space-y-4">
          {trucks.map((t: any) => <TruckCard key={t.id} truck={t} />)}
        </div>
      </div>
    </div>
  )
}