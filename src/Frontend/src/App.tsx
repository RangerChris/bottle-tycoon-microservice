import { useEffect, useState } from 'react'
import HealthCheck from './components/HealthCheck'

export default function App() {
  return (
    <div className="min-h-screen bg-base-200 text-base-content">
      <div className="navbar bg-base-100 shadow">
        <div className="container mx-auto">
          <a className="btn btn-ghost text-xl">Bottle Tycoon</a>
        </div>
      </div>
      <div className="container mx-auto p-6">
        <div className="card bg-base-100 shadow">
          <div className="card-body">
            <HealthCheck />
          </div>
        </div>
      </div>
    </div>
  )
}