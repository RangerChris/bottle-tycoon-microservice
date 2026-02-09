import React from 'react'
import { Recycler } from '../types'
import useGameStore, { GameState } from '../store/useGameStore'

export default function RecyclerCard({ recycler }: { recycler: Recycler }) {
  const upgradeRecycler = useGameStore((s: GameState) => s.upgradeRecycler)
  const deliverBottlesRandom = useGameStore((s: GameState) => s.deliverBottlesRandom)

  const totalBottles = recycler.currentBottles.glass + recycler.currentBottles.metal + recycler.currentBottles.plastic
  const capacity = Math.floor(recycler.capacity * Math.pow(1.25, recycler.level))
  const percentage = Math.min((totalBottles / capacity) * 100, 100)

  return (
    <div className="bg-gray-800 p-4 rounded-lg border border-gray-700">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="text-lg font-semibold">{recycler.name || 'Recycler'}</h3>
          <div className="text-sm text-gray-400">Level {recycler.level}</div>
        </div>
        <div className="text-right">
          <div className="text-sm text-gray-400">{Math.floor(percentage)}%</div>
          <div className="text-xs text-gray-300">{totalBottles} / {capacity} bottles</div>
        </div>
      </div>

      <div className="mt-3">
        <div className="capacity-bar">
          <div className="capacity-fill" style={{ width: `${percentage}%` }} />
          <div className="capacity-text">{Math.floor(percentage)}%</div>
        </div>
      </div>

      <div className="mt-3 text-sm text-gray-300 space-y-1">
        <div>🟢 Glass: {recycler.currentBottles.glass}</div>
        <div>⚪ Metal: {recycler.currentBottles.metal}</div>
        <div>🔵 Plastic: {recycler.currentBottles.plastic}</div>
      </div>

      <div className="mt-4 flex gap-2">
        <button className="btn btn-sm bg-green-600 hover:bg-green-700 text-white flex-1" onClick={() => deliverBottlesRandom(recycler.id)}>📦 Add bottles</button>
        <button className="btn bg-amber-600 hover:bg-amber-700 text-white min-w-40" onClick={() => upgradeRecycler(recycler.id)} disabled={recycler.level >= 3}>⬆️ Upgrade ({200 * (recycler.level + 1)})</button>
      </div>
    </div>
  )
}