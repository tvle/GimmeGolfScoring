# GitHub Copilot instructions for GimmeGolfScoring

## Product

Gimme is an offline-first golf scoring and analytics application built with .NET MAUI. It targets Android and iOS from one codebase.

Users must be able to score rounds and access existing local data without signing in or having network access. Signing in is optional and enables cloud backup, synchronization, web access, and other account-based features.

Never make authentication a prerequisite for core local scoring unless an issue explicitly changes that product requirement.

## Project structure

- `iDoublePress/` — .NET MAUI application.
- `iDoublePress/Models/` — local domain and UI models.
- `iDoublePress/Data/` — SQLite repositories and database initialization.
- `iDoublePress/PageModels/` — CommunityToolkit.Mvvm page models.
- `iDoublePress/Pages/` — XAML pages and code-behind.
- `iDoublePress/Platforms/Android/` — Android-specific configuration and callbacks.
- `iDoublePress/Platforms/iOS/` — iOS-specific configuration, entitlements, and privacy declarations.
- `iDoublePress.Tests/` — unit and repository tests.
- `iDoublePress.UITests/` — Appium UI tests.

Services and repositories are registered in `iDoublePress/MauiProgram.cs`. Follow the existing page/page-model/repository separation. Put new cross-platform authentication, API, and synchronization behavior in injected services rather than page code-behind.

## Change discipline

- Implement only the assigned issue. Avoid unrelated redesigns, package upgrades, formatting changes, or warning cleanup.
- Preserve all existing user data. Never replace, delete, or recreate the SQLite database as a migration strategy.
- Any schema change must use an ordered, transactional, repeatable migration.
- Do not change application IDs, signing identities, version numbers, provisioning profiles, or store configuration unless the issue explicitly requests it.
- Never commit keystores, certificates, provisioning profiles, access tokens, provider secrets, API keys, local databases, SQL traces, or environment-specific paths.
- Do not commit generated build output, preprocessed MSBuild files, `bin`, `obj`, or diagnostic logs.
- Do not suppress exceptions merely to make a test pass. Handle expected failures explicitly and preserve actionable diagnostics without exposing sensitive data.

## Local data rules

- SQLite remains the application’s local source of truth. UI operations must continue to work offline.
- Use parameterized SQL.
- Enable `PRAGMA foreign_keys=ON` and the configured busy timeout on every opened SQLite connection.
- Use transactions for aggregate writes, including courses with course holes and rounds with holes or shot segments.
- Use UTC for persisted and synchronized timestamps. Convert to local time only for display.
- Device-local integer keys may remain for local joins, but synchronized entities must also have stable UUID identifiers.
- Synchronization metadata should include a server revision/version, UTC update time, deletion tombstone, and pending-sync state or outbox entry.
- Never silently discard child records when hydrating an aggregate.
- Avoid N+1 database queries on list screens. Do not optimize prematurely, but measure and fix demonstrated query problems.
- Treat migrations, synchronization, and conflict handling as data-loss-sensitive work and add tests for upgrade paths.

## Authentication and API rules

- The app is a public OAuth client and must not contain Apple, Google, or backend client secrets.
- Use a protected external/system authentication session and Authorization Code with PKCE. Never authenticate inside an embedded WebView.
- Provider responses must be validated by the backend. The mobile app consumes only first-party Gimme API tokens.
- Never return access or refresh tokens in a deep-link URL. Return a short-lived, single-use authorization code and exchange it with a PKCE verifier.
- Keep short-lived access tokens in memory.
- Store rotating refresh tokens using .NET MAUI Secure Storage.
- Never log tokens, authorization codes, email addresses, account identifiers, golf notes, or GPS coordinates.
- Handle corrupt or unrestorable Secure Storage values by clearing the invalid session and requiring reauthentication.
- Exclude Android Secure Storage preferences from Android Auto Backup.
- Clear stale iOS Keychain authentication data on a true first launch after reinstall.
- Signing out revokes the current device refresh token and clears local credentials. It must not delete local golf data.
- Account deletion is a separate, confirmed operation and must remove server data while clearly explaining what happens to local data.

## Synchronization rules

- All server records are owned using the authenticated server-side user context. Never trust an owner ID supplied by the client.
- Do not equate an authenticated account with a local `Player`; an account may manage multiple player records.
- Uploads must be idempotent.
- Incremental downloads must use an opaque server cursor.
- Deletions must propagate using tombstones.
- Use optimistic concurrency or revisions. Never silently overwrite conflicting changes to an active round.
- In the initial implementation, synchronize a round, its holes, and its shot segments as one versioned aggregate.
- Synchronization failures must not block local scoring or corrupt acknowledged local data.
- Retry transient failures using bounded exponential backoff.
- GPS coordinates are sensitive. Do not upload them unless the user has been informed and the relevant privacy requirement is satisfied.

## UI, accessibility, and localization

- Keep account and synchronization status understandable without technical language.
- Explain the benefit of signing in before presenting login choices.
- Preserve large touch targets used during active scoring.
- Add `AutomationId` values to new interactive controls.
- Add meaningful semantic descriptions to icon-only controls and important scoring actions.
- Support dynamic text sizing, screen readers, light/dark themes, and adequate contrast.
- Put new user-visible strings in the existing localization resources. Do not hard-code English strings in new UI.

## Validation

Before finishing a pull request:

1. Restore packages and report any vulnerability warnings.
2. Build the Android target:
   `dotnet build iDoublePress/iDoublePress.csproj -f net10.0-android -c Debug`
3. Build the iOS simulator target when a compatible macOS/Xcode environment is available.
4. Run applicable unit and repository tests.
5. Add tests for changed domain, repository, migration, authentication, or synchronization behavior.
6. Verify that no signing files, credentials, databases, or logs were added.
7. Confirm that existing offline scoring behavior remains intact.

If the environment cannot build a target, state the exact limitation in the pull request. Do not claim that a build passed when it was skipped, timed out, or failed because of tooling.

## Pull-request description

Every pull request should include:

- A concise summary of the behavior changed.
- Files or architectural areas affected.
- Tests and builds run, with results.
- Database migration and rollback considerations.
- Security and privacy impact.
- Required manual Apple, Google, Cloudflare, or store-console configuration.
- Known limitations or follow-up issues.

Do not include generated files or unrelated cleanup in the pull request.
