# Phase 3: Historical Data & Analytics

## Overview
Provide powerful analytics and insights by analyzing historical rounds, enabling golfers to track improvement and understand their game deeply.

## User Stories

### US-3.1: View Round History
**As a** golfer  
**I want to** see all my past rounds  
**So that** I can review my golf history

**Acceptance Criteria**:
- List of all completed rounds
- Shows date, course, score, score vs par
- Sorted by date (newest first)
- Pull-to-refresh to update
- Tap to view round details

**UI Mock**:
```
???????????????????????????????
?  Round History              ?
???????????????????????????????
?  ?? [Search]  [Filter ?]    ?
?                             ?
?  Today                      ?
?  ?? Pebble Beach     87 +15 ?
?  ?  18 holes, 3h 45m        ?
?                             ?
?  This Week                  ?
?  ?? Local Muni       78 +6  ?
?  ?  18 holes, 3h 20m        ?
?  ?? Practice         41 +5  ?
?     9 holes, 1h 45m         ?
?                             ?
???????????????????????????????
```

### US-3.2: Filter and Search Rounds
**As a** golfer  
**I want to** filter rounds by course or date  
**So that** I can find specific rounds quickly

**Acceptance Criteria**:
- Filter by course
- Filter by date range
- Search by notes
- Clear filters option
- Shows count of filtered results

### US-3.3: View Performance Trends
**As a** golfer  
**I want to** see my score trends over time  
**So that** I can track improvement

**Acceptance Criteria**:
- Line chart showing score over time
- Shows trend line
- Filter by time period (1M, 3M, 6M, 1Y, All)
- Average score displayed
- Best/worst rounds highlighted

**UI Mock**:
```
???????????????????????????????
?  Performance Trends         ?
???????????????????????????????
?  [ 1M ] [ 3M ] [6M] [1Y] All?
?                             ?
?  [Line Chart: Score vs Date]?
?                             ?
?  Average: 85.4              ?
?  Best: 78                   ?
?  Worst: 95                  ?
?  Rounds: 24                 ?
???????????????????????????????
```

### US-3.4: Calculate Handicap Index
**As a** golfer  
**I want to** calculate my handicap index  
**So that** I can compete fairly with others

**Acceptance Criteria**:
- Uses USGA handicap formula
- Requires minimum 5 rounds
- Shows current handicap index
- Updates automatically with new rounds
- Displays trend (improving/declining)

### US-3.5: Analyze Stats by Category
**As a** golfer  
**I want to** see averages for each stat category  
**So that** I can identify strengths and weaknesses

**Acceptance Criteria**:
- Average score per round
- Average putts per round
- Fairway hit percentage
- GIR percentage
- Best/worst stats highlighted
- Compare to last month

**UI Mock**:
```
???????????????????????????????
?  Analytics Dashboard        ?
???????????????????????????????
?  Last 10 Rounds             ?
?                             ?
?  Avg Score: 87.2 (+15.2)    ?
?  Avg Putts: 32.4 (1.8/hole) ?
?  Fairways:  52% ?           ?
?  GIR:       28% ?           ?
?  Penalties: 1.8/round       ?
?                             ?
?  [Detailed Stats ?]         ?
???????????????????????????????
```

### US-3.6: Manage Courses
**As a** golfer  
**I want to** add and edit golf courses  
**So that** I can score rounds at any course

**Acceptance Criteria**:
- List of all courses (built-in + custom)
- Add custom course
- Edit course details (name, par, hole info)
- Delete custom courses
- Favorite courses for quick access

### US-3.7: View Course Stats
**As a** golfer  
**I want to** see my performance at specific courses  
**So that** I can understand which courses suit my game

**Acceptance Criteria**:
- Average score at course
- Best/worst round at course
- Number of rounds played
- Average by hole
- Hole-by-hole performance chart

## Technical Implementation

### New Pages

