# Localization Quick Start Guide

## What You Need to Do

To support multiple languages in your iDoublePress golf app, follow these steps:

### 1. Install Visual Studio Extension (Optional but Recommended)
- **ResX Manager** extension for Visual Studio makes managing translations easier
- Install from Extensions > Manage Extensions > Search "ResXManager"

### 2. Create Resource Files

Create these files in `iDoublePress/Resources/Strings/`:

```
AppResources.resx          (English - default)
AppResources.es.resx       (Spanish)
AppResources.fr.resx       (French)
AppResources.de.resx       (German)
AppResources.ja.resx       (Japanese)
AppResources.ko.resx       (Korean)
```

### 3. Update .csproj File

Add this to `iDoublePress.csproj`:

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
    <CustomToolNamespace>iDoublePress.Resources.Strings</CustomToolNamespace>
  </EmbeddedResource>
</ItemGroup>
```

### 4. Sample AppResources.resx Content

Right-click in Visual Studio > Add > New Item > Resources File
Name it: `AppResources.resx`

Add these key-value pairs:

| Name | Value (English) |
|------|----------------|
| Cancel | Cancel |
| OK | OK |
| ActiveRound | Active Round |
| Resume | Resume |
| StartNew | Start New |
| CompleteRound | Complete Round |
| Abandon | Abandon |
| Previous | Previous |
| Next | Next |
| HoleFormat | Hole {0} |
| ParFormat | Par {0} |
| ResumeRoundTitle | Resume Round? |
| JustNow | Just now |
| MinutesAgo | {0} min ago |

### 5. Use Resources in C# Code

**Update MainPageModel.cs:**

At the top, add:
```csharp
using iDoublePress.Resources.Strings;
```

Replace hardcoded strings:
```csharp
// Before
"Resume Round?"

// After  
AppResources.ResumeRoundTitle
```

### 6. Create XAML Extension for Bindings

Already created for you in: `iDoublePress/Extensions/TranslateExtension.cs`

You'll need to create this file (see full implementation guide).

### 7. Use in XAML

```xml
xmlns:ext="clr-namespace:iDoublePress.Extensions"

<Label Text="{ext:Translate ActiveRound}" />
<Button Text="{ext:Translate Resume}" />
```

### 8. Initialize on App Start

Update `App.xaml.cs`:

```csharp
using iDoublePress.Services;

public App()
{
    InitializeComponent();
    
    // Load saved language or use device language
    LocalizationManager.Instance.LoadSavedLanguage();
    
    MainPage = new AppShell();
}
```

### 9. Test Different Languages

Change your device language in Settings to test, or add a language picker:

```csharp
[RelayCommand]
private async Task SelectLanguage()
{
    var cultures = LocalizationManager.Instance.GetSupportedCultures();
    var names = cultures.Select(c => c.NativeName).ToArray();
    
    var selected = await Shell.Current.DisplayActionSheet(
        "Select Language",
        "Cancel",
        null,
        names);
    
    if (selected != null && selected != "Cancel")
    {
        var index = Array.IndexOf(names, selected);
        var culture = cultures[index];
        LocalizationManager.Instance.SetCulture(culture);
        LocalizationManager.Instance.SaveLanguagePreference(culture);
        
        // Show message that app needs restart or refresh pages
    }
}
```

## Files Already Created

? `Services/LocalizationManager.cs` - Manages language switching
? `docs/internationalization-guide.md` - Complete implementation guide

## Next Steps

1. **Create the resource files** (.resx) manually or use ResX Manager
2. **Extract all hardcoded strings** from your code
3. **Update all C# files** to use `AppResources.StringName`
4. **Create TranslateExtension** for XAML
5. **Update XAML files** to use translate extension
6. **Get translations** (use AI or professional translator)
7. **Test thoroughly** with each language

## Translation Priority

Start with these most important for golf:
1. ? English (already done)
2. Spanish (large market)
3. Japanese (very popular)
4. Korean (very popular)

## Tools for Translation

- **Microsoft Translator API** - For initial draft
- **Google Translate** - For reference
- **Professional translator** - For final polish (especially golf terms)
- **Native speaking golfers** - For review and validation

## Cost Estimation

- Professional translation: ~$0.10-0.20 per word
- Estimated words in app: ~500-1000
- Per language cost: $50-200
- Total for 5 languages: $250-1000

Alternatively, use AI translation + native review for ~50% cost savings.
