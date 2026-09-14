using iDoublePress.Data;
using Microsoft.Data.Sqlite;

namespace iDoublePress.Tests;

public sealed class MigrationTests : IDisposable
{
    private readonly SqliteConnection _db;

    public MigrationTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _db = new SqliteConnection("Data Source=:memory:");
        _db.Open();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RepresentativePreMigrationDatabase_UpgradesWithoutDataLoss_AndIsIdempotent()
    {
        await CreateRepresentativePreMigrationSchemaAsync();
        await SeedRepresentativePreMigrationDataAsync();

        await DatabaseMigrator.MigrateAsync(_db);
        await DatabaseMigrator.MigrateAsync(_db);

        (await GetMigrationVersionsAsync()).Should().Equal(1, 2);

        await AssertColumnExistsAsync("Player", "PublicId");
        await AssertColumnExistsAsync("Course", "PublicId");
        await AssertColumnExistsAsync("CourseHole", "PublicId");
        await AssertColumnExistsAsync("Round", "PublicId");
        await AssertColumnExistsAsync("Hole", "PublicId");
        await AssertColumnExistsAsync("ShotSegment", "PublicId");
        await AssertColumnExistsAsync("ShotSegment", "PendingSync");

        await AssertSingleRowSyncMetadataAsync("Player", "Alice");
        await AssertSingleRowSyncMetadataAsync("Course", "North Nine");
        await AssertSingleRowSyncMetadataAsync("Round", "Practice round");

        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM CourseHole WHERE HoleNumber IN (1, 2);")).Should().Be(2);
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Hole WHERE HoleNumber IN (1, 2);")).Should().Be(2);
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ShotSegment WHERE Sequence IN (0, 1);")).Should().Be(2);
        (await ExecuteScalarAsync<string>("SELECT 'table' FROM sqlite_master WHERE name = 'SyncOutbox' AND type = 'table' LIMIT 1;")).Should().Be("table");
    }

    [Fact]
    public async Task FreshDatabase_GetsLatestSchema_WithOutboxSupport()
    {
        await DatabaseMigrator.MigrateAsync(_db);

        (await GetMigrationVersionsAsync()).Should().Equal(1, 2);
        await AssertColumnExistsAsync("Player", "SyncUpdatedAtUtc");
        await AssertColumnExistsAsync("Hole", "DeletedAtUtc");
        await AssertColumnExistsAsync("ShotSegment", "ServerRevision");
        await AssertColumnExistsAsync("CourseHole", "PendingSync");
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM SyncOutbox;")).Should().Be(0);
    }

