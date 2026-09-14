using Microsoft.Data.Sqlite;

namespace iDoublePress.Tests;

/// <summary>
/// Regression tests that verify SQLitePCLRaw.bundle_e_sqlite3 3.0.5 (SQLite 3.53.4)
/// supports the CRUD operations used by PlayerRepository, CourseRepository, and
/// RoundRepository.  These tests exercise an in-memory database so they run without
/// any MAUI runtime.
///
/// NOTE: The test project does not include a ProjectReference to iDoublePress.csproj
/// because that project targets MAUI platforms (net10.0-android, net10.0-ios, etc.)
/// which are not available in this CI/test environment.  The schema reproduced in
/// CreateSchema() below must be kept in sync with the migration scripts in
/// iDoublePress/Data/ whenever production schema changes are made.
/// </summary>
public sealed class SqliteRegressionTests : IDisposable
{
    private readonly SqliteConnection _db;

    public SqliteRegressionTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _db = new SqliteConnection("Data Source=:memory:");
        _db.Open();
        EnableForeignKeys();
        CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────

    private void EnableForeignKeys()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    private void CreateSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Player (
                ID        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT    NOT NULL,
                Handicap  REAL    DEFAULT 0,
                Email     TEXT,
                CreatedAt TEXT    NOT NULL,
                UpdatedAt TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Course (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT    NOT NULL,
                City        TEXT,
                State       TEXT,
                Country     TEXT,
                Holes       INTEGER NOT NULL DEFAULT 18,
                Par         INTEGER NOT NULL DEFAULT 72,
                Rating      REAL    DEFAULT 0,
                Slope       REAL    DEFAULT 0,
                CreatedAt   TEXT    NOT NULL,
                UpdatedAt   TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CourseHole (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                CourseID    INTEGER NOT NULL REFERENCES Course(ID) ON DELETE CASCADE,
                HoleNumber  INTEGER NOT NULL,
                Par         INTEGER NOT NULL,
                Yardage     INTEGER DEFAULT 0,
                Handicap    INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Round (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayerID    INTEGER NOT NULL REFERENCES Player(ID),
                CourseID    INTEGER NOT NULL REFERENCES Course(ID),
                StartedAt   TEXT    NOT NULL,
                FinishedAt  TEXT,
                TotalScore  INTEGER,
                Notes       TEXT,
                CreatedAt   TEXT    NOT NULL,
                UpdatedAt   TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS RoundHole (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundID     INTEGER NOT NULL REFERENCES Round(ID) ON DELETE CASCADE,
                HoleNumber  INTEGER NOT NULL,
                Score       INTEGER NOT NULL,
                Putts       INTEGER DEFAULT 0,
                FairwayHit  INTEGER DEFAULT 0,
                GIR         INTEGER DEFAULT 0,
                Notes       TEXT,
                CreatedAt   TEXT    NOT NULL,
                UpdatedAt   TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ShotSegment (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundHoleID INTEGER NOT NULL REFERENCES RoundHole(ID) ON DELETE CASCADE,
                Club        TEXT,
                Distance    REAL    DEFAULT 0,
                Latitude    REAL,
                Longitude   REAL,
                CreatedAt   TEXT    NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    private long InsertPlayer(string name, double handicap = 0.0)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Player (Name, Handicap, CreatedAt, UpdatedAt)
            VALUES ($name, $hcp, $ca, $ua);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$hcp", handicap);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertCourse(string name)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Course (Name, Holes, Par, CreatedAt, UpdatedAt)
            VALUES ($name, 18, 72, $ca, $ua);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertCourseHole(long courseId, int holeNumber, int par)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO CourseHole (CourseID, HoleNumber, Par)
            VALUES ($cid, $hn, $par);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$cid", courseId);
        cmd.Parameters.AddWithValue("$hn", holeNumber);
        cmd.Parameters.AddWithValue("$par", par);
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertRound(long playerId, long courseId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Round (PlayerID, CourseID, StartedAt, CreatedAt, UpdatedAt)
            VALUES ($pid, $cid, $sa, $ca, $ua);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$pid", playerId);
        cmd.Parameters.AddWithValue("$cid", courseId);
        cmd.Parameters.AddWithValue("$sa", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertRoundHole(long roundId, int holeNumber, int score)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO RoundHole (RoundID, HoleNumber, Score, CreatedAt, UpdatedAt)
            VALUES ($rid, $hn, $score, $ca, $ua);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$rid", roundId);
        cmd.Parameters.AddWithValue("$hn", holeNumber);
        cmd.Parameters.AddWithValue("$score", score);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertShotSegment(long roundHoleId, string club, double distance)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ShotSegment (RoundHoleID, Club, Distance, CreatedAt)
            VALUES ($rhid, $club, $dist, $ca);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$rhid", roundHoleId);
        cmd.Parameters.AddWithValue("$club", club);
        cmd.Parameters.AddWithValue("$dist", distance);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    // ── sqlite version ───────────────────────────────────────────────────

    [Fact]
    public void SqliteVersion_IsAtLeast3_50_2()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT sqlite_version();";
        var version = (string)cmd.ExecuteScalar()!;

        var parts = version.Split('.').Select(int.Parse).ToArray();
        var major = parts.Length > 0 ? parts[0] : 0;
        var minor = parts.Length > 1 ? parts[1] : 0;
        var patch = parts.Length > 2 ? parts[2] : 0;

        (major > 3
            || (major == 3 && minor > 50)
            || (major == 3 && minor == 50 && patch >= 2))
            .Should().BeTrue($"SQLite {version} does not meet the minimum 3.50.2 requirement");
    }

    // ── player ───────────────────────────────────────────────────────────

    [Fact]
    public void Player_Create_ReturnsId()
    {
        var id = InsertPlayer("Alice", 5.2);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Player_Load_ReturnsCorrectFields()
    {
        InsertPlayer("Bob", 12.0);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Name, Handicap FROM Player WHERE Name = $name;";
        cmd.Parameters.AddWithValue("$name", "Bob");
        using var reader = cmd.ExecuteReader();

        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Bob");
        reader.GetDouble(1).Should().Be(12.0);
    }

    [Fact]
    public void Player_Update_PersistsChange()
    {
        var id = InsertPlayer("Carol", 0.0);

        using var upd = _db.CreateCommand();
        upd.CommandText = "UPDATE Player SET Handicap = $h, UpdatedAt = $ua WHERE ID = $id;";
        upd.Parameters.AddWithValue("$h", 3.5);
        upd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        upd.Parameters.AddWithValue("$id", id);
        upd.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT Handicap FROM Player WHERE ID = $id;";
        sel.Parameters.AddWithValue("$id", id);
        ((double)sel.ExecuteScalar()!).Should().Be(3.5);
    }

    [Fact]
    public void Player_Delete_RemovesRow()
    {
        var id = InsertPlayer("Dave");

        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM Player WHERE ID = $id;";
        del.Parameters.AddWithValue("$id", id);
        del.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM Player WHERE ID = $id;";
        sel.Parameters.AddWithValue("$id", id);
        ((long)sel.ExecuteScalar()!).Should().Be(0);
    }

    // ── course + course holes ────────────────────────────────────────────

    [Fact]
    public void Course_Create_WithHoles_ReturnsIds()
    {
        var courseId = InsertCourse("Pebble Beach");
        for (int i = 1; i <= 18; i++)
            InsertCourseHole(courseId, i, i <= 4 ? 5 : (i <= 14 ? 4 : 3));

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM CourseHole WHERE CourseID = $cid;";
        cmd.Parameters.AddWithValue("$cid", courseId);
        ((long)cmd.ExecuteScalar()!).Should().Be(18);
    }

    [Fact]
    public void Course_Delete_CascadesToCourseHoles()
    {
        var courseId = InsertCourse("Augusta");
        InsertCourseHole(courseId, 1, 4);
        InsertCourseHole(courseId, 2, 5);

        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM Course WHERE ID = $id;";
        del.Parameters.AddWithValue("$id", courseId);
        del.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM CourseHole WHERE CourseID = $id;";
        sel.Parameters.AddWithValue("$id", courseId);
        ((long)sel.ExecuteScalar()!).Should().Be(0);
    }

    // ── round + round holes ──────────────────────────────────────────────

    [Fact]
    public void Round_Create_WithHoles_PersistsCorrectly()
    {
        var playerId = InsertPlayer("Eve");
        var courseId = InsertCourse("St Andrews");
        var roundId = InsertRound(playerId, courseId);

        InsertRoundHole(roundId, 1, 4);
        InsertRoundHole(roundId, 2, 5);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM RoundHole WHERE RoundID = $rid;";
        cmd.Parameters.AddWithValue("$rid", roundId);
        ((long)cmd.ExecuteScalar()!).Should().Be(2);
    }

    [Fact]
    public void Round_Delete_CascadesToRoundHoles()
    {
        var playerId = InsertPlayer("Frank");
        var courseId = InsertCourse("Torrey Pines");
        var roundId = InsertRound(playerId, courseId);
        InsertRoundHole(roundId, 1, 3);

        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM Round WHERE ID = $id;";
        del.Parameters.AddWithValue("$id", roundId);
        del.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM RoundHole WHERE RoundID = $id;";
        sel.Parameters.AddWithValue("$id", roundId);
        ((long)sel.ExecuteScalar()!).Should().Be(0);
    }

    // ── shot segments ────────────────────────────────────────────────────

    [Fact]
    public void ShotSegment_Create_PersistsCorrectly()
    {
        var playerId = InsertPlayer("Grace");
        var courseId = InsertCourse("Bethpage Black");
        var roundId = InsertRound(playerId, courseId);
        var holeId = InsertRoundHole(roundId, 1, 4);
        var segId = InsertShotSegment(holeId, "Driver", 280.5);

        segId.Should().BeGreaterThan(0);

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT Club, Distance FROM ShotSegment WHERE ID = $id;";
        sel.Parameters.AddWithValue("$id", segId);
        using var reader = sel.ExecuteReader();
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("Driver");
        reader.GetDouble(1).Should().BeApproximately(280.5, 0.01);
    }

    [Fact]
    public void ShotSegment_Delete_RemovesRow()
    {
        var playerId = InsertPlayer("Hank");
        var courseId = InsertCourse("Muirfield");
        var roundId = InsertRound(playerId, courseId);
        var holeId = InsertRoundHole(roundId, 1, 5);
        var segId = InsertShotSegment(holeId, "5-iron", 180.0);

        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM ShotSegment WHERE ID = $id;";
        del.Parameters.AddWithValue("$id", segId);
        del.ExecuteNonQuery();

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM ShotSegment WHERE ID = $id;";
        sel.Parameters.AddWithValue("$id", segId);
        ((long)sel.ExecuteScalar()!).Should().Be(0);
    }

    // ── foreign-key enforcement ──────────────────────────────────────────

    [Fact]
    public void ForeignKey_ViolationOnRoundHole_ThrowsSqliteException()
    {
        Action act = () =>
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO RoundHole (RoundID, HoleNumber, Score, CreatedAt, UpdatedAt)
                VALUES (999999, 1, 4, $ca, $ua);";
            cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        };

        act.Should().Throw<SqliteException>()
            .WithMessage("*FOREIGN KEY*");
    }

    // ── transactional aggregate write ────────────────────────────────────

    [Fact]
    public void Transaction_RollbackOnError_LeavesNoPartialData()
    {
        var courseId = InsertCourse("Royal Troon");
        using var txn = _db.BeginTransaction();

        try
        {
            // Insert a valid CourseHole inside the transaction.
            using var cmd = _db.CreateCommand();
            cmd.Transaction = txn;
            cmd.CommandText = @"
                INSERT INTO CourseHole (CourseID, HoleNumber, Par)
                VALUES ($cid, 1, 4);";
            cmd.Parameters.AddWithValue("$cid", courseId);
            cmd.ExecuteNonQuery();

            // Reference a non-existent CourseID to trigger a FK violation and force a rollback.
            // PRAGMA foreign_keys = ON was enabled in the constructor on the same connection,
            // so it applies to all commands on this connection regardless of transaction context.
            using var badCmd = _db.CreateCommand();
            badCmd.Transaction = txn;
            badCmd.CommandText = @"
                INSERT INTO CourseHole (CourseID, HoleNumber, Par)
                VALUES (99999, 2, 4);";
            badCmd.ExecuteNonQuery();

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
        }

        using var sel = _db.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM CourseHole WHERE CourseID = $id;";
        sel.Parameters.AddWithValue("$id", courseId);
        ((long)sel.ExecuteScalar()!).Should().Be(0, "transaction rollback must not leave partial rows");
    }
}
