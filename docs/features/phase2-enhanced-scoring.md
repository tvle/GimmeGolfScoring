# Phase 2: Enhanced Scoring Experience

## Overview
Build on the core scoring functionality to capture detailed statistics and provide immediate insights during and after rounds.

## User Stories

### US-2.1: Track Additional Stats
**As a** golfer  
**I want to** record additional stats like putts and fairways hit  
**So that** I can analyze my performance in detail

**Acceptance Criteria**:
- Optional "Stats" button on scorecard
- Quick entry for: Putts, Fairway Hit (Y/N), GIR (Y/N), Penalties
- Doesn't slow down basic scoring workflow
- Stats entry is skippable

**UI Mock**:
```
???????????????????????????
?  Hole 7       Par 4     ?
?  Score: 5               ?
???????????????????????????
?  [ Stats ? ]            ?
?                         ?
?  Putts:  [-]  2  [+]    ?
?  Fairway:  ? Yes  ? No  ?
?  GIR:      ? Yes  ? No  ?
?  Penalties: [-] 0 [+]   ?
?                         ?
???????????????????????????
```

### US-2.2: View Score Distribution Chart
**As a** golfer  
**I want to** see my score distribution visually  
**So that** I can quickly understand my performance patterns

**Acceptance Criteria**:
- Doughnut chart showing eagles, birdies, pars, bogeys, etc.
- Similar to existing CategoryChart in MainPage
- Updates in real-time as round progresses
- Accessible from active round page

### US-2.3: See Front 9 / Back 9 Comparison
**As a** golfer  
**I want to** compare my front 9 and back 9 scores  
**So that** I can understand when I play better

**Acceptance Criteria**:
- Shows Front 9 score and relative to par
- Shows Back 9 score and relative to par
- Appears after completing hole 9
- Updates dynamically

**UI Mock**:
```
???????????????????????????
?  Round Progress         ?
???????????????????????????
?  Front 9:  38  (+2)     ?
?  Back 9:   --  (--)     ?
?                         ?
?  Total:    38  (+2)     ?
???????????????????????????
```

### US-2.4: View Round Summary
**As a** golfer  
**I want to** see a detailed summary after completing a round  
**So that** I can review my performance

**Acceptance Criteria**:
- Automatic navigation to summary after round completion
- Shows total score, score vs par
- Displays all captured stats
- Score distribution chart
- Best/worst holes
- Option to add notes

### US-2.5: Highlight Best and Worst Holes
**As a** golfer  
**I want to** see my best and worst holes  
**So that** I can celebrate successes and identify struggles

**Acceptance Criteria**:
- Shows best hole (lowest score relative to par)
- Shows worst hole (highest score relative to par)
- Displays on round summary page
- Includes hole number and score

## Technical Implementation

### Updated Models

#### Hole.cs (extended)
```csharp
public class Hole
{
    // ...existing properties...
    
    public int Putts { get; set; }
    public bool? FairwayHit { get; set; }
    public bool? GreenInRegulation { get; set; }
    public int Penalties { get; set; }
    
    // Calculated properties
    public Color ScoreColor => ScoreTypeEnum switch
    {
        ScoreType.Eagle => Color.FromArgb("#2D7A3E"),
        ScoreType.Birdie => Color.FromArgb("#4CAF50"),
        ScoreType.Par => Color.FromArgb("#FFC107"),
        ScoreType.Bogey => Color.FromArgb("#FF9800"),
        _ => Color.FromArgb("#F44336")
    };
}
```

#### RoundStats.cs (new)
```csharp
public class RoundStats
{
    public int ID { get; set; }
    public int RoundID { get; set; }
    public int TotalPutts { get; set; }
    public int FairwaysHit { get; set; }
    public int FairwaysAttempted { get; set; }
    public int GreensInRegulation { get; set; }
    public int SandSaves { get; set; }
    public int TotalPenalties { get; set; }
    public int Front9Score { get; set; }
    public int Back9Score { get; set; }
    
    public decimal FairwayPercentage => 
        FairwaysAttempted > 0 ? (decimal)FairwaysHit / FairwaysAttempted * 100 : 0;
    
    public decimal GIRPercentage => 
        (decimal)GreensInRegulation / 18 * 100;
    
    public decimal AveragePutts => 
        TotalPutts > 0 ? (decimal)TotalPutts / 18 : 0;
}
```

### New Components