    private async Task CreateRepresentativePreMigrationSchemaAsync()
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Player (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Handicap REAL DEFAULT 0,
                Email TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE Course (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Location TEXT,
                TotalPar INTEGER NOT NULL,
                Holes INTEGER DEFAULT 18,
                Rating REAL,
                Slope INTEGER,
                IsCustom INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE CourseHole (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                CourseID INTEGER NOT NULL,
                HoleNumber INTEGER NOT NULL,
                Par INTEGER NOT NULL,
                Handicap INTEGER,
                Yardage INTEGER,
                FOREIGN KEY (CourseID) REFERENCES Course(ID) ON DELETE CASCADE,
                UNIQUE(CourseID, HoleNumber)
            );

            CREATE TABLE Round (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayerID INTEGER NOT NULL,
                CourseID INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                TotalScore INTEGER DEFAULT 0,
                Status TEXT DEFAULT 'InProgress',
                Notes TEXT,
                Weather TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (PlayerID) REFERENCES Player(ID) ON DELETE CASCADE,
                FOREIGN KEY (CourseID) REFERENCES Course(ID) ON DELETE RESTRICT
            );

            CREATE TABLE Hole (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundID INTEGER NOT NULL,
                HoleNumber INTEGER NOT NULL,
                Par INTEGER NOT NULL,
                Yardage INTEGER,
                Score INTEGER DEFAULT 0,
                IsScored INTEGER DEFAULT 0,
                Putts INTEGER,
                FairwayHit INTEGER,
                FairwayResult INTEGER DEFAULT 0,
                FairwayMissPenalty INTEGER DEFAULT 0,
                GreenInRegulation INTEGER,
                Penalties INTEGER DEFAULT 0,
                Proximity TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (RoundID) REFERENCES Round(ID) ON DELETE CASCADE,
                UNIQUE(RoundID, HoleNumber)
            );

            CREATE TABLE ShotSegment (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                HoleID INTEGER NOT NULL,
                Sequence INTEGER NOT NULL,
                Latitude REAL NOT NULL,
                Longitude REAL NOT NULL,
                Tag TEXT,
                CreatedAt TEXT NOT NULL,
                Accuracy REAL,
                FOREIGN KEY (HoleID) REFERENCES Hole(ID) ON DELETE CASCADE
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedRepresentativePreMigrationDataAsync()
    {
        const string created = "2026-09-10T15:00:00";
        const string updated = "2026-09-11T16:30:00";

        var playerId = await InsertAsync(@"
            INSERT INTO Player (Name, Handicap, Email, CreatedAt, UpdatedAt)
            VALUES ('Alice', 8.4, 'alice@example.com', @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();", created, updated);

        var courseId = await InsertAsync(@"
            INSERT INTO Course (Name, Location, TotalPar, Holes, Rating, Slope, IsCustom, CreatedAt)
            VALUES ('North Nine', 'Local', 36, 9, 36.0, 113, 1, @CreatedAt);
            SELECT last_insert_rowid();", created, updated);

        await InsertAsync(@"
            INSERT INTO CourseHole (CourseID, HoleNumber, Par, Handicap, Yardage)
            VALUES (@CourseID, 1, 4, 1, 380);
            SELECT last_insert_rowid();", created, updated, ("@CourseID", courseId));
        await InsertAsync(@"
            INSERT INTO CourseHole (CourseID, HoleNumber, Par, Handicap, Yardage)
            VALUES (@CourseID, 2, 3, 2, 160);
            SELECT last_insert_rowid();", created, updated, ("@CourseID", courseId));

        var roundId = await InsertAsync(@"
            INSERT INTO Round (PlayerID, CourseID, StartTime, TotalScore, Status, Notes, Weather, CreatedAt, UpdatedAt)
            VALUES (@PlayerID, @CourseID, @StartTime, 7, 'Completed', 'Practice round', 'Sunny', @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();", created, updated, ("@PlayerID", playerId), ("@CourseID", courseId), ("@StartTime", created));

        var hole1Id = await InsertAsync(@"
            INSERT INTO Hole (RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts, FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt)
            VALUES (@RoundID, 1, 4, 380, 4, 1, 2, 1, 0, 0, 1, 0, 'S', 'Steady', @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();", created, updated, ("@RoundID", roundId));
        var hole2Id = await InsertAsync(@"
            INSERT INTO Hole (RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts, FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt)
            VALUES (@RoundID, 2, 3, 160, 3, 1, 1, 0, 0, 0, 1, 0, 'M', 'Dialed', @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();", created, updated, ("@RoundID", roundId));

        await InsertAsync(@"
            INSERT INTO ShotSegment (HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy)
            VALUES (@HoleID, 0, 37.111, -122.111, 'Drive', @CreatedAt, 5.0);
            SELECT last_insert_rowid();", created, updated, ("@HoleID", hole1Id));
        await InsertAsync(@"
            INSERT INTO ShotSegment (HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy)
            VALUES (@HoleID, 1, 37.112, -122.112, 'Approach', @CreatedAt, 4.5);
            SELECT last_insert_rowid();", created, updated, ("@HoleID", hole2Id));
    }

    private async Task<long> InsertAsync(string sql, string createdAt, string updatedAt, params (string Name, object Value)[] extraParameters)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        if (sql.Contains("@CreatedAt", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("@CreatedAt", createdAt);
        if (sql.Contains("@UpdatedAt", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("@UpdatedAt", updatedAt);

        foreach (var (name, value) in extraParameters)
            cmd.Parameters.AddWithValue(name, value);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<List<int>> GetMigrationVersionsAsync()
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Version FROM __SchemaMigration ORDER BY Version;";
        var versions = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            versions.Add(reader.GetInt32(0));
        return versions;
    }

    private async Task AssertColumnExistsAsync(string tableName, string columnName)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";

        var found = false;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        found.Should().BeTrue($"{tableName} should contain {columnName} after migration");
    }

    private async Task AssertSingleRowSyncMetadataAsync(string tableName, string expectedMarker)
    {
        var markerColumn = tableName == "Course" ? "Name" : tableName == "Round" ? "Notes" : "Name";
        var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
            SELECT PublicId, SyncUpdatedAtUtc, IsDeleted, PendingSync
            FROM {tableName}
            WHERE {markerColumn} = @Marker";
        cmd.Parameters.AddWithValue("@Marker", expectedMarker);

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        Guid.TryParse(reader.GetString(0), out _).Should().BeTrue();
        DatabaseDateTime.ParseUtc(reader.GetString(1)).Kind.Should().Be(DateTimeKind.Utc);
        reader.GetInt32(2).Should().Be(0);
        reader.GetInt32(3).Should().Be(0);
    }

    private async Task<T> ExecuteScalarAsync<T>(string sql)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        return (T)(await cmd.ExecuteScalarAsync())!;
    }
}
