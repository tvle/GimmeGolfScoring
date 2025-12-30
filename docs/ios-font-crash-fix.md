# iOS Startup Crash Fix: GSFont invalid font file (FluentUI.cs)

If iOS logs show:

- `GSFont: invalid font file - ".../iDoublePress.app/FluentUI.cs"`

then the app bundle is incorrectly registering a **C# source file** (`FluentUI.cs`) as a font.

## Root cause

`FluentUI.cs` is being included in the app’s **UIAppFonts** list (directly in `Platforms/iOS/Info.plist` or indirectly via MAUI font build items).

iOS attempts to load everything in `UIAppFonts` as a font at startup; when it encounters a non-font file, startup initialization can fail later in UI construction.

## Fix checklist

1. Remove `FluentUI.cs` from `UIAppFonts` in `Platforms/iOS/Info.plist`.
2. Ensure your `.csproj` only registers real fonts:

```xml
<ItemGroup>
  <MauiFont Include="Resources\\Fonts\\*.ttf" />
  <MauiFont Include="Resources\\Fonts\\*.otf" />
</ItemGroup>
```

3. If you previously used a broad wildcard (like `Resources\**\*.*`) for fonts, constrain it.
4. Clean + delete the app from the simulator/device + rebuild.
