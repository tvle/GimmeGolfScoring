# Phase 1: Core Scoring Functionality

## Overview
The MVP focuses on making it dead simple to score a round of golf. No bells and whistles—just fast, reliable score tracking.

## User Stories

### US-1.1: Start a New Round
**As a** golfer  
**I want to** quickly start a new round  
**So that** I can begin scoring immediately without setup delays

**Acceptance Criteria**:
- Tap "New Round" from main page
- Select course from list (or use default)
- Round starts immediately with hole 1 active
- Auto-save creates round in database

**UI Mock**:
```
???????????????????????????
?  Golf Scoring App       ?
???????????????????????????
?                         ?
?  [ + New Round ]        ?
?                         ?
?  Recent Rounds          ?
?  ?? Pebble Beach  -2    ?
?  ?? Local Muni   +5     ?
?  ?? Practice     +3     ?
?                         ?
???????????????????????????
```

### US-1.2: Score a Hole
**As a** golfer  
**I want to** enter my score for a hole in < 5 seconds  
**So that** scoring doesn't slow down my round

**Acceptance Criteria**:
- Large +/- buttons for quick tapping
- Score defaults to par
- Visual color coding: Green (under par), Yellow (par), Red (over par)
- Auto-saves on every change
- Swipe left to move to next hole

**UI Mock**:
```
???????????????????????????
?  Hole 7       Par 4     ?
???????????????????????????
?                         ?
?         [ - ]           ?
?                         ?
?           5             ?  ? Red background (over par)
?                         ?
?         [ + ]           ?
?                         ?
?  ? Hole 6   Hole 8 ?    ?
???????????????????????????
```

### US-1.3: Navigate Between Holes
**As a** golfer  
**I want to** easily move between holes  
**So that** I can correct previous scores or jump ahead

**Acceptance Criteria**:
- Swipe left/right between holes
- Hole selector at bottom shows all 18 holes
- Current hole clearly highlighted
- Can jump directly to any hole

### US-1.4: View Round Progress
**As a** golfer  
**I want to** see my current total score  
**So that** I know how I'm performing relative to par

**Acceptance Criteria**:
- Running total always visible at top
- Shows score relative to par (e.g., "+3", "E", "-2")
- Front 9/Back 9 subtotals
- Holes completed vs remaining

### US-1.5: Complete a Round
**As a** golfer  
**I want to** finalize my round when finished  
**So that** it's saved in my history

**Acceptance Criteria**:
- "Finish Round" button appears on hole 18
- Shows final score summary
- Saves EndTime timestamp
- Marks status as "Completed"
- Returns to main page

## Technical Implementation

### Pages

#### MainPage (updated)
- Add "New Round" button
- Show recent rounds with scores
- Navigate to `ActiveRoundPage` on new round

#### ActiveRoundPage (new)
- Full-screen scorecard interface
- Swipeable hole entry
- Running score display
- Complete round button

### Components

#### HoleScoreCard.xaml
```xaml
<Border Style="{StaticResource CardStyle}">
    <Grid RowDefinitions="Auto,*,Auto,Auto">
        <!-- Hole info -->
        <HorizontalStackLayout Grid.Row="0">
            <Label Text="{Binding HoleNumber, StringFormat='Hole {0}'}" 
                   Style="{StaticResource Title2}"/>
            <Label Text="{Binding Par, StringFormat='Par {0}'}" 
                   Style="{StaticResource Title3}"/>
        </HorizontalStackLayout>
        
        <!-- Decrease button -->
        <Button Grid.Row="1" Text="-" 
                Command="{Binding DecreaseScoreCommand}"
                HeightRequest="100"
                Style="{StaticResource ScoreButtonStyle}"/>
        
        <!-- Score display -->
        <Label Grid.Row="2" 
               Text="{Binding Score}"
               Style="{StaticResource Display}"
               BackgroundColor="{Binding ScoreColor}"
               HorizontalOptions="Center"/>
        
        <!-- Increase button -->
        <Button Grid.Row="3" Text="+" 
                Command="{Binding IncreaseScoreCommand}"
                HeightRequest="100"
                Style="{StaticResource ScoreButtonStyle}"/>
    </Grid>
</Border>
```

