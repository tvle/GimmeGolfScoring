# Multi-Language Support Summary

## What Has Been Created

### ? Documentation
1. **`docs/internationalization-guide.md`** - Complete 13-step implementation guide
2. **`docs/localization-quickstart.md`** - Quick start guide with practical steps

### ? Code Files
1. **`Services/LocalizationManager.cs`** - Service to manage language switching

## What You Need to Do

### Step 1: Create Resource Files (.resx)

In Visual Studio:
1. Right-click on `iDoublePress` project
2. Add > New Folder > Name it: `Resources/Strings`
3. Right-click on `Strings` folder
4. Add > New Item > Search for "Resources File"
5. Name it: `AppResources.resx`

This creates the default English resource file.

### Step 2: Add String Resources

In the AppResources.resx file, add:

| Name | Value |
|------|-------|
| Resume | Resume |
| StartNew | Start New |
| CompleteRound | Complete Round |
| Abandon | Abandon |
| ResumeRoundTitle | Resume Round? |
| Cancel | Cancel |
| OK | OK |

(See full list in docs/internationalization-guide.md)

### Step 3: Create Additional Language Files

For each language:
1. Copy `AppResources.resx`
2. Rename to `AppResources.es.resx` (Spanish)
3. Translate the values (not the names!)
4. Repeat for other languages

### Step 4: Update .csproj

Add to `iDoublePress.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Update="Resources\Strings\AppResources.resx">
    <Generator>ResXFileCodeGenerator</Generator>
    <LastGenOutput>AppResources.Designer.cs</LastGenOutput>
    <CustomToolNamespace>iDoublePress.Resources.Strings</CustomToolNamespace>
  </EmbeddedResource>
</ItemGroup>
```

### Step 5: Update Code Files

**MainPageModel.cs** - Replace:
```csharp
"Resume Round?" ? AppResources.ResumeRoundTitle
"Resume" ? AppResources.Resume
"Start New" ? AppResources.StartNew
```

**ActiveRoundPageModel.cs** - Replace:
```csharp
"Complete Round?" ? AppResources.CompleteRoundTitle
"Abandon" ? AppResources.Abandon
```

### Step 6: Create XAML Extension

Create `Extensions/TranslateExtension.cs`:
```csharp
using System.Globalization;

namespace iDoublePress.Extensions;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension
{
    public string? Key { get; set; }

    public object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (Key == null)
            return null;

        var rm = Resources.Strings.AppResources.ResourceManager;
        return rm.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;
    }
}
```

### Step 7: Update XAML Files

In `ActiveRoundPage.xaml`:
```xml
xmlns:ext="clr-namespace:iDoublePress.Extensions"

<Button Text="{ext:Translate Abandon}" />
<Button Text="{ext:Translate CompleteRound}" />
```

### Step 8: Initialize on App Start

Update `App.xaml.cs`:
```csharp
public App()
{
    InitializeComponent();
    LocalizationManager.Instance.LoadSavedLanguage();
    MainPage = new AppShell();
}
```

## Languages to Support (Priority Order)

Based on global golf participation:

1. **English** (Primary market)
2. **Spanish** - Spain, Mexico, Latin America
3. **Japanese** - 2nd largest golf market
4. **Korean** - Very active golf community
5. **French** - France, Canada, Africa
6. **German** - Central Europe
7. **Chinese (Simplified)** - Growing market
8. **Italian** - Southern Europe
9. **Portuguese** - Brazil, Portugal
10. **Swedish** - Nordic region

## Estimated Effort

- **Setup (Steps 1-4)**: 2-3 hours
- **Code updates (Steps 5-7)**: 4-6 hours
- **Testing per language**: 1 hour
- **Professional translation** (per language): 1-2 days waiting time
- **Total time**: 1-2 days development + translation time

## Translation Options

### Option 1: AI Translation (Fastest)
- Use ChatGPT/Claude to translate AppResources
- Cost: Free
- Quality: 80-90%
- Time: 1 hour

### Option 2: Professional Translation
- Services like OneSky, Crowdin, or freelance translators
- Cost: $50-200 per language
- Quality: 95-99%
- Time: 2-5 days

### Option 3: Hybrid
- AI for initial translation
- Native speaker review for golf-specific terms
- Cost: $20-50 per language
- Quality: 90-95%
- Time: 1-2 days

## Golf-Specific Terms to Consider

These require special attention in translation:

- **Birdie, Eagle, Bogey** - Keep English terms or translate?
- **Green in Regulation (GIR)** - Abbreviations differ by language
- **Fairway Hit** - Technical golf terms
- **Handicap** - Some languages use English term
- **Par** - Universal, usually keep as "Par"

## Testing Strategy

1. **Build with en-US** - Verify everything works
2. **Add es-ES** - Test one language thoroughly
3. **Test on device** - Change device language
4. **Verify layouts** - Ensure UI doesn't break with longer text
5. **Add remaining languages** - Once confident

## Maintenance

After initial setup:
- New features: Add strings to AppResources.resx first
- Auto-translate: Use tools or AI
- Review: Have translations reviewed periodically
- Version control: Track .resx files in Git

## Common Issues

1. **Missing translations** - Falls back to English (by design)
2. **Text too long** - Test with German (longest language)
3. **Date/Number formats** - Use CultureInfo formatting
4. **Plurals** - Some languages have complex plural rules

## App Store Considerations

After localization:
- Update app store descriptions in each language
- Provide localized screenshots
- Translate app keywords for SEO
- Consider localized app icons (optional)

## Success Metrics

Track by language:
- Download counts
- User engagement
- Retention rates
- App store ratings

This will help prioritize which languages to focus on.
