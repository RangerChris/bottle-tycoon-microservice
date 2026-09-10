import React from 'react'
import useGameStore, { GameState } from '../store/useGameStore'
import useWorldStore from '../store/useWorldStore'
import RecyclerCard from './RecyclerCard'

export default function RecyclersSection() {
  const recyclers = useGameStore((state: GameState) => state.recyclers)
  const credits = useGameStore((state: GameState) => state.credits)
  const buildMode = useWorldStore((s) => s.buildMode)
  const setBuildMode = useWorldStore((s) => s.setBuildMode)

  const atMaxLimit = recyclers.length >= 10
  const placing = buildMode === 'recycler'

  return (
    <div className="card bg-base-200 shadow-xl">
      <div className="card-body">
        <div className="flex items-center justify-between">
          <h2 className="card-title text-emerald-500">♻️ Recyclers</h2>
          <button
            className={`btn btn-sm text-white px-6 shadow-md no-outline-btn ${placing ? 'btn-warning' : 'bg-blue-600 hover:bg-blue-700'}`}
            onClick={() => setBuildMode(placing ? 'none' : 'recycler')}
            disabled={atMaxLimit || credits < 500}
          >{placing ? '🎯 Click a tile next to a road…' : '+ Buy Recycler (500)'}</button>
        </div>

        <div className="mt-4 space-y-4">
          {recyclers.map((r: any) => <RecyclerCard key={r.id} recycler={r} />)}
        </div>
      </div>
    </div>
  )
}