### ViewModels

#### ActiveRoundPageModel.cs
```csharp
public partial class ActiveRoundPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    
    [ObservableProperty]
    private Round? currentRound;
    
    [ObservableProperty]
    private Hole? currentHole;
    
    [ObservableProperty]
    private int currentHoleIndex;
    
    public string TotalScoreDisplay => 
        CurrentRound?.ScoreRelativeToPar.ToString("+0;-#;E") ?? "E";
    
    [RelayCommand]
    private async Task IncreaseScore()
    {
        if (CurrentHole == null) return;
        CurrentHole.Score++;
        await SaveCurrentHole();
        UpdateTotalScore();
    }
    
    [RelayCommand]
    private async Task DecreaseScore()
    {
        if (CurrentHole == null || CurrentHole.Score <= 1) return;
        CurrentHole.Score--;
        await SaveCurrentHole();
        UpdateTotalScore();
    }
    
    [RelayCommand]
    private async Task NextHole()
    {
        if (CurrentHoleIndex < 17)
        {
            CurrentHoleIndex++;
            CurrentHole = CurrentRound?.Holes[CurrentHoleIndex];
        }
    }
    
    [RelayCommand]
    private async Task CompleteRound()
    {
        if (CurrentRound == null) return;
        CurrentRound.Status = RoundStatus.Completed;
        CurrentRound.EndTime = DateTime.Now;
        await _roundRepository.SaveItemAsync(CurrentRound);
        await Shell.Current.GoToAsync("..");
    }
}
```

### Repositories

#### RoundRepository.cs
```csharp
public class RoundRepository : BaseRepository<Round>
{
    public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
    {
        var course = await Database.Table<Course>()
            .Where(c => c.ID == courseId)
            .FirstOrDefaultAsync();
        
        var round = new Round
        {
            PlayerID = playerId,
            CourseID = courseId,
            StartTime = DateTime.Now,
            Status = RoundStatus.InProgress
        };
        
        await Database.InsertAsync(round);
        
        // Create holes
        var courseHoles = await Database.Table<CourseHole>()
            .Where(ch => ch.CourseID == courseId)
            .OrderBy(ch => ch.HoleNumber)
            .ToListAsync();
        
        foreach (var courseHole in courseHoles)
        {
            var hole = new Hole
            {
                RoundID = round.ID,
                HoleNumber = courseHole.HoleNumber,
                Par = courseHole.Par,
                Score = courseHole.Par // Default to par
            };
            await Database.InsertAsync(hole);
            round.Holes.Add(hole);
        }
        
        return round;
    }
    
    public async Task SaveHoleAsync(Hole hole)
    {
        hole.UpdatedAt = DateTime.Now;
        await Database.UpdateAsync(hole);
    }
}
```

## Design Specifications

### Color Coding
- **Eagle/Better**: Dark Green (#2D7A3E)
- **Birdie**: Light Green (#4CAF50)
- **Par**: Yellow (#FFC107)
- **Bogey**: Orange (#FF9800)
- **Double+**: Red (#F44336)

### Touch Targets
- Buttons minimum 80pt height on mobile
- Score number: 120pt font size
- Swipe gesture: Full screen width

### Accessibility
- VoiceOver/TalkBack: "Hole 7, Par 4, Current score 5, one over par"
- Haptic feedback on score change
- High contrast mode support

## Testing Checklist

- [ ] Can create new round in < 3 taps
- [ ] Can score all 18 holes
- [ ] Scores persist after app restart
- [ ] Works completely offline
- [ ] Battery usage < 5% for 4-hour round
- [ ] No crashes during normal scoring flow
- [ ] Swipe gestures responsive
- [ ] Auto-save prevents data loss

## Performance Targets

- Page load time: < 500ms
- Score update response: < 100ms
- Database save: < 50ms
- Memory usage: < 50MB during active round

---

**Status**: ?? Not Started  
**Dependencies**: Database schema, base repositories  
**Next**: [Phase 2 Features](phase2-enhanced-scoring.md)
