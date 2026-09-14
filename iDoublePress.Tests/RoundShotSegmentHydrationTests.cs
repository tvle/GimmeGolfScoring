using Microsoft.Data.Sqlite;

namespace iDoublePress.Tests;

/// <summary>
/// Verifies that shot segments saved against a round's holes are preserved and
/// correctly re-attached to each <see cref="Hole"/> when the round is re-loaded.
///
/// The schema reproduced here matches the production schema managed by
/// <c>RoundRepository.InitializeAsync</c> and must be kept in sync whenever
/// schema migrations are applied to the production tables.
/// </summary>
public sealed class RoundShotSegmentHydrationTests : IDisposable
{
    private readonly SqliteConnection _db;

    public RoundShotSegmentHydrationTests()
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
                CreatedAt TEXT    NOT NULL,
                UpdatedAt TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Course (
                ID        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT    NOT NULL,
                Holes     INTEGER NOT NULL DEFAULT 18,
                Par       INTEGER NOT NULL DEFAULT 72,
                CreatedAt TEXT    NOT NULL,
                UpdatedAt TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Round (
                ID         INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayerID   INTEGER NOT NULL REFERENCES Player(ID),
                CourseID   INTEGER NOT NULL REFERENCES Course(ID),
                StartedAt  TEXT    NOT NULL,
                CreatedAt  TEXT    NOT NULL,
                UpdatedAt  TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Hole (
                ID                 INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundID            INTEGER NOT NULL REFERENCES Round(ID) ON DELETE CASCADE,
                HoleNumber         INTEGER NOT NULL,
                Par                INTEGER NOT NULL,
                Yardage            INTEGER,
                Score              INTEGER DEFAULT 0,
                IsScored           INTEGER DEFAULT 0,
                Putts              INTEGER,
                FairwayHit         INTEGER,
                FairwayResult      INTEGER DEFAULT 0,
                FairwayMissPenalty INTEGER DEFAULT 0,
                GreenInRegulation  INTEGER,
                Penalties          INTEGER DEFAULT 0,
                Proximity          TEXT,
                Notes              TEXT,
                CreatedAt          TEXT    NOT NULL,
                UpdatedAt          TEXT    NOT NULL,
                UNIQUE(RoundID, HoleNumber)
            );

            CREATE TABLE IF NOT EXISTS ShotSegment (
                ID            INTEGER PRIMARY KEY AUTOINCREMENT,
                HoleID        INTEGER NOT NULL REFERENCES Hole(ID) ON DELETE CASCADE,
                Sequence      INTEGER NOT NULL DEFAULT 0,
                Latitude      REAL    NOT NULL,
                Longitude     REAL    NOT NULL,
                Tag           TEXT,
                CreatedAt     TEXT    NOT NULL,
                Accuracy      REAL
            );

            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID ON ShotSegment(HoleID);
            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID_Sequence ON ShotSegment(HoleID, Sequence);";
        cmd.ExecuteNonQuery();
    }

    private long InsertPlayer()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO Player (Name, CreatedAt, UpdatedAt) VALUES ('Test', $ca, $ua); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertCourse()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO Course (Name, Holes, Par, CreatedAt, UpdatedAt) VALUES ('Test Course', 18, 72, $ca, $ua); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertRound(long playerId, long courseId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO Round (PlayerID, CourseID, StartedAt, CreatedAt, UpdatedAt) VALUES ($pid, $cid, $sa, $ca, $ua); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$pid", playerId);
        cmd.Parameters.AddWithValue("$cid", courseId);
        cmd.Parameters.AddWithValue("$sa", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertHole(long roundId, int holeNumber, int par, int score = 4)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO Hole (RoundID, HoleNumber, Par, Score, IsScored, Penalties, CreatedAt, UpdatedAt) VALUES ($rid, $hn, $par, $score, 1, 0, $ca, $ua); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$rid", roundId);
        cmd.Parameters.AddWithValue("$hn", holeNumber);
        cmd.Parameters.AddWithValue("$par", par);
        cmd.Parameters.AddWithValue("$score", score);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    private long InsertShotSegment(long holeId, int sequence, double lat, double lon, string? tag, double? accuracy)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ShotSegment (HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy)
            VALUES ($hid, $seq, $lat, $lon, $tag, $ca, $acc);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$hid", holeId);
        cmd.Parameters.AddWithValue("$seq", sequence);
        cmd.Parameters.AddWithValue("$lat", lat);
        cmd.Parameters.AddWithValue("$lon", lon);
        cmd.Parameters.AddWithValue("$tag", (object?)tag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ca", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$acc", (object?)accuracy ?? DBNull.Value);
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>
    /// Simulates the batched hydration query used by RoundRepository: loads all
    /// ShotSegment rows for a set of hole IDs in one query and groups them by HoleID.
    /// </summary>
    private Dictionary<long, List<(long id, int seq, double lat, double lon, string? tag, double? accuracy)>>
        LoadSegmentsByHoleId(IEnumerable<long> holeIds)
    {
        var idList = holeIds.ToList();
        var result = new Dictionary<long, List<(long, int, double, double, string?, double?)>>();

        if (idList.Count == 0)
            return result;

        using var cmd = _db.CreateCommand();
        var paramNames = new string[idList.Count];
        for (int i = 0; i < idList.Count; i++)
        {
            paramNames[i] = $"@hid{i}";
            cmd.Parameters.AddWithValue(paramNames[i], idList[i]);
        }

        cmd.CommandText = $"SELECT ID, HoleID, Sequence, Latitude, Longitude, Tag, Accuracy FROM ShotSegment WHERE HoleID IN ({string.Join(",", paramNames)}) ORDER BY HoleID, Sequence DESC;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var hid = reader.GetInt64(1);
            if (!result.TryGetValue(hid, out var list))
                result[hid] = list = new();
            list.Add((
                reader.GetInt64(0),
                reader.GetInt32(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : (double?)reader.GetDouble(6)
            ));
        }
        return result;
    }

    // ── tests ─────────────────────────────────────────────────────────────

    [Fact]
    public void RoundHydration_SingleHole_SegmentsAttached()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid = InsertHole(rid, 1, 4);

        InsertShotSegment(hid, 1, 37.12345, -122.54321, "Drive", 5.0);
        InsertShotSegment(hid, 2, 37.12400, -122.54200, null, null);

        var segsByHole = LoadSegmentsByHoleId(new[] { hid });

        segsByHole.Should().ContainKey(hid);
        segsByHole[hid].Should().HaveCount(2);
    }

    [Fact]
    public void RoundHydration_MultipleHoles_SegmentsCorrectlyGrouped()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid1 = InsertHole(rid, 1, 4);
        var hid2 = InsertHole(rid, 2, 3);
        var hid3 = InsertHole(rid, 3, 5);

        // Hole 1: 3 segments
        InsertShotSegment(hid1, 1, 37.1, -122.1, "Drive", 4.0);
        InsertShotSegment(hid1, 2, 37.2, -122.2, "Approach", 3.5);
        InsertShotSegment(hid1, 3, 37.3, -122.3, "Chip", null);

        // Hole 2: 1 segment
        InsertShotSegment(hid2, 1, 38.1, -121.1, "TeeShot", 6.0);

        // Hole 3: 0 segments

        var segsByHole = LoadSegmentsByHoleId(new[] { hid1, hid2, hid3 });

        segsByHole.Should().ContainKey(hid1);
        segsByHole[hid1].Should().HaveCount(3);

        segsByHole.Should().ContainKey(hid2);
        segsByHole[hid2].Should().HaveCount(1);

        // Hole 3 has no segments, so should not appear in the result dictionary
        segsByHole.Should().NotContainKey(hid3);
    }

    [Fact]
    public void RoundHydration_SegmentFields_RoundTripCorrectly()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid = InsertHole(rid, 1, 4);

        var segId = InsertShotSegment(hid, 7, 37.98765, -122.12345, "Putt", 2.5);

        var segsByHole = LoadSegmentsByHoleId(new[] { hid });

        var seg = segsByHole[hid].Single(s => s.id == segId);
        seg.seq.Should().Be(7);
        seg.lat.Should().BeApproximately(37.98765, 1e-5);
        seg.lon.Should().BeApproximately(-122.12345, 1e-5);
        seg.tag.Should().Be("Putt");
        seg.accuracy.Should().BeApproximately(2.5, 1e-9);
    }