#### RoundHistoryPage.xaml
```xaml
<ContentPage Title="Round History">
    <Grid RowDefinitions="Auto,*">
        <!-- Search and Filter -->
        <HorizontalStackLayout Grid.Row="0" Padding="15" Spacing="10">
            <SearchBar Placeholder="Search rounds..." 
                       Text="{Binding SearchText}"
                       HorizontalOptions="FillAndExpand"/>
            <Button Text="Filter" Command="{Binding ShowFilterCommand}"/>
        </HorizontalStackLayout>
        
        <!-- Rounds List -->
        <CollectionView Grid.Row="1" 
                        ItemsSource="{Binding GroupedRounds}"
                        IsGrouped="True"
                        SelectionMode="Single"
                        SelectedItem="{Binding SelectedRound}"
                        SelectionChangedCommand="{Binding NavigateToRoundCommand}">
            <CollectionView.GroupHeaderTemplate>
                <DataTemplate>
                    <Label Text="{Binding GroupName}" 
                           Style="{StaticResource Title3}"
                           Padding="15,10"/>
                </DataTemplate>
            </CollectionView.GroupHeaderTemplate>
            
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="models:Round">
                    <Border Style="{StaticResource CardStyle}" Margin="15,5">
                        <Grid ColumnDefinitions="*,Auto">
                            <VerticalStackLayout Grid.Column="0">
                                <Label Text="{Binding Course.Name}" 
                                       Style="{StaticResource Title3}"/>
                                <Label Text="{Binding StartTime, StringFormat='{0:MMM dd, yyyy}'}"
                                       Style="{StaticResource Caption1}"/>
                                <Label Text="{Binding Duration, StringFormat='{0:h}h {0:m}m'}"
                                       Style="{StaticResource Caption1}"/>
                            </VerticalStackLayout>
                            
                            <VerticalStackLayout Grid.Column="1" HorizontalOptions="End">
                                <Label Text="{Binding TotalScore}" 
                                       Style="{StaticResource LargeTitle}"/>
                                <Label Text="{Binding ScoreRelativeToPar, StringFormat='{0:+0;-#;E}'}"
                                       Style="{StaticResource Title2}"/>
                            </VerticalStackLayout>
                        </Grid>
                    </Border>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </Grid>
</ContentPage>
```

