# Bottle Tycoon - HTML Mockup

This is a simple HTML/CSS/JavaScript mockup of the Bottle Tycoon frontend, created before implementing the full React application.

## Files

- **index.html** - Main game interface with all UI components
- **styles.css** - Complete styling with dark theme and responsive design
- **app.js** - Interactive functionality and game logic simulation

## How to Use

1. Open `index.html` in any modern web browser
2. No build process or dependencies required!

## Features Demonstrated

### Game Dashboard
- **Credits Display** - Shows current player credits
- **Game Statistics** - Total recyclers, trucks, and earnings

### Recyclers Section
- Individual recycler cards with:
  - Level indicators (0-3)
  - Capacity bars showing fill percentage
  - Bottle breakdown by type (Glass, Metal, Plastic)
  - Deliver and upgrade buttons
- Buy new recyclers button

### Trucks Section
- Individual truck cards with:
  - Level indicators (0-3)
  - Status badges (Idle/Picking/Delivering)
  - Capacity bars for load tracking
  - Upgrade buttons
- Buy new trucks button

### Earnings Chart
- Placeholder for time-series chart (will use Recharts in React)
- Mock bar chart visualization

### Activity Log
- Real-time event logging
- Color-coded by event type (info/success/warning)
- Auto-scrolling with timestamp

### Info Panel (Sidebar)
- Bottle values reference table
- How to play instructions
- Upgrade system explanation

## Interactive Features

### Try These Actions:
1. **Click "Deliver Bottles"** on Recycler #1
   - Random bottles added to recycler
   - Activity log shows delivery
   - When recycler reaches 90%, truck auto-dispatches
   
2. **Click "Upgrade"** buttons
   - Upgrades cost credits (increases per level)
   - Capacity increases by 25% per level
   - Max 3 levels

3. **Click "Buy Recycler"** or "Buy Truck"**
   - Costs 500/800 credits respectively
   - New card appears in grid
   - Counter updates

4. **Watch the Activity Log**
   - Shows all game events with timestamps
   - Color-coded entries

## Design Highlights

### Color Scheme
- Primary Green: #10b981 (eco-friendly theme)
- Dark Background: #1f2937 gradient
- Status Colors: Success/Warning/Info variations

### Responsive Design
- Desktop: Two-column layout (main + sidebar)
- Tablet: Single column, sidebar below
- Mobile: Stacked cards, full-width buttons

### UI Components Match Tech Stack
- Layout ready for Tailwind CSS conversion
- Component structure mirrors DaisyUI patterns
- Grid layouts for recyclers/trucks
- Card-based design system

## Converting to React

When building the React version:

1. **Component Breakdown:**
   - `Header` - Credits display
   - `GameStats` - Statistics cards
   - `RecyclerCard` - Individual recycler
   - `RecyclerGrid` - Collection of recyclers
   - `TruckCard` - Individual truck
   - `TruckGrid` - Collection of trucks
   - `EarningsChart` - Recharts integration
   - `ActivityLog` - Event stream
   - `InfoPanel` - Sidebar content

2. **State Management (Zustand):**
   - Player credits
   - Recycler array
   - Truck array
   - Activity log entries
   - Total earnings

3. **API Integration (TanStack Query):**
   - Fetch player data
   - Deliver bottles endpoint
   - Upgrade endpoints
   - Purchase endpoints
   - Real-time updates via Socket.io

4. **Styling:**
   - Replace CSS with Tailwind classes
   - Use DaisyUI components (cards, buttons, badges)
   - Keep color scheme consistent

## Notes

- All game logic is simulated in `app.js`
- No actual API calls (placeholder for microservices)
- Capacity calculations match game design specs
- Upgrade cost formulas: Recycler = 200 × level, Truck = 300 × level
- Auto-dispatch triggers at 90% capacity

## Next Steps

1. Set up React 19 project with Vite
2. Install Tailwind CSS + DaisyUI
3. Install TanStack Query + Zustand
4. Convert HTML structure to JSX components
5. Connect to actual microservices APIs
6. Add Socket.io for real-time events
7. Integrate Recharts for earnings visualization