    [Fact]
    public void RoundHydration_SegmentWithNullTagAndAccuracy_RoundTripCorrectly()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid = InsertHole(rid, 1, 4);

        var segId = InsertShotSegment(hid, 1, 51.5074, -0.1278, null, null);

        var segsByHole = LoadSegmentsByHoleId(new[] { hid });

        var seg = segsByHole[hid].Single(s => s.id == segId);
        seg.tag.Should().BeNull();
        seg.accuracy.Should().BeNull();
    }

    [Fact]
    public void RoundHydration_HoleWithNoSegments_DoesNotAppearInResult()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid = InsertHole(rid, 1, 4);

        var segsByHole = LoadSegmentsByHoleId(new[] { hid });

        // A hole with no segments should not have an entry rather than an empty list.
        segsByHole.Should().NotContainKey(hid);
    }

    [Fact]
    public void RoundHydration_SegmentsOrderedBySequenceDescendingPerHole()
    {
        var pid = InsertPlayer();
        var cid = InsertCourse();
        var rid = InsertRound(pid, cid);
        var hid = InsertHole(rid, 1, 4);

        InsertShotSegment(hid, 3, 37.3, -122.3, null, null);
        InsertShotSegment(hid, 1, 37.1, -122.1, null, null);
        InsertShotSegment(hid, 2, 37.2, -122.2, null, null);

        var segsByHole = LoadSegmentsByHoleId(new[] { hid });

        var seqs = segsByHole[hid].Select(s => s.seq).ToList();
        seqs.Should().BeInDescendingOrder();
    }
}
