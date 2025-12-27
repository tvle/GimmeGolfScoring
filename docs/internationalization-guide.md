# Internationalization (i18n) Implementation Guide for iDoublePress

## Overview
This guide outlines the steps needed to add multi-language support to the iDoublePress golf scoring app.

## 1. Create Resource Files

### Step 1.1: Create Resources/Strings folder
```
iDoublePress/
  Resources/
    Strings/
      AppResources.resx          (Default - English)
      AppResources.es.resx       (Spanish)
      AppResources.fr.resx       (French)
      AppResources.de.resx       (German)
      AppResources.ja.resx       (Japanese)
      AppResources.ko.resx       (Korean)
      AppResources.zh-CN.resx    (Simplified Chinese)
```

### Step 1.2: Update iDoublePress.csproj
Add to the .csproj file:
```xml
<ItemGroup>
  <Compile Update="Resources\Strings\AppResources.Designer.cs">
    <DesignTime>True</DesignTime>
    <AutoGen>True</AutoGen>
    <DependentUpon>AppResources.resx</DependentUpon>
  </Compile>
</ItemGroup>

<ItemGroup>
  <EmbeddedResource Update="Resources\Strings\AppResources.resx">
    <Generator>ResXFileCodeGenerator</Generator>
    <LastGenOutput>AppResources.Designer.cs</LastGenOutput>
  </EmbeddedResource>
</ItemGroup>
```

## 2. Create LocalizationManager Service

Create `Services/LocalizationManager.cs`:
```csharp
using System.Globalization;

namespace iDoublePress.Services;

public class LocalizationManager
{
    public static LocalizationManager Instance { get; } = new();

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public void SetCulture(CultureInfo culture)
    {
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        CultureChanged?.Invoke(this, new CultureChangedEventArgs(culture));
    }

    public CultureInfo GetCurrentCulture()
    {
        return CultureInfo.CurrentUICulture;
    }

    public List<CultureInfo> GetSupportedCultures()
    {
        return new List<CultureInfo>
        {
            new CultureInfo("en"),    // English
            new CultureInfo("es"),    // Spanish
            new CultureInfo("fr"),    // French
            new CultureInfo("de"),    // German
            new CultureInfo("ja"),    // Japanese
            new CultureInfo("ko"),    // Korean
            new CultureInfo("zh-CN"), // Simplified Chinese
        };
    }
}

public class CultureChangedEventArgs : EventArgs
{
    public CultureInfo NewCulture { get; }

    public CultureChangedEventArgs(CultureInfo newCulture)
    {
        NewCulture = newCulture;
    }
}
```

## 3. Create Translation Markup Extension for XAML

Create `Extensions/TranslateExtension.cs`:
```csharp
using System.Globalization;
using iDoublePress.Resources.Strings;

namespace iDoublePress.Extensions;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension
{
    public string? Key { get; set; }

    public object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (Key == null)
            return null;

        var resourceManager = AppResources.ResourceManager;
        return resourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;
    }
}
```

## 4. Update Code Files to Use Resources

### Example: MainPageModel.cs

**Before:**
```csharp
var resume = await Shell.Current.DisplayAlert(
    "Resume Round?",
    $"You have a round in progress at {inProgressRound.Course?.Name}.\n" +
    $"Started: {timeAgo}\n" +
    $"Progress: {holesCompleted}/{totalHoles} holes\n\n" +
    $"Resume it?",
    "Resume",
    "Start New");
```

**After:**
```csharp
using iDoublePress.Resources.Strings;

var message = string.Format(
    AppResources.ResumeRoundMessage,
    inProgressRound.Course?.Name,
    timeAgo,
    holesCompleted,
    totalHoles);

var resume = await Shell.Current.DisplayAlert(
    AppResources.ResumeRoundTitle,
    message,
    AppResources.Resume,
    AppResources.StartNew);
```

## 5. Update XAML Files to Use Localization

### Example: ActiveRoundPage.xaml

**Add namespace:**
```xml
xmlns:ext="clr-namespace:iDoublePress.Extensions"
xmlns:strings="clr-namespace:iDoublePress.Resources.Strings"
```

**Before:**
```xml
<Label Text="Active Round" />
<Button Text="Abandon" />
<Button Text="Complete Round" />
```

**After:**
```xml
<Label Text="{ext:Translate ActiveRound}" />
<Button Text="{ext:Translate Abandon}" />
<Button Text="{ext:Translate CompleteRound}" />
```

## 6. Key Resource Strings Needed

### Common Strings
- Cancel, OK, Yes, No, Error

### Golf Scoring Strings
- ActiveRound, ResumeRoundTitle, Resume, StartNew
- CompleteRoundTitle, CompleteRound, Abandon
- Previous, Next
- Hole {0}, Par {0}
- HoleFormat, ParFormat

### Time Strings
- JustNow, MinutesAgo, HourAgo, HoursAgo, DayAgo, DaysAgo

### Course/Player Strings
- StandardCourse, NineHoleCourse, PracticeCourse
- DefaultLocation, Me

