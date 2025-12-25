# Phase 4: Social & Advanced Features

## Overview
Enable social and competitive features while maintaining the simplicity of the core app. All features in this phase are opt-in and don't interfere with single-player experience.

## User Stories

### US-4.1: Track Multi-Player Rounds
**As a** golfer  
**I want to** track scores for multiple players in one round  
**So that** I can keep everyone's scores while we play

**Acceptance Criteria**:
- Add players to round before starting
- Switch between players on each hole
- Each player has independent scorecard
- Quick player switching (swipe or tabs)
- All players' scores auto-save

**UI Mock**:
```
???????????????????????????????
?  [John] Mike  Sarah         ?
???????????????????????????????
?  Hole 7       Par 4         ?
?                             ?
?         [ - ]               ?
?           5                 ?
?         [ + ]               ?
?                             ?
?  John: 38 (+2)              ?
?  Mike: 41 (+5)              ?
?  Sarah: 36 (E)              ?
???????????????????????????????
```

### US-4.2: View Live Leaderboard
**As a** golfer  
**I want to** see a live leaderboard during multi-player rounds  
**So that** I know who's winning

**Acceptance Criteria**:
- Leaderboard accessible with button tap
- Shows all players sorted by score
- Updates in real-time as scores entered
- Shows score relative to par
- Indicates leader with icon

### US-4.3: Play Match Play Mode
**As a** golfer  
**I want to** track match play scoring  
**So that** I can compete hole-by-hole

**Acceptance Criteria**:
- Toggle match play mode when starting round
- Shows holes up/down for each player
- Calculates match status (2UP, 1DN, AS)
- Declares winner when mathematically decided
- Option for stroke play + match play simultaneously

### US-4.4: Share Round Results
**As a** golfer  
**I want to** share my round results  
**So that** I can celebrate with friends or post on social media

**Acceptance Criteria**:
- Share button on round summary
- Generates shareable image with:
  - Course name and date
  - Final score
  - Key stats
  - Score distribution chart
- Share via native share sheet (SMS, email, social)

**UI Mock**:
```
???????????????????????????????
?  ??? Golf Score Card         ?
?                             ?
?  John Smith                 ?
?  Pebble Beach Golf Links    ?
?  March 15, 2024             ?
?                             ?
?  Score: 87 (+15)            ?
?  Putts: 32                  ?
?  Fairways: 7/14 (50%)       ?
?  GIR: 5/18 (28%)            ?
?                             ?
?  [Score Distribution Chart] ?
?                             ?
?  Tracked with Golf Score App?
???????????????????????????????
```

### US-4.5: Earn Achievements
**As a** golfer  
**I want to** unlock achievements for milestones  
**So that** I feel rewarded for progress

**Acceptance Criteria**:
- Achievements for:
  - First round completed
  - First birdie/eagle
  - First par round
  - 10/50/100 rounds played
  - Score under 80/90/100
  - Perfect putts (18 putts)
  - Streak (3/5/10 consecutive rounds)
- Notification when achievement unlocked
- View all achievements in profile

### US-4.6: Set and Track Goals
**As a** golfer  
**I want to** set personal goals  
**So that** I stay motivated to improve

**Acceptance Criteria**:
- Set score goals (break 90, 80, etc.)
- Set stat goals (70% fairways, 50% GIR)
- Track progress toward goals
- Celebrate when goals achieved
- Goal suggestions based on current performance

### US-4.7: GPS Yardage (Optional)
**As a** golfer  
**I want to** see distance to the green  
**So that** I can select the right club

**Acceptance Criteria**:
- Opt-in feature (disabled by default for battery)
- Shows distance to front/center/back of green
- Updates as player moves
- Works offline with pre-downloaded course data
- Battery-efficient implementation

### US-4.8: Track Club Selection
**As a** golfer  
**I want to** record which club I used on each shot  
**So that** I can analyze club performance

**Acceptance Criteria**:
- Optional club selection per hole
- Quick picker for common clubs
- Shows club stats (average distance, accuracy)
- Helps identify gapping issues

## Technical Implementation

### Updated Models

#### RoundPlayer.cs (new)
```csharp
public class RoundPlayer
{
    public int ID { get; set; }
    public int RoundID { get; set; }
    public int PlayerID { get; set; }
    public int Order { get; set; }
    
    public Player? Player { get; set; }
    public List<Hole> Holes { get; set; } = new();
    
    public int TotalScore => Holes.Sum(h => h.Score);
    public int ScoreRelativeToPar { get; set; }
}
```

#### Achievement.cs (new)
```csharp
public class Achievement
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public AchievementType Type { get; set; }
    public int TargetValue { get; set; }
}

public class PlayerAchievement
{
    public int ID { get; set; }
    public int PlayerID { get; set; }
    public int AchievementID { get; set; }
    public DateTime UnlockedAt { get; set; }
    
    public Achievement? Achievement { get; set; }
}

public enum AchievementType
{
    RoundsPlayed,
    BestScore,
    Birdie,
    Eagle,
    Streak,
    Perfect
}
```

#### Goal.cs (new)
```csharp
public class Goal
{
    public int ID { get; set; }
    public int PlayerID { get; set; }
    public string Name { get; set; } = string.Empty;
    public GoalType Type { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AchievedAt { get; set; }
    
    public decimal ProgressPercentage => 
        Math.Min((CurrentValue / TargetValue) * 100, 100);
    
    public bool IsAchieved => CurrentValue >= TargetValue;
}

public enum GoalType
{
    BestScore,
    AverageScore,
    Handicap,
    FairwayPercentage,
    GIRPercentage,
    PuttsPerRound
}
```

