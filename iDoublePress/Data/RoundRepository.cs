using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.IO;
using System.Text;
using System;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing golf rounds in the database.
/// </summary>
public class RoundRepository : RepositoryBase
{
    private readonly SemaphoreSlim _roundWriteSemaphore = new(1, 1);
    private readonly SemaphoreSlim _holeWriteSemaphore = new(3, 3); // Allow concurrent hole writes
    private readonly CourseRepository _courseRepository;
    private readonly PlayerRepository _playerRepository;

    // Simple on-device SQL trace to capture last statements and parameter values.
    // Helps diagnosing native sqlite crashes by recording the SQL being prepared.
    private void LogSql(SqliteCommand cmd, string? note = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("----- " + DateTime.Now.ToString("o") + (note != null ? " " + note : ""));
            sb.AppendLine(cmd.CommandText ?? string.Empty);
            if (cmd.Parameters != null && cmd.Parameters.Count > 0)
            {
                foreach (SqliteParameter p in cmd.Parameters)
                {
                    var val = p.Value == null || p.Value == DBNull.Value ? "<null>" : p.Value.ToString();
                    sb.AppendLine($"{p.ParameterName} = {val}");
                }
            }
            sb.AppendLine();

            var path = Path.Combine(FileSystem.AppDataDirectory, "sqlite_trace.log");
            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // swallow logging errors
        }
    }

    public RoundRepository(CourseRepository courseRepository, PlayerRepository playerRepository, ILogger<RoundRepository> logger) : base(logger)
    {
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
    }

    private async Task Init()
    {
        await EnsureInitializedAsync();
    }

    protected override async Task InitializeInternalAsync()
    {
        await using var connection = await CreateConnectionAsync();

        try
        {
            var pragmaFk = connection.CreateCommand();
            pragmaFk.CommandText = "PRAGMA foreign_keys = ON;";
            LogSql(pragmaFk, "PRAGMA foreign_keys");
            await pragmaFk.ExecuteNonQueryAsync();

            var pragmaBusy = connection.CreateCommand();
            pragmaBusy.CommandText = "PRAGMA busy_timeout = 5000;";
            LogSql(pragmaBusy, "PRAGMA busy_timeout");
            await pragmaBusy.ExecuteNonQueryAsync();

            var createRoundTableCmd = connection.CreateCommand();
            createRoundTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Round (
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
            );";
            LogSql(createRoundTableCmd, "DDL Round");
            await createRoundTableCmd.ExecuteNonQueryAsync();

            var createHoleTableCmd = connection.CreateCommand();
            createHoleTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Hole (
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
            );";
            LogSql(createHoleTableCmd, "DDL Hole");
            await createHoleTableCmd.ExecuteNonQueryAsync();

            var checkColumnCmd = connection.CreateCommand();
            checkColumnCmd.CommandText = "PRAGMA table_info(Hole);";
            LogSql(checkColumnCmd, "PRAGMA table_info(Hole)");

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await checkColumnCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            if (!existingColumns.Contains("Putts"))
            {
                _logger.LogInformation("Adding Putts column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN Putts INTEGER;";
                LogSql(addColumnCmd, "ALTER TABLE ADD Putts");
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("IsScored"))
            {
                _logger.LogInformation("Adding IsScored column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN IsScored INTEGER DEFAULT 0;";
                LogSql(addColumnCmd, "ALTER TABLE ADD IsScored");
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("FairwayResult"))
            {
                _logger.LogInformation("Adding FairwayResult column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN FairwayResult INTEGER DEFAULT 0;";
                LogSql(addColumnCmd, "ALTER TABLE ADD FairwayResult");
                await addColumnCmd.ExecuteNonQueryAsync();

                if (existingColumns.Contains("FairwayHit"))
                {
                    var migrateCmd = connection.CreateCommand();
                    migrateCmd.CommandText = @"
                    UPDATE Hole
                    SET FairwayResult = CASE
                        WHEN FairwayHit IS NULL THEN 0
                        WHEN FairwayHit = 1 THEN 2
                        ELSE 0
                    END
                    WHERE FairwayResult = 0;";
                    LogSql(migrateCmd, "MIGRATE FairwayResult");
                    await migrateCmd.ExecuteNonQueryAsync();
                }
            }

            if (!existingColumns.Contains("FairwayMissPenalty"))
            {
                _logger.LogInformation("Adding FairwayMissPenalty column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN FairwayMissPenalty INTEGER DEFAULT 0;";
                LogSql(addColumnCmd, "ALTER TABLE ADD FairwayMissPenalty");
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("Proximity"))
            {
                _logger.LogInformation("Adding Proximity column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN Proximity TEXT;";
                LogSql(addColumnCmd, "ALTER TABLE ADD Proximity");
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("Yardage"))
            {
                _logger.LogInformation("Adding Yardage column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN Yardage INTEGER;";
                LogSql(addColumnCmd, "ALTER TABLE ADD Yardage");
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            var createIndexes = connection.CreateCommand();
            createIndexes.CommandText = @"
            CREATE INDEX IF NOT EXISTS IDX_Round_PlayerID ON Round(PlayerID);
            CREATE INDEX IF NOT EXISTS IDX_Round_COURSEID ON Round(CourseID);
            CREATE INDEX IF NOT EXISTS IDX_Round_StartTime ON Round(StartTime DESC);
            CREATE INDEX IF NOT EXISTS IDX_Round_Status ON Round(Status);
            CREATE INDEX IF NOT EXISTS IDX_Hole_RoundID ON Hole(RoundID);";
            LogSql(createIndexes, "CREATE INDEXES");
            await createIndexes.ExecuteNonQueryAsync();

            var createShotSegmentTableCmd = connection.CreateCommand();
            createShotSegmentTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ShotSegment (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                HoleID INTEGER NOT NULL,
                Sequence INTEGER NOT NULL,
                Latitude REAL NOT NULL,
                Longitude REAL NOT NULL,
                Tag TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (HoleID) REFERENCES Hole(ID) ON DELETE CASCADE
            );";
            LogSql(createShotSegmentTableCmd, "DDL ShotSegment");
            await createShotSegmentTableCmd.ExecuteNonQueryAsync();

            var createShotIndexes = connection.CreateCommand();
            createShotIndexes.CommandText = @"
            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID ON ShotSegment(HoleID);
            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID_Sequence ON ShotSegment(HoleID, Sequence);";
            LogSql(createShotIndexes, "CREATE INDEXES ShotSegment");
            await createShotIndexes.ExecuteNonQueryAsync();

            // Ensure Tag column exists for older databases
            var checkShotCols = connection.CreateCommand();
            checkShotCols.CommandText = "PRAGMA table_info(ShotSegment);";
            LogSql(checkShotCols, "PRAGMA table_info(ShotSegment)");
            var existingShotCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var readerCols = await checkShotCols.ExecuteReaderAsync())
            {
                while (await readerCols.ReadAsync())
                {
                    existingShotCols.Add(readerCols.GetString(1));
                }
            }

            if (!existingShotCols.Contains("Tag"))
            {
                var addTagCmd = connection.CreateCommand();
                addTagCmd.CommandText = "ALTER TABLE ShotSegment ADD COLUMN Tag TEXT;";
                LogSql(addTagCmd, "ALTER TABLE ADD Tag ShotSegment");
                await addTagCmd.ExecuteNonQueryAsync();
            }

            if (!existingShotCols.Contains("Accuracy"))
            {
                var addAccuracyCmd = connection.CreateCommand();
                addAccuracyCmd.CommandText = "ALTER TABLE ShotSegment ADD COLUMN Accuracy REAL;";
                LogSql(addAccuracyCmd, "ALTER TABLE ADD Accuracy ShotSegment");
                await addAccuracyCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating Round tables");
            throw;
        }

        _hasBeenInitialized = true;
    }

    public async Task<List<Round>> ListAsync()
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Round ORDER BY StartTime DESC";
        LogSql(selectCmd, "ListAsync");
        var rounds = new List<Round>();

        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var round = await ReadRoundFromReader(reader, connection);
            rounds.Add(round);
        }

        return rounds;
    }

    public async Task<Round?> GetAsync(int id)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Round WHERE ID = @id";
        selectCmd.Parameters.AddWithValue("@id", id);
        LogSql(selectCmd, "GetAsync");

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return await ReadRoundFromReader(reader, connection);
        }

        return null;
    }

    public async Task<Round?> GetInProgressRoundAsync(int playerId)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Round WHERE PlayerID = @playerId AND Status = 'InProgress' ORDER BY StartTime DESC LIMIT 1";
        selectCmd.Parameters.AddWithValue("@playerId", playerId);
        LogSql(selectCmd, "GetInProgressRoundAsync");

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return await ReadRoundFromReader(reader, connection);
        }

        return null;
    }

    public async Task<List<Round>> GetInProgressRoundsAsync(int playerId)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Round WHERE PlayerID = @playerId AND Status = 'InProgress' ORDER BY StartTime DESC";
        selectCmd.Parameters.AddWithValue("@playerId", playerId);
        LogSql(selectCmd, "GetInProgressRoundsAsync");

        var rounds = new List<Round>();
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var round = await ReadRoundFromReader(reader, connection);
            rounds.Add(round);
        }

        return rounds;
    }

    private async Task<Round> ReadRoundFromReader(SqliteDataReader reader, SqliteConnection connection)
    {
        var round = new Round
        {
            ID = reader.GetInt32(0),
            PlayerID = reader.GetInt32(1),
            CourseID = reader.GetInt32(2),
            StartTime = DateTime.Parse(reader.GetString(3)),
            EndTime = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
            TotalScore = reader.GetInt32(5),
            Status = Enum.Parse<RoundStatus>(reader.GetString(6)),
            Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
            Weather = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAt = DateTime.Parse(reader.GetString(9)),
            UpdatedAt = DateTime.Parse(reader.GetString(10))
        };

        round.Player = await _playerRepository.GetAsync(round.PlayerID);
        round.Course = await _courseRepository.GetAsync(round.CourseID);
        round.Holes = await GetHolesAsync(connection, round.ID);

        return round;
    }

    private async Task<List<Hole>> GetHolesAsync(SqliteConnection connection, int roundId)
    {
        var selectHolesCmd = connection.CreateCommand();
        selectHolesCmd.CommandText = @"
            SELECT ID, RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts,
                   FairwayHit, FairwayResult, FairwayMissPenalty,
                   GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt
            FROM Hole
            WHERE RoundID = @roundId
            ORDER BY HoleNumber";
        selectHolesCmd.Parameters.AddWithValue("@roundId", roundId);
        LogSql(selectHolesCmd, "GetHolesAsync");

        var holes = new List<Hole>();
        await using var reader = await selectHolesCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fairwayResult = FairwayResult.None;
            if (!reader.IsDBNull(9))
            {
                fairwayResult = (FairwayResult)reader.GetInt32(9);
            }
            else if (!reader.IsDBNull(8))
            {
                fairwayResult = reader.GetInt32(8) == 1 ? FairwayResult.Fairway : FairwayResult.None;
            }

            char? proximity = null;
            if (!reader.IsDBNull(13))
            {
                var s = reader.GetString(13);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var c = char.ToUpperInvariant(s.Trim()[0]);
                    if (c is 'S' or 'M' or 'L')
                        proximity = c;
                }
            }

            holes.Add(new Hole
            {
                ID = reader.GetInt32(0),
                RoundID = reader.GetInt32(1),
                HoleNumber = reader.GetInt32(2),
                Par = reader.GetInt32(3),
                Yardage = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Score = reader.GetInt32(5),
                IsScored = reader.GetInt32(6) == 1,
                Putts = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                FairwayResult = fairwayResult,
                FairwayMissPenalty = !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                GreenInRegulation = reader.IsDBNull(11) ? null : reader.GetInt32(11) == 1,
                Penalties = reader.GetInt32(12),
                Proximity = proximity,
                Notes = reader.IsDBNull(14) ? null : reader.GetString(14),
                CreatedAt = DateTime.Parse(reader.GetString(15)),
                UpdatedAt = DateTime.Parse(reader.GetString(16))
            });
        }

        // Load shot segments for each hole
        foreach (var hole in holes)
        {
            hole.Notes = hole.Notes; // keep
            var segments = await GetShotSegmentsForHoleAsync(connection, hole.ID);
            // Attach segments to hole via a property if needed; currently Hole doesn't have property for segments.
            // We'll store in a transient dictionary in memory on Round if needed. For now, leave hole unchanged.
        }

        return holes;
    }

    private async Task<List<ShotSegment>> GetShotSegmentsForHoleAsync(SqliteConnection connection, int holeId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ID, HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy
            FROM ShotSegment
            WHERE HoleID = @holeId
            ORDER BY Sequence DESC";
        cmd.Parameters.AddWithValue("@holeId", holeId);
        LogSql(cmd, "GetShotSegmentsForHoleAsync");

        var list = new List<ShotSegment>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var seg = new ShotSegment
            {
                ID = reader.GetInt32(0),
                HoleID = reader.GetInt32(1),
                Sequence = reader.GetInt32(2),
                Point = new Location(reader.GetDouble(3), reader.GetDouble(4)),
                Tag = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                AccuracyMeters = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                LocationDisplay = $"{reader.GetDouble(3):F7}, {reader.GetDouble(4):F7}",
                DistanceDisplay = "---"
            };
            list.Add(seg);
        }

        return list;
    }

    public async Task<List<ShotSegment>> GetShotSegmentsForHoleAsync(int holeId)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();
        var segments = await GetShotSegmentsForHoleAsync(connection, holeId);
        // FIX: Ensure segments are ordered Newest First (Desc) to match ToggleMeasurement logic
        // This ensures the logic in RecalculateDistances aligns tags with the correct intervals.
        var loaded = new List<ShotSegment>(segments.Count);
        foreach (var s in segments.OrderByDescending(x => x.CreatedAt))
            loaded.Add(s);

        ShotSegmentUtilities.RecalculateDistances(loaded);
        return loaded;
    }

    public async Task SaveShotSegmentsForHoleAsync(int holeId, IEnumerable<ShotSegment> segments)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            // Delete existing segments for hole then insert provided list with proper sequence
            var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM ShotSegment WHERE HoleID = @holeId";
            deleteCmd.Parameters.AddWithValue("@holeId", holeId);
            LogSql(deleteCmd, "DeleteShotSegmentsForHole");
            await deleteCmd.ExecuteNonQueryAsync();

            int seq = 0; // newer first expected
            foreach (var s in segments)
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO ShotSegment (HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy)
                    VALUES (@HoleID, @Sequence, @Latitude, @Longitude, @Tag, @CreatedAt, @Accuracy);";
                insertCmd.Parameters.AddWithValue("@HoleID", holeId);
                insertCmd.Parameters.AddWithValue("@Sequence", seq);
                insertCmd.Parameters.AddWithValue("@Latitude", s.Point.Latitude);
                insertCmd.Parameters.AddWithValue("@Longitude", s.Point.Longitude);
                insertCmd.Parameters.AddWithValue("@Tag", (object?)s.Tag ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@CreatedAt", (s.CreatedAt == default ? DateTime.Now : s.CreatedAt).ToString("o"));
                insertCmd.Parameters.AddWithValue("@Accuracy", (object?)s.AccuracyMeters ?? DBNull.Value);
                LogSql(insertCmd, "InsertShotSegment");
                await insertCmd.ExecuteNonQueryAsync();
                seq++;
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteShotSegmentsForHoleAsync(int holeId)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ShotSegment WHERE HoleID = @holeId";
        cmd.Parameters.AddWithValue("@holeId", holeId);
        LogSql(cmd, "DeleteShotSegmentsForHoleAsync");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();

            var course = await _courseRepository.GetAsync(courseId);
            if (course == null)
                throw new InvalidOperationException($"Course with ID {courseId} not found");

            var round = new Round
            {
                PlayerID = playerId,
                CourseID = courseId,
                StartTime = DateTime.Now,
                Status = RoundStatus.InProgress,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Course = course
            };

            await using var connection = await CreateConnectionAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var insertRoundCmd = connection.CreateCommand();
                insertRoundCmd.CommandText = @"
                INSERT INTO Round (PlayerID, CourseID, StartTime, Status, CreatedAt, UpdatedAt)
                VALUES (@PlayerID, @CourseID, @StartTime, @Status, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
                insertRoundCmd.Parameters.AddWithValue("@PlayerID", round.PlayerID);
                insertRoundCmd.Parameters.AddWithValue("@CourseID", round.CourseID);
                insertRoundCmd.Parameters.AddWithValue("@StartTime", round.StartTime.ToString("o"));
                insertRoundCmd.Parameters.AddWithValue("@Status", round.Status.ToString());
                insertRoundCmd.Parameters.AddWithValue("@CreatedAt", round.CreatedAt.ToString("o"));
                insertRoundCmd.Parameters.AddWithValue("@UpdatedAt", round.UpdatedAt.ToString("o"));

                LogSql(insertRoundCmd, "CreateNewRound");
                var result = await insertRoundCmd.ExecuteScalarAsync();
                round.ID = Convert.ToInt32(result);

                foreach (var courseHole in course.CourseHoles.OrderBy(h => h.HoleNumber))
                {
                    var hole = new Hole
                    {
                        RoundID = round.ID,
                        HoleNumber = courseHole.HoleNumber,
                        Par = courseHole.Par,
                        Yardage = courseHole.Yardage,
                        Score = courseHole.Par,
                        IsScored = false,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await SaveHoleAsync(connection, hole, transaction);
                    round.Holes.Add(hole);
                }

                transaction.Commit();
                return round;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }

    private async Task<bool> CheckDatabaseIntegrityAsync(SqliteConnection connection)
    {
        // Use a very small, safe query against sqlite_master instead of `PRAGMA integrity_check`
        // `integrity_check` can trigger heavy internal parsing and has previously crashed on corrupted DB files.
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' LIMIT 1;";
            LogSql(cmd, "CheckDatabaseIntegrity");
            var result = await cmd.ExecuteScalarAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lightweight DB read failed � treating database as possibly corrupt");
            return false;
        }
    }

    public async Task<int> SaveItemAsync(Round item)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            await using var connection = await CreateConnectionAsync();

            // Verify DB integrity before performing update to avoid sqlite native crashes
            if (!await CheckDatabaseIntegrityAsync(connection))
            {
                _logger.LogError("Database integrity check failed before SaveItemAsync");
                throw new InvalidOperationException("Database appears corrupt. Aborting save.");
            }

            item.UpdatedAt = DateTime.Now;
            item.TotalScore = item.Holes.Where(h => h.IsScored).Sum(h => h.Score);

            using var transaction = connection.BeginTransaction();
            try
            {
                var saveCmd = connection.CreateCommand();
                saveCmd.Transaction = transaction;
                saveCmd.CommandText = @"
            UPDATE Round
            SET PlayerID = @PlayerID, CourseID = @CourseID, StartTime = @StartTime, EndTime = @EndTime,
                TotalScore = @TotalScore, Status = @Status, Notes = @Notes, Weather = @Weather, UpdatedAt = @UpdatedAt
            WHERE ID = @ID";

                saveCmd.Parameters.AddWithValue("@ID", item.ID);
                saveCmd.Parameters.AddWithValue("@PlayerID", item.PlayerID);
                saveCmd.Parameters.AddWithValue("@CourseID", item.CourseID);
                saveCmd.Parameters.AddWithValue("@StartTime", item.StartTime.ToString("o"));
                saveCmd.Parameters.AddWithValue("@EndTime", item.EndTime?.ToString("o") ?? (object)DBNull.Value);
                saveCmd.Parameters.AddWithValue("@TotalScore", item.TotalScore);
                saveCmd.Parameters.AddWithValue("@Status", item.Status.ToString());
                saveCmd.Parameters.AddWithValue("@Notes", (object?)item.Notes ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@Weather", (object?)item.Weather ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@UpdatedAt", item.UpdatedAt.ToString("o"));

                LogSql(saveCmd, "SaveItem");
                try
                {
                    await saveCmd.ExecuteNonQueryAsync();
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "SQLite error executing SaveItemAsync. SQL: {Sql}", saveCmd.CommandText);
                    throw;
                }

                foreach (var hole in item.Holes)
                {
                    await SaveHoleAsync(connection, hole, transaction);
                }

                transaction.Commit();
                return item.ID;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }

    public async Task SaveHoleAsync(Hole hole)
    {
        await _holeWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            await using var connection = await CreateConnectionAsync();

            if (!await CheckDatabaseIntegrityAsync(connection))
            {
                _logger.LogError("Database integrity check failed before SaveHoleAsync");
                throw new InvalidOperationException("Database appears corrupt. Aborting save.");
            }

            using var transaction = connection.BeginTransaction();
            try
            {
                await SaveHoleAsync(connection, hole, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _holeWriteSemaphore.Release();
        }
    }

    private async Task SaveHoleAsync(SqliteConnection connection, Hole hole, SqliteTransaction? transaction = null)
    {
        hole.UpdatedAt = DateTime.Now;

        // Defensive parameter normalization
        if (!string.IsNullOrEmpty(hole.Notes) && hole.Notes.Length > 20000)
            hole.Notes = hole.Notes.Substring(0, 20000);
        if (hole.Proximity.HasValue && !("SML".Contains(hole.Proximity.Value)))
            hole.Proximity = null;

        var saveCmd = connection.CreateCommand();
        if (transaction != null)
            saveCmd.Transaction = transaction;
        if (hole.ID == 0)
        {
            hole.CreatedAt = DateTime.Now;

            saveCmd.CommandText = @"
                INSERT INTO Hole (RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts, FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt)
                VALUES (@RoundID, @HoleNumber, @Par, @Yardage, @Score, @IsScored, @Putts, @FairwayHit, @FairwayResult, @FairwayMissPenalty, @GreenInRegulation, @Penalties, @Proximity, @Notes, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
        }
        else
        {
            saveCmd.CommandText = @"
                UPDATE Hole
                SET RoundID = @RoundID, HoleNumber = @HoleNumber, Par = @Par, Yardage = @Yardage, Score = @Score, IsScored = @IsScored, Putts = @Putts,
                    FairwayHit = @FairwayHit,
                    FairwayResult = @FairwayResult,
                    FairwayMissPenalty = @FairwayMissPenalty,
                    GreenInRegulation = @GreenInRegulation,
                    Penalties = @Penalties,
                    Proximity = @Proximity,
                    Notes = @Notes, UpdatedAt = @UpdatedAt
                WHERE ID = @ID";
            saveCmd.Parameters.AddWithValue("@ID", hole.ID);
        }

        saveCmd.Parameters.AddWithValue("@RoundID", hole.RoundID);
        saveCmd.Parameters.AddWithValue("@HoleNumber", hole.HoleNumber);
        saveCmd.Parameters.AddWithValue("@Par", hole.Par);
        saveCmd.Parameters.AddWithValue("@Yardage", (object?)hole.Yardage ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@Score", hole.Score);
        saveCmd.Parameters.AddWithValue("@IsScored", hole.IsScored ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@Putts", (object?)hole.Putts ?? DBNull.Value);

        saveCmd.Parameters.AddWithValue("@FairwayHit", hole.FairwayResult == FairwayResult.Fairway ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@FairwayResult", (int)hole.FairwayResult);
        saveCmd.Parameters.AddWithValue("@FairwayMissPenalty", hole.FairwayMissPenalty ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@GreenInRegulation", hole.GreenInRegulation.HasValue ? (hole.GreenInRegulation.Value ? 1 : 0) : DBNull.Value);
        saveCmd.Parameters.AddWithValue("@Penalties", hole.Penalties);
        saveCmd.Parameters.AddWithValue("@Proximity", hole.Proximity.HasValue ? hole.Proximity.Value.ToString() : (object)DBNull.Value);
        saveCmd.Parameters.AddWithValue("@Notes", (object?)hole.Notes ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@CreatedAt", hole.CreatedAt.ToString("o"));
        saveCmd.Parameters.AddWithValue("@UpdatedAt", hole.UpdatedAt.ToString("o"));

        LogSql(saveCmd, "SaveHole");
        try
        {
            if (hole.ID == 0)
            {
                var result = await saveCmd.ExecuteScalarAsync();
                hole.ID = Convert.ToInt32(result);
            }
            else
            {
                await saveCmd.ExecuteNonQueryAsync();
            }
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "SQLite error executing SaveHoleAsync. SQL: {Sql}", saveCmd.CommandText);
            throw;
        }
    }

    public async Task<int> DeleteItemAsync(Round item)
    {
        await _roundWriteSemaphore.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            await using var connection = await CreateConnectionAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM Round WHERE ID = @ID";
                deleteCmd.Parameters.AddWithValue("@ID", item.ID);

                LogSql(deleteCmd, "DeleteItem");
                var result = await deleteCmd.ExecuteNonQueryAsync();
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _roundWriteSemaphore.Release();
        }
    }
}
