# Golf Scoring App

A .NET MAUI cross-platform golf scoring application focused on ease of use and high reliability.

## Vision

This app will set the standard for golf scoring applications by making it as easy as possible to track your golf round. The focus is on:
- **Simplicity**: Score a hole in seconds
- **Reliability**: Never lose your data
- **Insights**: Understand your game better

## Current Status

?? **Planning Phase** - See `/docs/development-plan.md` for detailed roadmap

## Documentation

- [Development Plan & Roadmap](docs/development-plan.md) - Phased development approach
- [Architecture Overview](docs/architecture.md) - Technical design and structure
- [Database Schema](docs/database-schema.md) - Data model and storage design
- [Feature Specifications](docs/features/) - Detailed feature requirements by phase

## Technology Stack

- .NET 10
- .NET MAUI (iOS, Android, Windows, macOS)
- SQLite for local data storage
- Syncfusion MAUI Toolkit
- CommunityToolkit.Mvvm

## Getting Started

### Prerequisites
- Visual Studio 2022 or later
- .NET 10 SDK

### Building the Solution
1. Clone the repository from Azure DevOps
2. Open `iDoublePress.sln` in Visual Studio
3. Restore NuGet packages
4. Select your target platform
5. Build and run

Debug builds do not require release signing material.
Release Android keystores, Java keystores, Apple certificates, and provisioning profiles must be supplied securely outside the repository.
For example, provide them through untracked local build configuration or CI secrets. Do not commit them to source control.

## Project Structure

```
iDoublePress/
??? docs/                    # Project documentation
?   ??? development-plan.md  # Phased roadmap
?   ??? architecture.md      # Technical architecture
?   ??? database-schema.md   # Database design
?   ??? features/            # Feature specifications
??? Models/                  # Data models
??? PageModels/             # ViewModels (MVVM)
??? Pages/                  # Views and UI
?   ??? Controls/           # Reusable UI components
??? Repositories/           # Data access layer
??? Resources/              # Images, fonts, styles
??? Services/               # Business logic
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines and coding standards.

## License

[License information to be added]