#### AnalyticsPage.xaml
```xaml
<ContentPage Title="Analytics">
    <ScrollView>
        <VerticalStackLayout Padding="{StaticResource LayoutPadding}" 
                             Spacing="{StaticResource LayoutSpacing}">
            
            <!-- Handicap -->
            <Border Style="{StaticResource CardStyle}">
                <VerticalStackLayout>
                    <Label Text="Handicap Index" Style="{StaticResource Title2}"/>
                    <Label Text="{Binding HandicapIndex, StringFormat='{0:F1}'}" 
                           Style="{StaticResource Display}"/>
                    <Label Text="{Binding HandicapTrend}" 
                           Style="{StaticResource Caption1}"/>
                </VerticalStackLayout>
            </Border>
            
            <!-- Trends Chart -->
            <Border Style="{StaticResource CardStyle}">
                <VerticalStackLayout>
                    <Label Text="Score Trends" Style="{StaticResource Title2}"/>
                    
                    <HorizontalStackLayout Spacing="5" HorizontalOptions="Center">
                        <Button Text="1M" Command="{Binding SetPeriodCommand}" CommandParameter="1"/>
                        <Button Text="3M" Command="{Binding SetPeriodCommand}" CommandParameter="3"/>
                        <Button Text="6M" Command="{Binding SetPeriodCommand}" CommandParameter="6"/>
                        <Button Text="1Y" Command="{Binding SetPeriodCommand}" CommandParameter="12"/>
                        <Button Text="All" Command="{Binding SetPeriodCommand}" CommandParameter="0"/>
                    </HorizontalStackLayout>
                    
                    <chart:SfCartesianChart HeightRequest="250">
                        <chart:SfCartesianChart.XAxes>
                            <chart:DateTimeAxis/>
                        </chart:SfCartesianChart.XAxes>
                        <chart:SfCartesianChart.YAxes>
                            <chart:NumericalAxis/>
                        </chart:SfCartesianChart.YAxes>
                        
                        <chart:LineSeries ItemsSource="{Binding ScoreTrends}"
                                         XBindingPath="Date"
                                         YBindingPath="Score"/>
                    </chart:SfCartesianChart>
                </VerticalStackLayout>
            </Border>
            
            <!-- Stats Summary -->
            <Border Style="{StaticResource CardStyle}">
                <VerticalStackLayout Spacing="10">
                    <Label Text="Performance Stats" Style="{StaticResource Title2}"/>
                    
                    <Grid ColumnDefinitions="*,Auto,Auto" RowDefinitions="Auto,Auto,Auto,Auto,Auto">
                        <!-- Headers -->
                        <Label Grid.Row="0" Grid.Column="1" Text="Value" Style="{StaticResource Caption1Strong}"/>
                        <Label Grid.Row="0" Grid.Column="2" Text="Trend" Style="{StaticResource Caption1Strong}"/>
                        
                        <!-- Avg Score -->
                        <Label Grid.Row="1" Grid.Column="0" Text="Avg Score"/>
                        <Label Grid.Row="1" Grid.Column="1" Text="{Binding AvgScore, StringFormat='{0:F1}'}"/>
                        <Label Grid.Row="1" Grid.Column="2" Text="{Binding AvgScoreTrend}"/>
                        
                        <!-- Avg Putts -->
                        <Label Grid.Row="2" Grid.Column="0" Text="Avg Putts"/>
                        <Label Grid.Row="2" Grid.Column="1" Text="{Binding AvgPutts, StringFormat='{0:F1}'}"/>
                        <Label Grid.Row="2" Grid.Column="2" Text="{Binding AvgPuttsTrend}"/>
                        
                        <!-- Fairways -->
                        <Label Grid.Row="3" Grid.Column="0" Text="Fairways"/>
                        <Label Grid.Row="3" Grid.Column="1" Text="{Binding FairwayPercentage, StringFormat='{0:F0}%'}"/>
                        <Label Grid.Row="3" Grid.Column="2" Text="{Binding FairwayTrend}"/>
                        
                        <!-- GIR -->
                        <Label Grid.Row="4" Grid.Column="0" Text="GIR"/>
                        <Label Grid.Row="4" Grid.Column="1" Text="{Binding GIRPercentage, StringFormat='{0:F0}%'}"/>
                        <Label Grid.Row="4" Grid.Column="2" Text="{Binding GIRTrend}"/>
                    </Grid>
                </VerticalStackLayout>
            </Border>
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

### Services

#### HandicapCalculator.cs
```csharp
public class HandicapCalculator
{
    public decimal CalculateHandicapIndex(List<Round> rounds)
    {
        if (rounds.Count < 5)
            return 0;
        
        // Use best 8 of last 20 rounds (simplified USGA method)
        var recentRounds = rounds
            .OrderByDescending(r => r.StartTime)
            .Take(20)
            .ToList();
        
        var scoreDifferentials = recentRounds
            .Select(r => CalculateScoreDifferential(r))
            .OrderBy(sd => sd)
            .Take(8)
            .ToList();
        
        var average = scoreDifferentials.Average();
        return Math.Round(average * 0.96m, 1);
    }
    
    private decimal CalculateScoreDifferential(Round round)
    {
        if (round.Course?.Rating == null || round.Course?.Slope == null)
            return 0;
        
        return ((decimal)round.TotalScore - round.Course.Rating.Value) * 113 / round.Course.Slope.Value;
    }
}
```

#### StatsCalculator.cs
```csharp
public class StatsCalculator
{
    public PerformanceStats CalculatePerformanceStats(List<Round> rounds)
    {
        if (!rounds.Any())
            return new PerformanceStats();
        
        var allHoles = rounds.SelectMany(r => r.Holes).ToList();
        
        return new PerformanceStats
        {
            AverageScore = rounds.Average(r => r.TotalScore),
            AveragePutts = allHoles.Average(h => h.Putts),
            FairwayPercentage = CalculateFairwayPercentage(allHoles),
            GIRPercentage = CalculateGIRPercentage(allHoles),
            BestRound = rounds.OrderBy(r => r.TotalScore).First(),
            WorstRound = rounds.OrderByDescending(r => r.TotalScore).First()
        };
    }
    
