# Contributing to Golf Scoring App

## Development Guidelines

### Coding Standards

This project follows the existing .NET MAUI patterns established in the codebase:

#### MVVM Pattern
- Use `CommunityToolkit.Mvvm` for ViewModels
- Inherit from `ObservableObject`
- Use `[RelayCommand]` attributes for command bindings
- Keep business logic in ViewModels, not code-behind

#### Naming Conventions
- ViewModels: `{Page}Model` (e.g., `MainPageModel`)
- Pages: `{Feature}Page` (e.g., `RoundDetailPage`)
- Controls: `{Purpose}Control` or `{Purpose}View` (e.g., `ScoreCardControl`)

#### UI/UX Standards
- Follow Fluent Design System typography styles (defined in `AppStyles.xaml`)
- Use `OnIdiom` for responsive sizing across devices
- Leverage Syncfusion controls for consistent UI
- Support both light and dark themes using `AppThemeBinding`
- Ensure accessibility with `SemanticProperties`

#### Data Access
- Use repository pattern for data operations
- Implement async/await for all database operations
- Handle errors through `ModalErrorHandler`
- Support offline-first architecture with SQLite

### Git Workflow

1. **Branch Naming**
   - Feature: `feature/{feature-name}`
   - Bug fix: `bugfix/{issue-description}`
   - Documentation: `docs/{topic}`

2. **Commit Messages**
   - Use clear, descriptive messages
   - Reference work items when applicable
   - Format: `[Component] Brief description`

3. **Pull Requests**
   - Link to related documentation in `/docs`
   - Update relevant markdown files
   - Ensure builds succeed on all platforms
   - Add unit tests for new features

### Development Phases

Follow the phased approach outlined in `/docs/development-plan.md`:
1. **Phase 1**: Core scoring functionality (MVP)
2. **Phase 2**: Enhanced scoring experience
3. **Phase 3**: Historical data & insights
4. **Phase 4**: Social & advanced features

### Testing Requirements

- Test on multiple platforms (iOS, Android, Windows)
- Verify offline functionality
- Test with various screen sizes
- Validate accessibility features
- Performance test with large datasets

### Documentation Standards

- Update `/docs` markdown files when adding features
- Include code examples in documentation
- Add diagrams for complex flows (use Mermaid syntax)
- Keep README.md current with project status
- Document breaking changes clearly

## Questions?

For questions about architecture or design decisions, refer to `/docs/architecture.md` or create a work item for discussion.