#### HoleStatsEntry.xaml
```xaml
<Border Style="{StaticResource CardStyle}" IsVisible="{Binding ShowStats}">
    <VerticalStackLayout Spacing="10">
        <Label Text="Hole Stats" Style="{StaticResource Title3}"/>
        
        <!-- Putts -->
        <HorizontalStackLayout Spacing="10">
            <Label Text="Putts:" VerticalOptions="Center" WidthRequest="100"/>
            <Button Text="-" Command="{Binding DecreasePuttsCommand}"/>
            <Label Text="{Binding CurrentHole.Putts}" 
                   Style="{StaticResource Title2}"
                   WidthRequest="50"
                   HorizontalTextAlignment="Center"/>
            <Button Text="+" Command="{Binding IncreasePuttsCommand}"/>
        </HorizontalStackLayout>
        
        <!-- Fairway Hit -->
        <HorizontalStackLayout Spacing="10">
            <Label Text="Fairway:" VerticalOptions="Center" WidthRequest="100"/>
            <CheckBox IsChecked="{Binding CurrentHole.FairwayHit}" />
        </HorizontalStackLayout>
        
        <!-- GIR -->
        <HorizontalStackLayout Spacing="10">
            <Label Text="GIR:" VerticalOptions="Center" WidthRequest="100"/>
            <CheckBox IsChecked="{Binding CurrentHole.GreenInRegulation}" />
        </HorizontalStackLayout>
        
        <!-- Penalties -->
        <HorizontalStackLayout Spacing="10">
            <Label Text="Penalties:" VerticalOptions="Center" WidthRequest="100"/>
            <Button Text="-" Command="{Binding DecreasePenaltiesCommand}"/>
            <Label Text="{Binding CurrentHole.Penalties}" 
                   Style="{StaticResource Title2}"
                   WidthRequest="50"
                   HorizontalTextAlignment="Center"/>
            <Button Text="+" Command="{Binding IncreasePenaltiesCommand}"/>
        </HorizontalStackLayout>
    </VerticalStackLayout>
</Border>
```

#### ScoreDistributionChart.xaml
```xaml
<Border Style="{StaticResource CardStyle}">
    <chart:SfCircularChart>
        <chart:DoughnutSeries 
            ItemsSource="{Binding ScoreDistribution}"
            PaletteBrushes="{Binding ScoreColors}"
            XBindingPath="ScoreType"
            YBindingPath="Count"
            ShowDataLabels="True"
            Radius="0.6"
            InnerRadius="0.7">
            <chart:DoughnutSeries.DataLabelSettings>
                <chart:CircularDataLabelSettings LabelPosition="Outside"/>
            </chart:DoughnutSeries.DataLabelSettings>
        </chart:DoughnutSeries>
    </chart:SfCircularChart>
</Border>
```

### ViewModels

#### RoundSummaryPageModel.cs (new)
```csharp
public partial class RoundSummaryPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    
    [ObservableProperty]
    private Round? round;
    
    [ObservableProperty]
    private RoundStats? stats;
    
    public ObservableCollection<ScoreDistributionItem> ScoreDistribution { get; } = new();
    
    public Hole? BestHole => Round?.Holes.OrderBy(h => h.ScoreRelativeToPar).FirstOrDefault();
    public Hole? WorstHole => Round?.Holes.OrderByDescending(h => h.ScoreRelativeToPar).FirstOrDefault();
    
    public async Task LoadRoundAsync(int roundId)
    {
        Round = await _roundRepository.GetItemAsync(roundId);
        Stats = await CalculateStats();
        UpdateScoreDistribution();
    }
    
    private void UpdateScoreDistribution()
    {
        if (Round == null) return;
        
        ScoreDistribution.Clear();
        
        var grouped = Round.Holes
            .GroupBy(h => h.ScoreTypeEnum)
            .Select(g => new ScoreDistributionItem 
            { 
                ScoreType = g.Key.ToString(), 
                Count = g.Count() 
            });
        
        foreach (var item in grouped)
        {
            ScoreDistribution.Add(item);
        }
    }
}

public class ScoreDistributionItem
{
    public string ScoreType { get; set; } = string.Empty;
    public int Count { get; set; }
}
```

## Design Specifications

### Stats Entry
- Collapsible panel below score entry
- Default collapsed to not overwhelm users
- Remembers user preference (expanded/collapsed)
- Touch targets: 44pt minimum

### Charts
- Reuse Syncfusion SfCircularChart
- Color scheme matches score colors
- Responsive sizing with OnIdiom
- Accessible labels for screen readers

### Round Summary Layout
```
???????????????????????????????
?  Round Complete! ??         ?
???????????????????????????????
?                             ?
?  Total Score: 87  (+15)     ?
?                             ?
?  Front 9:  44  (+8)         ?
?  Back 9:   43  (+7)         ?
?                             ?
?  [Score Distribution Chart] ?
?                             ?
?  ?? Stats                   ?
?  ?? Putts: 32 (1.8 avg)    ?
?  ?? Fairways: 7/14 (50%)   ?
?  ?? GIR: 5/18 (28%)        ?
?  ?? Penalties: 2           ?
?                             ?
?  ?? Best Hole: #12 (Par)    ?
?  ?? Worst Hole: #8 (+3)     ?
?                             ?
?  [ Add Notes ]              ?
?  [ Done ]                   ?
???????????????????????????????
```

## Testing Checklist

- [ ] Stats entry doesn't interfere with basic scoring
- [ ] Chart updates in real-time during round
- [ ] Front 9 / Back 9 calculation accurate
- [ ] Summary displays all captured data
- [ ] Best/worst hole calculation correct
- [ ] Works with partial stats (some holes without stats)
- [ ] Accessibility labels accurate
- [ ] Charts render correctly on all platforms

## Performance Targets

- Stats calculation: < 100ms
- Chart rendering: < 200ms
- Summary page load: < 500ms

---

**Status**: ?? Not Started  
**Dependencies**: Phase 1 completion  
**Next**: [Phase 3 Features](phase3-analytics.md)
