# Scoring Logic Update

## Problem
The application needed a better scoring system where:
1. Holes being actively scored should default to par (for easy +/- adjustment)
2. Unscored holes should not count toward the running total
3. The database should track which holes have been scored

## Solution

### 1. Added `IsScored` Field to Hole Model
**File: `iDoublePress/Models/Hole.cs`**
- Added `public bool IsScored { get; set; }` property to track if a hole has been scored

### 2. Updated Database Schema
**File: `iDoublePress/Data/RoundRepository.cs`**
- Added `IsScored INTEGER DEFAULT 0` column to the Hole table
- Updated all CRUD operations to include the `IsScored` field

### 3. Modified Score Calculation Logic
**Files: `iDoublePress/Data/RoundRepository.cs`, `iDoublePress/Models/Round.cs`**
- `SaveItemAsync()`: Changed to `item.TotalScore = item.Holes.Where(h => h.IsScored).Sum(h => h.Score);`
- `Round.ScoreRelativeToPar`: Updated to only calculate based on scored holes

### 4. Updated UI Score Handling
**File: `iDoublePress/PageModels/ActiveRoundPageModel.cs`**
- `IncreaseScore()`: Sets `CurrentHole.IsScored = true` when user increases score
- `DecreaseScore()`: Sets `CurrentHole.IsScored = true` when user decreases score
- `SaveCurrentHole()`: Updated calculation to use only scored holes

### 5. Default Hole Initialization
**File: `iDoublePress/Data/RoundRepository.cs`**
- In `CreateNewRoundAsync()`, holes are created with:
  - `Score = courseHole.Par` (defaults to par for easy adjustment)
  - `IsScored = false` (not counted in total until user modifies)

## Behavior

### Before Scoring
- Hole shows par value as the score
- `IsScored = false`
- Does not contribute to TotalScore
- Running total only includes previously scored holes

### After User Adjusts Score
- User presses + or - button
- `IsScored = true`
- Now contributes to TotalScore
- Running total updates to include this hole

## Database Migration
The schema change adds the `IsScored` column with a default value of 0 (false). 

**For existing data:**
- Existing holes will have `IsScored = 0` (false)
- If you want to mark existing holes as scored, you can run:
  ```sql
  UPDATE Hole SET IsScored = 1 WHERE Score != Par;
  ```

## Testing Recommendations
1. Create a new round - verify holes show par but total is 0
2. Score a few holes - verify total only includes scored holes
3. Navigate between holes - verify scores persist correctly
4. Complete a round - verify final total is accurate
