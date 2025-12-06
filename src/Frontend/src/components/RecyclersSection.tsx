import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'
import RecyclerCard from './RecyclerCard'

export default function RecyclersSection() {
  const recyclers = useGameStore((state: GameState) => state.recyclers)
  const total = recyclers.length
  const buyRecycler = useGameStore((state: GameState) => state.buyRecycler)
  const buyingRecycler = useGameStore((state: GameState) => state.buyingRecycler)

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">♻️ Recyclers</h2>
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-400">Total</span>
            <span className="font-bold text-gray-100">{total}</span>
            <button className="btn btn-secondary btn-sm ml-4 shadow-md no-outline-btn" onClick={buyRecycler} disabled={buyingRecycler}>+ Buy Recycler (500)</button>
          </div>
        </div>

        <div className="mt-4 space-y-4">
          {recyclers.map((r: any) => <RecyclerCard key={r.id} recycler={r} />)}
        </div>
      </div>
    </div>
  )
}