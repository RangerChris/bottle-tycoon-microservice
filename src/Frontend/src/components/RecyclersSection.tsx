import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'
import RecyclerCard from './RecyclerCard'

export default function RecyclersSection() {
  const recyclers = useGameStore((state: GameState) => state.recyclers)
  const buyRecycler = useGameStore((state: GameState) => state.buyRecycler)
  const buyingRecycler = useGameStore((state: GameState) => state.buyingRecycler)

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">♻️ Recyclers</h2>
          <button className="btn btn-secondary btn-sm px-6 shadow-md no-outline-btn" onClick={buyRecycler} disabled={buyingRecycler}>+ Buy Recycler (500)</button>
        </div>

        <div className="mt-4 space-y-4">
          {recyclers.map((r: any) => <RecyclerCard key={r.id} recycler={r} />)}
        </div>
      </div>
    </div>
  )
}