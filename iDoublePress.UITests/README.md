# iDoublePress.UITests

This project contains Appium-based UI tests.

## Prereqs
- Appium server
- Platform tooling
  - Android: Android SDK + emulator
  - iOS: Xcode + iOS Simulator (macOS only)
  - Windows: WinAppDriver alternative via Appium Windows driver

## Running
Configure environment variables used by the tests:
- `APPIUM_SERVER_URL` (default: `http://127.0.0.1:4723/`)
- `PLATFORM_NAME` (`Android` / `iOS` / `Windows`)
- `APP_PATH` (path to built app package: `.apk` / `.app` / `.msix`/`.exe` depending on platform)

Then run:

```pwsh
dotnet test .\iDoublePress.UITests\iDoublePress.UITests.csproj
```
