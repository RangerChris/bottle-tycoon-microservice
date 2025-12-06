import React from 'react'

export default function HelpModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  if (!open) return null

  return (
    <div className="modal-overlay">
      <div role="dialog" aria-modal="true" className="modal-box max-w-3xl relative bg-gray-800 text-gray-100 p-6 rounded-lg border border-gray-700 shadow-xl mx-4 my-8">
        <button className="btn btn-sm btn-circle absolute right-4 top-4" onClick={onClose}>✕</button>
        <h3 className="card-title text-emerald-500 text-2xl mb-4">Game Info</h3>
        <div className="text-sm text-gray-200 space-y-5">
          <div>
            <h4 className="font-semibold mb-2">Bottle Values</h4>
            <table className="table table-compact w-full text-sm text-gray-200">
              <thead>
                <tr>
                  <th className="text-left">Type</th>
                  <th className="text-left">Value</th>
                </tr>
              </thead>
              <tbody>
                <tr><td>🟢 Glass</td><td>4 credits</td></tr>
                <tr><td>⚪ Metal</td><td>2.5 credits</td></tr>
                <tr><td>🔵 Plastic</td><td>1.75 credits</td></tr>
              </tbody>
            </table>
          </div>

          <div>
            <h4 className="font-semibold mb-2">How to Play</h4>
            <ol className="list-decimal ml-6 text-gray-300 space-y-1">
              <li>Visitors bring bottles to recyclers automatically</li>
              <li>Trucks dispatch when recyclers have enough bottles</li>
              <li>Trucks pick up bottles and deliver to plant</li>
              <li>Earn credits from deliveries</li>
              <li>Upgrade or buy new equipment</li>
            </ol>
          </div>

          <div>
            <h4 className="font-semibold mb-2">Upgrade System</h4>
            <p className="text-gray-300 leading-relaxed">Each upgrade increases capacity by <strong>+25%</strong>. Max upgrades: <strong>3 levels</strong></p>
          </div>
        </div>

        <div className="modal-action mt-6">
          <button className="btn btn-sm btn-primary" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  )
}