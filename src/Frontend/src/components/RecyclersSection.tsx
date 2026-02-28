import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'
import RecyclerCard from './RecyclerCard'

export default function RecyclersSection() {
  const recyclers = useGameStore((state: GameState) => state.recyclers)
  const buyRecycler = useGameStore((state: GameState) => state.buyRecycler)
  const buyingRecycler = useGameStore((state: GameState) => state.buyingRecycler)

  const atMaxLimit = recyclers.length >= 10

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">♻️ Recyclers</h2>
          <button className="btn btn-sm bg-blue-600 hover:bg-blue-700 text-white px-6 shadow-md no-outline-btn" onClick={buyRecycler} disabled={buyingRecycler || atMaxLimit}>+ Buy Recycler (500)</button>
        </div>

        <div className="mt-4 space-y-4">
          {recyclers.map((r: any) => <RecyclerCard key={r.id} recycler={r} />)}
        </div>
      </div>
    </div>
  )
}