## 7. Files to Update

### Code Files (C#)
1. **MainPageModel.cs**
   - StartNewRound method
   - GetTimeAgo method

2. **ActiveRoundPageModel.cs**
   - CompleteRound method
   - AbandonRound method
   - LoadRound method

3. **GolfSeedDataService.cs**
   - Course names
   - Default player name
   - Location names

### XAML Files
1. **ActiveRoundPage.xaml**
   - All button text
   - All label text

2. **MainPage.xaml**
   - Section headers
   - Button text

## 8. Add Language Selection UI

Create a settings page or menu option:

```csharp
[RelayCommand]
private async Task ChangeLanguage()
{
    var cultures = LocalizationManager.Instance.GetSupportedCultures();
    var languageNames = cultures.Select(c => c.NativeName).ToArray();
    
    var selected = await Shell.Current.DisplayActionSheet(
        AppResources.SelectLanguage,
        AppResources.Cancel,
        null,
        languageNames);
    
    if (selected != null && selected != AppResources.Cancel)
    {
        var index = Array.IndexOf(languageNames, selected);
        if (index >= 0)
        {
            LocalizationManager.Instance.SetCulture(cultures[index]);
            Preferences.Default.Set("app_language", cultures[index].Name);
            
            // Reload current page or restart app
            await Shell.Current.DisplayAlert(
                AppResources.LanguageChanged,
                AppResources.RestartAppMessage,
                AppResources.OK);
        }
    }
}
```

## 9. Load Saved Language on App Start

In `App.xaml.cs`:
```csharp
public App()
{
    InitializeComponent();
    
    // Load saved language preference
    var savedLanguage = Preferences.Default.Get("app_language", "en");
    try
    {
        var culture = new CultureInfo(savedLanguage);
        LocalizationManager.Instance.SetCulture(culture);
    }
    catch
    {
        // Use default English if saved language is invalid
    }
    
    MainPage = new AppShell();
}
```

## 10. Example Spanish Translation (AppResources.es.resx)

```xml
<data name="ResumeRoundTitle" xml:space="preserve">
  <value>¿Reanudar Ronda?</value>
</data>
<data name="Resume" xml:space="preserve">
  <value>Reanudar</value>
</data>
<data name="StartNew" xml:space="preserve">
  <value>Comenzar Nueva</value>
</data>
<data name="CompleteRound" xml:space="preserve">
  <value>Completar Ronda</value>
</data>
<data name="Abandon" xml:space="preserve">
  <value>Abandonar</value>
</data>
<data name="HoleFormat" xml:space="preserve">
  <value>Hoyo {0}</value>
</data>
<data name="ParFormat" xml:space="preserve">
  <value>Par {0}</value>
</data>
```

## 11. Testing Multi-Language Support

1. **During Development:**
   - Test each language in the emulator
   - Change device language in Settings
   - Verify all strings are translated

2. **Pseudo-localization:**
   - Create a pseudo-language (e.g., en-pseudo) with longer strings
   - Test UI layout with expanded text

3. **RTL Languages (if supporting Arabic, Hebrew):**
   - Test right-to-left layout
   - May need FlowDirection="RightToLeft" in XAML

## 12. Best Practices

1. **Never hardcode strings in C# or XAML**
2. **Use placeholders for dynamic content:** `{0}`, `{1}`, etc.
3. **Keep strings context-specific:** "OK" button vs "OK" affirmation
4. **Test with longest translation** (usually German)
5. **Consider cultural differences:** Date formats, number formats
6. **Use proper plural forms** for different languages
7. **Maintain a translation key spreadsheet** for translators

## 13. Professional Translation Services

For production:
- **Microsoft Translator API** - For automated translation
- **Professional translators** - For golf-specific terminology
- **Community translation** - Via Crowdin or similar platforms

## Priority Languages for Golf App

Based on golf popularity worldwide:
1. **English** (US, UK, Australia)
2. **Spanish** (Spain, Latin America)
3. **Japanese** (Large golf market)
4. **Korean** (Very popular)
5. **French** (Europe, Canada)
6. **German** (Europe)
7. **Chinese** (Growing market)
8. **Thai** (Southeast Asia)
9. **Swedish** (Nordic countries)
10. **Italian** (Europe)

## Implementation Checklist

- [ ] Create Resources/Strings folder
- [ ] Create AppResources.resx (English)
- [ ] Update .csproj with resource configuration
- [ ] Create LocalizationManager service
- [ ] Create TranslateExtension for XAML
- [ ] Update MainPageModel.cs with resources
- [ ] Update ActiveRoundPageModel.cs with resources
- [ ] Update GolfSeedDataService.cs with resources
- [ ] Update ActiveRoundPage.xaml with translations
- [ ] Update MainPage.xaml with translations
- [ ] Add language selection UI
- [ ] Add language persistence (Preferences)
- [ ] Test with at least 2 languages
- [ ] Create additional language .resx files
- [ ] Get professional translations
- [ ] Test RTL languages (if applicable)
- [ ] Update app store descriptions in multiple languages
