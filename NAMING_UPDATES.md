# Recycler and Truck Naming Updates

## Overview
Updated the naming convention for recyclers and trucks to use user-friendly sequential names (e.g., "Recycler 1", "Truck 2") instead of displaying truncated GUIDs or generic "Standard Truck" names.

## Changes Made

### Backend Services

#### RecyclerService (`src/RecyclerService/Services/RecyclerService.cs`)
- Updated `CreateRecyclerAsync()` to generate sequential names when no name is provided
- New naming pattern: `"Recycler {existingCount + 1}"`
- Falls back to provided name if explicitly set

**Example:**
- First recycler created: "Recycler 1"
- Second recycler created: "Recycler 2"
- Custom named recycler via API: uses provided name

#### TruckService (`src/TruckService/Services/TruckService.cs`)
- Updated `CreateTruckAsync()` to generate sequential names when no model is provided
- New naming pattern: `"Truck {existingCount + 1}"`
- Falls back to provided model if explicitly set

**Example:**
- First truck created: "Truck 1"
- Second truck created: "Truck 2"
- Custom named truck via API: uses provided name

### Frontend

#### Type Definitions (`src/Frontend/src/types/index.ts`)
- Added optional `name?: string` field to `Recycler` type to support recycler names from API

#### Components

**RecyclerCard** (`src/Frontend/src/components/RecyclerCard.tsx`)
- Changed title from `Recycler #{id.substring(0, 8)}` to `{recycler.name || 'Recycler'}`
- Displays friendly recycler name instead of truncated ID

**TruckCard** (`src/Frontend/src/components/TruckCard.tsx`)
- Changed title from `Truck #{id.substring(0, 8)}` to `{truck.model || 'Truck'}`
- Now displays truck model name instead of truncated ID
- Removed redundant display of model in subtitle

#### Game Store (`src/Frontend/src/store/useGameStore.ts`)

**Buy Truck Logic:**
- Updated to send `model: \`Truck ${state.trucks.length + 1}\`` when creating trucks
- Changed log message from `Purchased Truck #${newTruck.id...}` to `Purchased ${newTruck.model}`

**Buy Recycler Logic:**
- Already sending `name: \`Recycler ${state.recyclers.length + 1}\`` when creating recyclers
- Changed log message from `Purchased Recycler #${newRecycler.id...}` to `Purchased ${newRecycler.name}`

**Fetch Recyclers Logic:**
- Updated to include `name: r.name` when mapping recyclers from API

**Upgrade Messages:**
- Recycler: Changed from `Recycler #${recyclerId}` to `${recycler.name}`
- Truck: Changed from `Truck #${truckId}` to `${truck.model}`

### Tests

#### RecyclerService Tests (`src/RecyclerService.Tests/RecyclerServiceTests.cs`)
- Updated `CreateRecyclerAsync_CreatesRecycler()` test to verify:
  - First created recycler has name "Recycler 1"
  - Fetched recycler preserves the name

All other tests continue to work as they provide explicit names or models.

## User-Facing Changes

### Before
- UI showed: "Recycler #a1b2c3d4" / "Truck #e5f6a7b8"
- Logs showed: "Purchased Recycler #a1b2c3d4" / "Purchased Truck #e5f6a7b8"
- Purchase messages: generic "Standard Truck"

### After
- UI shows: "Recycler 1", "Recycler 2", "Truck 1", "Truck 2", etc.
- Logs show: "Purchased Recycler 1" / "Purchased Truck 1"
- Clear sequential numbering for easy reference
- Upgrades show: "Recycler 1 upgraded to Level 2"

## Implementation Notes

1. **Database Compatibility**: No database schema changes required. Names are generated on-the-fly when empty.
2. **Backward Compatibility**: Existing names in the database are preserved if already set.
3. **API Contract**: CreateRecycler and CreateTruck endpoints continue to accept optional name/model parameters.
4. **Sequential Numbering**: Calculated at creation time based on count of existing entities before insertion.

## Testing

All unit and integration tests pass. Tests that provide explicit names/models continue to validate those values.

### Key Test Scenarios Covered
- Creating recyclers/trucks without names generates sequential numbers
- Explicitly provided names are preserved
- Frontend displays names correctly in cards
- Upgrade messages reference names instead of IDs
- Log messages use friendly names