### New Services

#### AchievementService.cs
```csharp
public class AchievementService
{
    private readonly RoundRepository _roundRepository;
    private readonly PlayerRepository _playerRepository;
    
    public async Task CheckAchievements(int playerId, Round round)
    {
        var achievements = new List<Achievement>();
        
        // Check first round
        var roundCount = await _roundRepository.GetPlayerRoundCountAsync(playerId);
        if (roundCount == 1)
        {
            achievements.Add(CreateAchievement("First Round", "Complete your first round"));
        }
        
        // Check milestone rounds
        if (roundCount == 10 || roundCount == 50 || roundCount == 100)
        {
            achievements.Add(CreateAchievement($"{roundCount} Rounds", 
                $"Play {roundCount} rounds"));
        }
        
        // Check for birdies/eagles
        if (round.Holes.Any(h => h.ScoreTypeEnum == ScoreType.Birdie))
        {
            achievements.Add(CreateAchievement("First Birdie", "Score your first birdie"));
        }
        
        if (round.Holes.Any(h => h.ScoreTypeEnum == ScoreType.Eagle))
        {
            achievements.Add(CreateAchievement("Eagle!", "Score an eagle"));
        }
        
        // Check score milestones
        if (round.TotalScore < 80)
        {
            achievements.Add(CreateAchievement("Break 80", "Score under 80"));
        }
        
        foreach (var achievement in achievements)
        {
            await UnlockAchievement(playerId, achievement);
        }
    }
    
    private async Task UnlockAchievement(int playerId, Achievement achievement)
    {
        // Save to database and show notification
        await Shell.Current.DisplayAlert("?? Achievement Unlocked!", 
            achievement.Description, "Awesome!");
    }
}
```

#### ShareService.cs
```csharp
public class ShareService
{
    public async Task ShareRound(Round round)
    {
        // Generate scorecard image
        var image = await GenerateScorecardImage(round);
        
        // Use native share
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"Golf Round - {round.Course?.Name}",
            File = new ShareFile(image)
        });
    }
    
    private async Task<string> GenerateScorecardImage(Round round)
    {
        // Create image with:
        // - Course name and date
        // - Final score
        // - Key stats
        // - Small chart
        
        // Implementation would use SkiaSharp or similar
        // to generate image programmatically
        
        return "path/to/generated/image.png";
    }
}
```

### ViewModels

#### MultiPlayerRoundPageModel.cs
```csharp
public partial class MultiPlayerRoundPageModel : ObservableObject
{
    [ObservableProperty]
    private Round? currentRound;
    
    [ObservableProperty]
    private RoundPlayer? currentPlayer;
    
    [ObservableProperty]
    private int currentHoleIndex;
    
    public ObservableCollection<RoundPlayer> Players { get; } = new();
    
    public List<LeaderboardEntry> Leaderboard => 
        Players
            .Select(p => new LeaderboardEntry 
            { 
                Player = p, 
                Position = GetPosition(p) 
            })
            .OrderBy(e => e.Player.TotalScore)
            .ToList();
    
    [RelayCommand]
    private void SwitchPlayer(RoundPlayer player)
    {
        CurrentPlayer = player;
        OnPropertyChanged(nameof(CurrentHole));
    }
    
    [RelayCommand]
    private async Task ShowLeaderboard()
    {
        // Display leaderboard modal
    }
    
    private int GetPosition(RoundPlayer player)
    {
        return Players.OrderBy(p => p.TotalScore).ToList().IndexOf(player) + 1;
    }
}

public class LeaderboardEntry
{
    public RoundPlayer? Player { get; set; }
    public int Position { get; set; }
    public bool IsLeader => Position == 1;
}
```

## Design Specifications

### Multi-Player UX
- Player tabs at top
- Clear indication of current player
- Swipe between players
- Leaderboard as slide-up panel
- Color-code each player

### Achievements
- Toast notification on unlock
- Achievement icon library
- Progress indicators for near-completion
- Achievement gallery page

### Share Image
- Branded template
- High-quality rendering
- Include app logo/name
- Optimized file size for sharing

### GPS (if implemented)
- Large, readable distance display
- Update frequency: every 5 seconds max
- Low-power mode option
- Offline course database

## Platform Considerations

### GPS Permissions
- iOS: Request "When In Use" location
- Android: Request FINE_LOCATION
- Explain usage to user
- Handle permission denial gracefully

### Sharing
- Use MAUI Community Toolkit Share API
- Platform-specific image rendering
- Handle missing share targets

### Battery Optimization
- GPS in background: minimize updates
- Use device motion to detect when moving
- Sleep when stationary
- Warn user about battery impact

## Testing Checklist

- [ ] Multi-player scoring accurate for all players
- [ ] Leaderboard sorts correctly
- [ ] Match play calculation correct
- [ ] Share generates proper image
- [ ] Achievements unlock at correct times
- [ ] No duplicate achievements
- [ ] Goals track progress accurately
- [ ] GPS updates smoothly (if enabled)
- [ ] Battery usage acceptable
- [ ] Works offline
- [ ] All platforms tested

## Performance Targets

- Multi-player player switch: < 100ms
- Leaderboard calculation: < 50ms
- Share image generation: < 2s
- GPS update: < 1s
- Achievement check: < 100ms

## Privacy Considerations

- GPS data stays local (not uploaded)
- Share is opt-in
- Multi-player data is local
- No automatic social posting
- Clear privacy policy

---

**Status**: ?? Not Started  
**Dependencies**: Phase 3 completion  
**Future**: Cloud sync, tournaments, course marketplace
