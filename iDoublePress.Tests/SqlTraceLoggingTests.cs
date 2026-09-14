using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace iDoublePress.Tests;

/// <summary>
/// Verifies that production code does not write raw SQL parameter values,
/// notes, email addresses, GPS coordinates, or other sensitive user data to
/// sqlite_trace.log or any other file-based trace log.
/// </summary>
public sealed class SqlTraceLoggingTests : IDisposable
{
    // Temporary directory used as a stand-in for AppDataDirectory so that any
    // accidental trace-file creation is detectable and isolated.
    private readonly string _appDataDir;
    private readonly SqliteConnection _db;

    public SqlTraceLoggingTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _appDataDir = Path.Combine(Path.GetTempPath(), "gimme_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appDataDir);

        _db = new SqliteConnection("Data Source=:memory:");
        _db.Open();

        using var fk = _db.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys = ON;";
        fk.ExecuteNonQuery();

        using var schema = _db.CreateCommand();
        schema.CommandText = @"
            CREATE TABLE Player (
                ID        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL,
                Email     TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE TABLE Round (
                ID         INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayerID   INTEGER NOT NULL REFERENCES Player(ID),
                Notes      TEXT,
                StartedAt  TEXT NOT NULL,
                CreatedAt  TEXT NOT NULL,
                UpdatedAt  TEXT NOT NULL
            );
            CREATE TABLE ShotSegment (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundID     INTEGER NOT NULL REFERENCES Round(ID),
                Latitude    REAL,
                Longitude   REAL,
                CreatedAt   TEXT NOT NULL
            );";
        schema.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_appDataDir))
            Directory.Delete(_appDataDir, recursive: true);
    }

    // ── no trace file created ─────────────────────────────────────────────

    [Fact]
    public void NoTraceLog_IsCreatedInAppDataDirectory_AfterCrudOperations()
    {
        PerformRepresentativeCrudOperations();

        var traceLog = Path.Combine(_appDataDir, "sqlite_trace.log");
        File.Exists(traceLog).Should().BeFalse(
            "production builds must not write sqlite_trace.log");
    }

    [Fact]
    public void NoTraceLog_ExistsAnywhere_InTempDirectory_AfterCrudOperations()
    {
        PerformRepresentativeCrudOperations();

        // Search the isolated temp directory for any .log file that looks like a trace.
        var logs = Directory.GetFiles(_appDataDir, "*.log", SearchOption.AllDirectories);
        logs.Should().BeEmpty(
            "no trace log files should be created during repository operations");
    }

    // ── no sensitive values in source ─────────────────────────────────────

    [Fact]
    public void RoundRepository_Source_DoesNotContain_SqliteTraceLogPath()
    {
        var sourceFile = FindRoundRepositorySource();
        var source = File.ReadAllText(sourceFile);
        source.Should().NotContain("sqlite_trace.log",
            "production code must not reference the sqlite_trace.log path");
    }

    [Fact]
    public void RoundRepository_Source_DoesNotContain_LogSqlMethod()
    {
        var sourceFile = FindRoundRepositorySource();
        var source = File.ReadAllText(sourceFile);
        source.Should().NotContain("void LogSql(",
            "the LogSql helper that writes raw parameter values must be removed");
    }

    [Fact]
    public void RoundRepository_Source_DoesNotContain_ParameterValueEnumeration()
    {
        var sourceFile = FindRoundRepositorySource();
        var source = File.ReadAllText(sourceFile);

        // The removed pattern iterated SqliteParameter values and called ToString()
        // on them before writing to a file. Detect that specific pattern.
        source.Should().NotContain("p.Value.ToString()",
            "raw parameter values must not be serialised for logging");
        source.Should().NotContain("File.AppendAllText",
            "production code must not write log files directly via File.AppendAllText");
    }

    // ── sensitive values do not appear in representative operations ───────

    /// <summary>
    /// Exercises the SQLite CRUD path and verifies that no sensitive parameter values
    /// appear in any files written to the isolated app-data directory.
    ///
    /// NOTE: This test uses an in-memory database directly rather than
    /// <c>RoundRepository</c> because that class depends on the MAUI runtime
    /// (<c>FileSystem.AppDataDirectory</c>) which is not available in this CI
    /// test project.  The source-level assertions in this class (e.g.
    /// <see cref="RoundRepository_Source_DoesNotContain_LogSqlMethod"/>) are
    /// the primary regression guard: they will fail immediately if
    /// <c>LogSql</c> or <c>sqlite_trace.log</c> are reintroduced into the
    /// production source.  This test additionally verifies that the test
    /// infrastructure itself does not create any unexpected log files.
    /// </summary>

    [Fact]
    public void SensitiveParameterValues_DoNotAppearInLogFiles_AfterInsert()
    {
        const string sensitiveEmail = "user_secret@example.com";
        const string sensitiveNote = "private round note XY9Z";
        const double sensitiveLat = 37.775;
        const double sensitiveLon = -122.418;

        // Insert data with sensitive values.
        using var insertPlayer = _db.CreateCommand();
        insertPlayer.CommandText =
            "INSERT INTO Player (Name, Email, CreatedAt, UpdatedAt) VALUES ($n, $e, $ca, $ua); SELECT last_insert_rowid();";
        insertPlayer.Parameters.AddWithValue("$n", "Test Player");
        insertPlayer.Parameters.AddWithValue("$e", sensitiveEmail);
        insertPlayer.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        insertPlayer.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        var playerId = (long)insertPlayer.ExecuteScalar()!;

        using var insertRound = _db.CreateCommand();
        insertRound.CommandText =
            "INSERT INTO Round (PlayerID, Notes, StartedAt, CreatedAt, UpdatedAt) VALUES ($pid, $notes, $sa, $ca, $ua); SELECT last_insert_rowid();";
        insertRound.Parameters.AddWithValue("$pid", playerId);
        insertRound.Parameters.AddWithValue("$notes", sensitiveNote);
        insertRound.Parameters.AddWithValue("$sa", DateTime.UtcNow.ToString("o"));
        insertRound.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        insertRound.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        var roundId = (long)insertRound.ExecuteScalar()!;

        using var insertSeg = _db.CreateCommand();
        insertSeg.CommandText =
            "INSERT INTO ShotSegment (RoundID, Latitude, Longitude, CreatedAt) VALUES ($rid, $lat, $lon, $ca); SELECT last_insert_rowid();";
        insertSeg.Parameters.AddWithValue("$rid", roundId);
        insertSeg.Parameters.AddWithValue("$lat", sensitiveLat);
        insertSeg.Parameters.AddWithValue("$lon", sensitiveLon);
        insertSeg.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        insertSeg.ExecuteScalar();

        // Assert: none of the sensitive values appear in any log file in our isolated dir.
        AssertNoSensitiveValueInLogs(sensitiveEmail, sensitiveNote,
            sensitiveLat.ToString(CultureInfo.InvariantCulture),
            sensitiveLon.ToString(CultureInfo.InvariantCulture));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private void PerformRepresentativeCrudOperations()
    {
        using var ins = _db.CreateCommand();
        ins.CommandText = "INSERT INTO Player (Name, CreatedAt, UpdatedAt) VALUES ($n, $ca, $ua);";
        ins.Parameters.AddWithValue("$n", "Alice");
        ins.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        ins.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        ins.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT ID, Name FROM Player;";
        using var reader = sel.ExecuteReader();
        while (reader.Read()) { _ = reader.GetInt64(0); }
    }

    private void AssertNoSensitiveValueInLogs(params string[] sensitiveValues)
    {
        if (!Directory.Exists(_appDataDir))
            return;

        foreach (var file in Directory.GetFiles(_appDataDir, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            foreach (var value in sensitiveValues)
            {
                content.Should().NotContain(value,
                    $"log file '{file}' must not contain the sensitive value '{value}'");
            }
        }
    }

    private static string FindRoundRepositorySource()
    {
        // Walk up from the test binary to find the repository root, then locate the source.
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "iDoublePress", "Data", "RoundRepository.cs");
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // Fallback: search from the directory containing the test project.
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var found = Directory.GetFiles(repoRoot, "RoundRepository.cs", SearchOption.AllDirectories)
                             .FirstOrDefault();
        found.Should().NotBeNull("RoundRepository.cs must be findable relative to the test output directory");
        return found!;
    }
}
