using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Threading;

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
        await Task.CompletedTask;
    }

    public async Task<List<Round>> ListAsync()
    {
        await Init();
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, PlayerID, CourseID, StartTime, EndTime, TotalScore, Status, Notes, Weather,
                   CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Round
            WHERE IsDeleted = 0
            ORDER BY StartTime DESC";
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
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, PlayerID, CourseID, StartTime, EndTime, TotalScore, Status, Notes, Weather,
                   CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Round
            WHERE ID = @id AND IsDeleted = 0";
        selectCmd.Parameters.AddWithValue("@id", id);

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
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, PlayerID, CourseID, StartTime, EndTime, TotalScore, Status, Notes, Weather,
                   CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Round
            WHERE PlayerID = @playerId AND Status = 'InProgress' AND IsDeleted = 0
            ORDER BY StartTime DESC
            LIMIT 1";
        selectCmd.Parameters.AddWithValue("@playerId", playerId);

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
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, PlayerID, CourseID, StartTime, EndTime, TotalScore, Status, Notes, Weather,
                   CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Round
            WHERE PlayerID = @playerId AND Status = 'InProgress' AND IsDeleted = 0
            ORDER BY StartTime DESC";
        selectCmd.Parameters.AddWithValue("@playerId", playerId);

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
            CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(9)),
            UpdatedAt = DatabaseDateTime.ParseUtc(reader.GetString(10)),
            PublicId = reader.GetString(11),
            SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(12)),
            IsDeleted = reader.GetInt32(13) == 1,
            DeletedAtUtc = reader.IsDBNull(14) ? null : DatabaseDateTime.ParseUtc(reader.GetString(14)),
            ServerRevision = reader.IsDBNull(15) ? null : reader.GetString(15),
            PendingSync = reader.GetInt32(16) == 1
        };

        round.Player = await _playerRepository.GetAsync(round.PlayerID, includeDeleted: true);
        round.Course = await _courseRepository.GetAsync(round.CourseID, includeDeleted: true);
        round.Holes = await GetHolesAsync(connection, round.ID);

        return round;
    }

    private async Task<List<Hole>> GetHolesAsync(SqliteConnection connection, int roundId)
    {
        var selectHolesCmd = connection.CreateCommand();
        selectHolesCmd.CommandText = @"
            SELECT ID, RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts,
                   FairwayHit, FairwayResult, FairwayMissPenalty,
                   GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt,
                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Hole
            WHERE RoundID = @roundId
              AND IsDeleted = 0
            ORDER BY HoleNumber";
        selectHolesCmd.Parameters.AddWithValue("@roundId", roundId);

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
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(15)),
                UpdatedAt = DatabaseDateTime.ParseUtc(reader.GetString(16)),
                PublicId = reader.GetString(17),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(18)),
                IsDeleted = reader.GetInt32(19) == 1,
                DeletedAtUtc = reader.IsDBNull(20) ? null : DatabaseDateTime.ParseUtc(reader.GetString(20)),
                ServerRevision = reader.IsDBNull(21) ? null : reader.GetString(21),
                PendingSync = reader.GetInt32(22) == 1
            });
        }

        // Load all shot segments for all holes in a single query and attach them.
        if (holes.Count > 0)
        {
            var holeIdLookup = holes.ToDictionary(h => h.ID);
            var allSegments = await GetShotSegmentsForHolesAsync(connection, holeIdLookup.Keys);
            foreach (var seg in allSegments)
            {
                if (holeIdLookup.TryGetValue(seg.HoleID, out var hole))
                    hole.ShotSegments.Add(seg);
            }
        }

        return holes;
    }

    private async Task<List<ShotSegment>> GetShotSegmentsForHolesAsync(SqliteConnection connection, IEnumerable<int> holeIds, SqliteTransaction? transaction = null)
    {
        var idList = holeIds.ToList();
        if (idList.Count == 0)
            return new List<ShotSegment>();

        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        var paramNames = new string[idList.Count];
        for (int i = 0; i < idList.Count; i++)
        {
            paramNames[i] = $"@hid{i}";
            cmd.Parameters.AddWithValue(paramNames[i], idList[i]);
        }

        cmd.CommandText = $@"
            SELECT ID, HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy,
                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM ShotSegment
            WHERE HoleID IN ({string.Join(",", paramNames)})
              AND IsDeleted = 0
            ORDER BY HoleID, Sequence DESC";

        var list = new List<ShotSegment>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var lat = reader.GetDouble(3);
            var lon = reader.GetDouble(4);
            list.Add(new ShotSegment
            {
                ID = reader.GetInt32(0),
                HoleID = reader.GetInt32(1),
                Sequence = reader.GetInt32(2),
                Point = new Location(lat, lon),
                Tag = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(6)),
                AccuracyMeters = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                PublicId = reader.GetString(8),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(9)),
                IsDeleted = reader.GetInt32(10) == 1,
                DeletedAtUtc = reader.IsDBNull(11) ? null : DatabaseDateTime.ParseUtc(reader.GetString(11)),
                ServerRevision = reader.IsDBNull(12) ? null : reader.GetString(12),
                PendingSync = reader.GetInt32(13) == 1,
                LocationDisplay = $"{lat:F7}, {lon:F7}",
                DistanceDisplay = "---"
            });
        }

        return list;
    }

    private async Task<List<ShotSegment>> GetShotSegmentsForHoleAsync(SqliteConnection connection, int holeId, SqliteTransaction? transaction = null)
    {
        return await GetShotSegmentsForHolesAsync(connection, new[] { holeId }, transaction);
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
            var existingSegments = new Dictionary<int, ShotSegment>();
            var existingSegmentCmd = connection.CreateCommand();
            existingSegmentCmd.Transaction = transaction;
            existingSegmentCmd.CommandText = @"
                SELECT ID, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
                FROM ShotSegment
                WHERE HoleID = @HoleID";
            existingSegmentCmd.Parameters.AddWithValue("@HoleID", holeId);

            await using (var reader = await existingSegmentCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingSegments[reader.GetInt32(0)] = new ShotSegment
                    {
                        ID = reader.GetInt32(0),
                        HoleID = holeId,
                        PublicId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        SyncUpdatedAtUtc = reader.IsDBNull(2) ? DateTime.UtcNow : DatabaseDateTime.ParseUtc(reader.GetString(2)),
                        IsDeleted = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                        DeletedAtUtc = reader.IsDBNull(4) ? null : DatabaseDateTime.ParseUtc(reader.GetString(4)),
                        ServerRevision = reader.IsDBNull(5) ? null : reader.GetString(5),
                        PendingSync = !reader.IsDBNull(6) && reader.GetInt32(6) == 1
                    };
                }
            }

            var retainedIds = new HashSet<int>();
            int seq = 0;
            foreach (var s in segments)
            {
                s.HoleID = holeId;
                s.Sequence = seq;
                s.CreatedAt = s.CreatedAt == default ? DateTime.UtcNow : DatabaseDateTime.EnsureUtc(s.CreatedAt);

                if (s.ID != 0 && existingSegments.TryGetValue(s.ID, out var existing))
                {
                    s.PublicId = existing.PublicId;
                    s.ServerRevision = existing.ServerRevision;
                    retainedIds.Add(s.ID);
                }

                MarkEntityForUpsert(s);

                var saveCmd = connection.CreateCommand();
                saveCmd.Transaction = transaction;
                if (s.ID == 0)
                {
                    saveCmd.CommandText = @"
                        INSERT INTO ShotSegment (HoleID, Sequence, Latitude, Longitude, Tag, CreatedAt, Accuracy,
                                                 PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                        VALUES (@HoleID, @Sequence, @Latitude, @Longitude, @Tag, @CreatedAt, @Accuracy,
                                @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                        SELECT last_insert_rowid();";
                }
                else
                {
                    saveCmd.CommandText = @"
                        UPDATE ShotSegment
                        SET HoleID = @HoleID, Sequence = @Sequence, Latitude = @Latitude, Longitude = @Longitude,
                            Tag = @Tag, CreatedAt = @CreatedAt, Accuracy = @Accuracy,
                            PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc, IsDeleted = @IsDeleted,
                            DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
                        WHERE ID = @ID";
                    saveCmd.Parameters.AddWithValue("@ID", s.ID);
                    retainedIds.Add(s.ID);
                }

                saveCmd.Parameters.AddWithValue("@HoleID", holeId);
                saveCmd.Parameters.AddWithValue("@Sequence", seq);
                saveCmd.Parameters.AddWithValue("@Latitude", s.Point.Latitude);
                saveCmd.Parameters.AddWithValue("@Longitude", s.Point.Longitude);
                saveCmd.Parameters.AddWithValue("@Tag", (object?)s.Tag ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@CreatedAt", DatabaseDateTime.ToUtcString(s.CreatedAt));
                saveCmd.Parameters.AddWithValue("@Accuracy", (object?)s.AccuracyMeters ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@PublicId", s.PublicId);
                saveCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(s.SyncUpdatedAtUtc));
                saveCmd.Parameters.AddWithValue("@IsDeleted", s.IsDeleted ? 1 : 0);
                saveCmd.Parameters.AddWithValue("@DeletedAtUtc", s.DeletedAtUtc.HasValue ? DatabaseDateTime.ToUtcString(s.DeletedAtUtc.Value) : DBNull.Value);
                saveCmd.Parameters.AddWithValue("@ServerRevision", (object?)s.ServerRevision ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@PendingSync", s.PendingSync ? 1 : 0);

                var result = await saveCmd.ExecuteScalarAsync();
                if (s.ID == 0)
                {
                    s.ID = Convert.ToInt32(result);
                    retainedIds.Add(s.ID);
                }

                await QueueOutboxAsync(connection, transaction, nameof(ShotSegment), s, "upsert");
                seq++;
            }

            foreach (var existing in existingSegments.Values.Where(x => !x.IsDeleted && !retainedIds.Contains(x.ID)))
            {
                MarkEntityForDelete(existing);

                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = @"
                    UPDATE ShotSegment
                    SET IsDeleted = 1,
                        DeletedAtUtc = @DeletedAtUtc,
                        SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                        PendingSync = 1
                    WHERE ID = @ID";
                deleteCmd.Parameters.AddWithValue("@ID", existing.ID);
                deleteCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(existing.DeletedAtUtc ?? DateTime.UtcNow));
                deleteCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(existing.SyncUpdatedAtUtc));
                await deleteCmd.ExecuteNonQueryAsync();

                await QueueOutboxAsync(connection, transaction, nameof(ShotSegment), existing, "delete");
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
        using var transaction = connection.BeginTransaction();

        var segments = await GetShotSegmentsForHoleAsync(connection, holeId);
        foreach (var segment in segments)
        {
            MarkEntityForDelete(segment);

            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE ShotSegment
                SET IsDeleted = 1,
                    DeletedAtUtc = @DeletedAtUtc,
                    SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                    PendingSync = 1
                WHERE ID = @ID";
            cmd.Parameters.AddWithValue("@ID", segment.ID);
            cmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(segment.DeletedAtUtc ?? DateTime.UtcNow));
            cmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(segment.SyncUpdatedAtUtc));
            await cmd.ExecuteNonQueryAsync();

            await QueueOutboxAsync(connection, transaction, nameof(ShotSegment), segment, "delete");
        }

        transaction.Commit();
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Course = course
            };
            MarkEntityForUpsert(round);

            await using var connection = await CreateConnectionAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var insertRoundCmd = connection.CreateCommand();
                insertRoundCmd.Transaction = transaction;
                insertRoundCmd.CommandText = @"
                INSERT INTO Round (PlayerID, CourseID, StartTime, Status, CreatedAt, UpdatedAt,
                                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                VALUES (@PlayerID, @CourseID, @StartTime, @Status, @CreatedAt, @UpdatedAt,
                        @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                SELECT last_insert_rowid();";
                insertRoundCmd.Parameters.AddWithValue("@PlayerID", round.PlayerID);
                insertRoundCmd.Parameters.AddWithValue("@CourseID", round.CourseID);
                insertRoundCmd.Parameters.AddWithValue("@StartTime", round.StartTime.ToString("o"));
                insertRoundCmd.Parameters.AddWithValue("@Status", round.Status.ToString());
                insertRoundCmd.Parameters.AddWithValue("@CreatedAt", DatabaseDateTime.ToUtcString(round.CreatedAt));
                insertRoundCmd.Parameters.AddWithValue("@UpdatedAt", DatabaseDateTime.ToUtcString(round.UpdatedAt));
                insertRoundCmd.Parameters.AddWithValue("@PublicId", round.PublicId);
                insertRoundCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(round.SyncUpdatedAtUtc));
                insertRoundCmd.Parameters.AddWithValue("@IsDeleted", round.IsDeleted ? 1 : 0);
                insertRoundCmd.Parameters.AddWithValue("@DeletedAtUtc", DBNull.Value);
                insertRoundCmd.Parameters.AddWithValue("@ServerRevision", (object?)round.ServerRevision ?? DBNull.Value);
                insertRoundCmd.Parameters.AddWithValue("@PendingSync", round.PendingSync ? 1 : 0);

                var result = await insertRoundCmd.ExecuteScalarAsync();
                round.ID = Convert.ToInt32(result);
                await QueueOutboxAsync(connection, transaction, nameof(Round), round, "upsert");

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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
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

            item.UpdatedAt = DateTime.UtcNow;
            item.TotalScore = item.Holes.Where(h => h.IsScored).Sum(h => h.Score);
            MarkEntityForUpsert(item);

            using var transaction = connection.BeginTransaction();
            try
            {
                var saveCmd = connection.CreateCommand();
                saveCmd.Transaction = transaction;
                saveCmd.CommandText = @"
            UPDATE Round
            SET PlayerID = @PlayerID, CourseID = @CourseID, StartTime = @StartTime, EndTime = @EndTime,
                TotalScore = @TotalScore, Status = @Status, Notes = @Notes, Weather = @Weather, UpdatedAt = @UpdatedAt,
                PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc, IsDeleted = @IsDeleted,
                DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
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
                saveCmd.Parameters.AddWithValue("@UpdatedAt", DatabaseDateTime.ToUtcString(item.UpdatedAt));
                saveCmd.Parameters.AddWithValue("@PublicId", item.PublicId);
                saveCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(item.SyncUpdatedAtUtc));
                saveCmd.Parameters.AddWithValue("@IsDeleted", item.IsDeleted ? 1 : 0);
                saveCmd.Parameters.AddWithValue("@DeletedAtUtc", item.DeletedAtUtc.HasValue ? DatabaseDateTime.ToUtcString(item.DeletedAtUtc.Value) : DBNull.Value);
                saveCmd.Parameters.AddWithValue("@ServerRevision", (object?)item.ServerRevision ?? DBNull.Value);
                saveCmd.Parameters.AddWithValue("@PendingSync", item.PendingSync ? 1 : 0);

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

                await QueueOutboxAsync(connection, transaction, nameof(Round), item, "upsert");
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
        hole.UpdatedAt = DateTime.UtcNow;

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
            hole.CreatedAt = DatabaseDateTime.EnsureUtc(hole.CreatedAt);
            MarkEntityForUpsert(hole);

            saveCmd.CommandText = @"
                INSERT INTO Hole (RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts, FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Proximity, Notes, CreatedAt, UpdatedAt,
                                  PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                VALUES (@RoundID, @HoleNumber, @Par, @Yardage, @Score, @IsScored, @Putts, @FairwayHit, @FairwayResult, @FairwayMissPenalty, @GreenInRegulation, @Penalties, @Proximity, @Notes, @CreatedAt, @UpdatedAt,
                        @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                SELECT last_insert_rowid();";
        }
        else
        {
            hole.CreatedAt = DatabaseDateTime.EnsureUtc(hole.CreatedAt);
            MarkEntityForUpsert(hole);
            saveCmd.CommandText = @"
                UPDATE Hole
                SET RoundID = @RoundID, HoleNumber = @HoleNumber, Par = @Par, Yardage = @Yardage, Score = @Score, IsScored = @IsScored, Putts = @Putts,
                    FairwayHit = @FairwayHit,
                    FairwayResult = @FairwayResult,
                    FairwayMissPenalty = @FairwayMissPenalty,
                    GreenInRegulation = @GreenInRegulation,
                    Penalties = @Penalties,
                    Proximity = @Proximity,
                    Notes = @Notes, UpdatedAt = @UpdatedAt, PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                    IsDeleted = @IsDeleted, DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
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
        saveCmd.Parameters.AddWithValue("@CreatedAt", DatabaseDateTime.ToUtcString(hole.CreatedAt));
        saveCmd.Parameters.AddWithValue("@UpdatedAt", DatabaseDateTime.ToUtcString(hole.UpdatedAt));
        saveCmd.Parameters.AddWithValue("@PublicId", hole.PublicId);
        saveCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(hole.SyncUpdatedAtUtc));
        saveCmd.Parameters.AddWithValue("@IsDeleted", hole.IsDeleted ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@DeletedAtUtc", hole.DeletedAtUtc.HasValue ? DatabaseDateTime.ToUtcString(hole.DeletedAtUtc.Value) : DBNull.Value);
        saveCmd.Parameters.AddWithValue("@ServerRevision", (object?)hole.ServerRevision ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@PendingSync", hole.PendingSync ? 1 : 0);

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

            await QueueOutboxAsync(connection, transaction, nameof(Hole), hole, "upsert");
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
                MarkEntityForDelete(item);

                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = @"
                    UPDATE Round
                    SET IsDeleted = 1,
                        DeletedAtUtc = @DeletedAtUtc,
                        SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                        PendingSync = 1,
                        PublicId = @PublicId
                    WHERE ID = @ID";
                deleteCmd.Parameters.AddWithValue("@ID", item.ID);
                deleteCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(item.DeletedAtUtc ?? DateTime.UtcNow));
                deleteCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(item.SyncUpdatedAtUtc));
                deleteCmd.Parameters.AddWithValue("@PublicId", item.PublicId);

                var result = await deleteCmd.ExecuteNonQueryAsync();

                var selectHolesCmd = connection.CreateCommand();
                selectHolesCmd.Transaction = transaction;
                selectHolesCmd.CommandText = @"
                    SELECT ID, RoundID, HoleNumber, Par, Yardage, Score, IsScored, Putts,
                           FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Proximity, Notes,
                           CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
                    FROM Hole
                    WHERE RoundID = @RoundID AND IsDeleted = 0";
                selectHolesCmd.Parameters.AddWithValue("@RoundID", item.ID);

                var holes = new List<Hole>();
                await using (var reader = await selectHolesCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
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
                            Penalties = reader.GetInt32(12),
                            Notes = reader.IsDBNull(14) ? null : reader.GetString(14),
                            CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(15)),
                            UpdatedAt = DatabaseDateTime.ParseUtc(reader.GetString(16)),
                            PublicId = reader.GetString(17),
                            SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(18)),
                            IsDeleted = reader.GetInt32(19) == 1,
                            DeletedAtUtc = reader.IsDBNull(20) ? null : DatabaseDateTime.ParseUtc(reader.GetString(20)),
                            ServerRevision = reader.IsDBNull(21) ? null : reader.GetString(21),
                            PendingSync = reader.GetInt32(22) == 1
                        });
                    }
                }

                foreach (var hole in holes)
                {
                    MarkEntityForDelete(hole);

                    var deleteHoleCmd = connection.CreateCommand();
                    deleteHoleCmd.Transaction = transaction;
                    deleteHoleCmd.CommandText = @"
                        UPDATE Hole
                        SET IsDeleted = 1,
                            DeletedAtUtc = @DeletedAtUtc,
                            SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                            PendingSync = 1
                        WHERE ID = @ID";
                    deleteHoleCmd.Parameters.AddWithValue("@ID", hole.ID);
                    deleteHoleCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(hole.DeletedAtUtc ?? DateTime.UtcNow));
                    deleteHoleCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(hole.SyncUpdatedAtUtc));
                    await deleteHoleCmd.ExecuteNonQueryAsync();
                    await QueueOutboxAsync(connection, transaction, nameof(Hole), hole, "delete");

                    var segments = await GetShotSegmentsForHoleAsync(connection, hole.ID, transaction);
                    foreach (var segment in segments)
                    {
                        MarkEntityForDelete(segment);

                        var deleteSegmentCmd = connection.CreateCommand();
                        deleteSegmentCmd.Transaction = transaction;
                        deleteSegmentCmd.CommandText = @"
                            UPDATE ShotSegment
                            SET IsDeleted = 1,
                                DeletedAtUtc = @DeletedAtUtc,
                                SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                                PendingSync = 1
                            WHERE ID = @ID";
                        deleteSegmentCmd.Parameters.AddWithValue("@ID", segment.ID);
                        deleteSegmentCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(segment.DeletedAtUtc ?? DateTime.UtcNow));
                        deleteSegmentCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(segment.SyncUpdatedAtUtc));
                        await deleteSegmentCmd.ExecuteNonQueryAsync();
                        await QueueOutboxAsync(connection, transaction, nameof(ShotSegment), segment, "delete");
                    }
                }

                await QueueOutboxAsync(connection, transaction, nameof(Round), item, "delete");
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
