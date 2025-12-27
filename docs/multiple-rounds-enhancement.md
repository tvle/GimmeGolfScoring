# Multiple In-Progress Rounds Enhancement

## Problem
The app could have multiple in-progress rounds, but the dialog only showed basic information (course name), making it difficult for users to identify which round to resume.

## Solution

### 1. Added Timestamp and Progress Information
Updated the resume dialog to show:
- **When the round was started** (user-friendly time ago format)
- **Progress** (holes completed / total holes)

### 2. Added Support for Multiple In-Progress Rounds

#### Single In-Progress Round
Shows an enhanced dialog with detailed information:
```
Resume Round?

You have a round in progress at Standard Course.
Started: 2 hours ago
Progress: 9/18 holes

Resume it?
[Resume] [Start New]
```

#### Multiple In-Progress Rounds
Shows an action sheet listing all in-progress rounds with details:
```
You have 3 rounds in progress

Standard Course - 2 hours ago (9/18)
9-Hole Course - Yesterday (5/9)
Practice Course - 3 days ago (12/18)
Start New Round
[Cancel]
```

### 3. New Repository Method
Added `GetInProgressRoundsAsync()` to retrieve all in-progress rounds for a player:
```csharp
public async Task<List<Round>> GetInProgressRoundsAsync(int playerId)
```

### 4. User-Friendly Time Display
Added `GetTimeAgo()` helper method that formats timestamps as:
- "Just now" (< 1 minute)
- "5 min ago" (< 1 hour)
- "2 hours ago" (< 24 hours)
- "3 days ago" (< 7 days)
- "Jan 15, 2:30 PM" (> 7 days)

## Files Changed
1. **iDoublePress/Data/RoundRepository.cs**
   - Added `GetInProgressRoundsAsync()` method

2. **iDoublePress/PageModels/MainPageModel.cs**
   - Updated `StartNewRound()` to handle single and multiple in-progress rounds
   - Added `GetTimeAgo()` helper method

## User Experience
- Users can now see when they started each round
- Progress information helps identify which round to continue
- Multiple rounds are easy to distinguish and select
- Timestamps are shown in a natural, readable format