    private decimal CalculateFairwayPercentage(List<Hole> holes)
    {
        var par4and5 = holes.Where(h => h.Par >= 4 && h.FairwayHit.HasValue).ToList();
        if (!par4and5.Any()) return 0;
        
        return (decimal)par4and5.Count(h => h.FairwayHit == true) / par4and5.Count * 100;
    }
    
    private decimal CalculateGIRPercentage(List<Hole> holes)
    {
        var withGIR = holes.Where(h => h.GreenInRegulation.HasValue).ToList();
        if (!withGIR.Any()) return 0;
        
        return (decimal)withGIR.Count(h => h.GreenInRegulation == true) / withGIR.Count * 100;
    }
}

public class PerformanceStats
{
    public decimal AverageScore { get; set; }
    public decimal AveragePutts { get; set; }
    public decimal FairwayPercentage { get; set; }
    public decimal GIRPercentage { get; set; }
    public Round? BestRound { get; set; }
    public Round? WorstRound { get; set; }
}
```

### ViewModels

#### AnalyticsPageModel.cs
```csharp
public partial class AnalyticsPageModel : ObservableObject
{
    private readonly RoundRepository _roundRepository;
    private readonly HandicapCalculator _handicapCalculator;
    private readonly StatsCalculator _statsCalculator;
    
    [ObservableProperty]
    private decimal handicapIndex;
    
    [ObservableProperty]
    private PerformanceStats? currentStats;
    
    [ObservableProperty]
    private int selectedPeriodMonths = 0; // 0 = All
    
    public ObservableCollection<ScoreTrendPoint> ScoreTrends { get; } = new();
    
    [RelayCommand]
    private async Task LoadAnalytics()
    {
        var allRounds = await _roundRepository.ListAsync();
        var filteredRounds = FilterByPeriod(allRounds);
        
        HandicapIndex = _handicapCalculator.CalculateHandicapIndex(allRounds);
        CurrentStats = _statsCalculator.CalculatePerformanceStats(filteredRounds);
        
        UpdateScoreTrends(filteredRounds);
    }
    
    [RelayCommand]
    private async Task SetPeriod(int months)
    {
        SelectedPeriodMonths = months;
        await LoadAnalytics();
    }
    
    private List<Round> FilterByPeriod(List<Round> rounds)
    {
        if (SelectedPeriodMonths == 0)
            return rounds;
        
        var cutoffDate = DateTime.Now.AddMonths(-SelectedPeriodMonths);
        return rounds.Where(r => r.StartTime >= cutoffDate).ToList();
    }
}

public class ScoreTrendPoint
{
    public DateTime Date { get; set; }
    public int Score { get; set; }
}
```

## Design Specifications

### Charts
- Line charts for trends
- Bar charts for comparisons
- Responsive with OnIdiom
- Color-coded for easy reading

### Performance Optimization
- Pagination for round history (25 per page)
- Lazy loading of round details
- Cache calculated stats
- Background calculation for handicap

## Testing Checklist

- [ ] History displays all rounds correctly
- [ ] Filtering works accurately
- [ ] Search finds rounds by notes
- [ ] Handicap calculation matches USGA formula
- [ ] Trends update when period changes
- [ ] Stats calculate correctly with partial data
- [ ] Course management CRUD operations work
- [ ] Performance good with 100+ rounds
- [ ] Charts render on all platforms

## Performance Targets

- History page load: < 1s for 100 rounds
- Analytics calculation: < 500ms
- Chart rendering: < 300ms
- Search results: < 200ms

---

**Status**: ?? Not Started  
**Dependencies**: Phase 2 completion  
**Next**: [Phase 4 Features](phase4-social.md)
