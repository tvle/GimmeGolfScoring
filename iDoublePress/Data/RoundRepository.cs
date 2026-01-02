using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing golf rounds in the database.
/// </summary>
public class RoundRepository
{
    private bool _hasBeenInitialized = false;
    private readonly ILogger _logger;
    private readonly CourseRepository _courseRepository;
    private readonly PlayerRepository _playerRepository;

    public RoundRepository(CourseRepository courseRepository, PlayerRepository playerRepository, ILogger<RoundRepository> logger)
    {
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _logger = logger;
    }

    private async Task Init()
    {
        if (_hasBeenInitialized)
            return;

        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        try
        {
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
            await createRoundTableCmd.ExecuteNonQueryAsync();

            var createHoleTableCmd = connection.CreateCommand();
            createHoleTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Hole (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoundID INTEGER NOT NULL,
                    HoleNumber INTEGER NOT NULL,
                    Par INTEGER NOT NULL,
                    Score INTEGER DEFAULT 0,
                    IsScored INTEGER DEFAULT 0,
                    Putts INTEGER,
                    FairwayHit INTEGER,
                    GreenInRegulation INTEGER,
                    Penalties INTEGER DEFAULT 0,
                    Notes TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    FOREIGN KEY (RoundID) REFERENCES Round(ID) ON DELETE CASCADE,
                    UNIQUE(RoundID, HoleNumber)
                );";
            await createHoleTableCmd.ExecuteNonQueryAsync();

            // Migration: Add IsScored column if it doesn't exist
            var checkColumnCmd = connection.CreateCommand();
            checkColumnCmd.CommandText = "PRAGMA table_info(Hole);";

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await checkColumnCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            // Ensure Putts exists (older installs might not have it)
            if (!existingColumns.Contains("Putts"))
            {
                _logger.LogInformation("Adding Putts column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN Putts INTEGER;";
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("IsScored"))
            {
                _logger.LogInformation("Adding IsScored column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN IsScored INTEGER DEFAULT 0;";
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            if (!existingColumns.Contains("FairwayResult"))
            {
                _logger.LogInformation("Adding FairwayResult column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN FairwayResult INTEGER DEFAULT 0;";
                await addColumnCmd.ExecuteNonQueryAsync();

                // Best-effort migration from legacy FairwayHit (nullable bool)
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
                    await migrateCmd.ExecuteNonQueryAsync();
                }
            }

            if (!existingColumns.Contains("FairwayMissPenalty"))
            {
                _logger.LogInformation("Adding FairwayMissPenalty column to Hole table");
                var addColumnCmd = connection.CreateCommand();
                addColumnCmd.CommandText = "ALTER TABLE Hole ADD COLUMN FairwayMissPenalty INTEGER DEFAULT 0;";
                await addColumnCmd.ExecuteNonQueryAsync();
            }

            // Create indexes
            var createIndexes = connection.CreateCommand();
            createIndexes.CommandText = @"
                CREATE INDEX IF NOT EXISTS IDX_Round_PlayerID ON Round(PlayerID);
                CREATE INDEX IF NOT EXISTS IDX_Round_CourseID ON Round(CourseID);
                CREATE INDEX IF NOT EXISTS IDX_Round_StartTime ON Round(StartTime DESC);
                CREATE INDEX IF NOT EXISTS IDX_Round_Status ON Round(Status);
                CREATE INDEX IF NOT EXISTS IDX_Hole_RoundID ON Hole(RoundID);";
            await createIndexes.ExecuteNonQueryAsync();
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

        // Load related data
        round.Player = await _playerRepository.GetAsync(round.PlayerID);
        round.Course = await _courseRepository.GetAsync(round.CourseID);
        round.Holes = await GetHolesAsync(connection, round.ID);

        return round;
    }

    private async Task<List<Hole>> GetHolesAsync(SqliteConnection connection, int roundId)
    {
        var selectHolesCmd = connection.CreateCommand();
        selectHolesCmd.CommandText = @"
            SELECT ID, RoundID, HoleNumber, Par, Score, IsScored, Putts,
                   FairwayHit, FairwayResult, FairwayMissPenalty,
                   GreenInRegulation, Penalties, Notes, CreatedAt, UpdatedAt 
            FROM Hole 
            WHERE RoundID = @roundId 
            ORDER BY HoleNumber";
        selectHolesCmd.Parameters.AddWithValue("@roundId", roundId);

        var holes = new List<Hole>();
        await using var reader = await selectHolesCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // Column indices:
            // 0 ID
            // 1 RoundID
            // 2 HoleNumber
            // 3 Par
            // 4 Score
            // 5 IsScored
            // 6 Putts
            // 7 FairwayHit (legacy)
            // 8 FairwayResult
            // 9 FairwayMissPenalty
            // 10 GreenInRegulation
            // 11 Penalties
            // 12 Notes
            // 13 CreatedAt
            // 14 UpdatedAt

            var fairwayResult = FairwayResult.None;
            if (!reader.IsDBNull(8))
            {
                fairwayResult = (FairwayResult)reader.GetInt32(8);
            }
            else if (!reader.IsDBNull(7))
            {
                // legacy fallback (FairwayHit = 1 => Fairway)
                fairwayResult = reader.GetInt32(7) == 1 ? FairwayResult.Fairway : FairwayResult.None;
            }

            holes.Add(new Hole
            {
                ID = reader.GetInt32(0),
                RoundID = reader.GetInt32(1),
                HoleNumber = reader.GetInt32(2),
                Par = reader.GetInt32(3),
                Score = reader.GetInt32(4),
                IsScored = reader.GetInt32(5) == 1,
                Putts = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                FairwayResult = fairwayResult,
                FairwayMissPenalty = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                GreenInRegulation = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
                Penalties = reader.GetInt32(11),
                Notes = reader.IsDBNull(12) ? null : reader.GetString(12),
                CreatedAt = DateTime.Parse(reader.GetString(13)),
                UpdatedAt = DateTime.Parse(reader.GetString(14))
            });
        }

        return holes;
    }

    public async Task<Round> CreateNewRoundAsync(int playerId, int courseId)
    {
        await Init();
        
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

        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        // Insert round
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

        var result = await insertRoundCmd.ExecuteScalarAsync();
        round.ID = Convert.ToInt32(result);

        // Create holes from course
        foreach (var courseHole in course.CourseHoles.OrderBy(h => h.HoleNumber))
        {
            var hole = new Hole
            {
                RoundID = round.ID,
                HoleNumber = courseHole.HoleNumber,
                Par = courseHole.Par,
                Score = courseHole.Par,
                IsScored = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await SaveHoleAsync(connection, hole);
            round.Holes.Add(hole);
        }

        return round;
    }

    public async Task<int> SaveItemAsync(Round item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        item.UpdatedAt = DateTime.Now;

        // Calculate total score from only scored holes
        item.TotalScore = item.Holes.Where(h => h.IsScored).Sum(h => h.Score);

        var saveCmd = connection.CreateCommand();
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

        await saveCmd.ExecuteNonQueryAsync();

        // Save holes
        foreach (var hole in item.Holes)
        {
            await SaveHoleAsync(connection, hole);
        }

        return item.ID;
    }

    public async Task SaveHoleAsync(Hole hole)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        await SaveHoleAsync(connection, hole);
    }

    private async Task SaveHoleAsync(SqliteConnection connection, Hole hole)
    {
        hole.UpdatedAt = DateTime.Now;

        var saveCmd = connection.CreateCommand();
        if (hole.ID == 0)
        {
            hole.CreatedAt = DateTime.Now;
            
            saveCmd.CommandText = @"
                INSERT INTO Hole (RoundID, HoleNumber, Par, Score, IsScored, Putts, FairwayHit, FairwayResult, FairwayMissPenalty, GreenInRegulation, Penalties, Notes, CreatedAt, UpdatedAt)
                VALUES (@RoundID, @HoleNumber, @Par, @Score, @IsScored, @Putts, @FairwayHit, @FairwayResult, @FairwayMissPenalty, @GreenInRegulation, @Penalties, @Notes, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
        }
        else
        {
            saveCmd.CommandText = @"
                UPDATE Hole
                SET RoundID = @RoundID, HoleNumber = @HoleNumber, Par = @Par, Score = @Score, IsScored = @IsScored, Putts = @Putts,
                    FairwayHit = @FairwayHit,
                    FairwayResult = @FairwayResult,
                    FairwayMissPenalty = @FairwayMissPenalty,
                    GreenInRegulation = @GreenInRegulation,
                    Penalties = @Penalties, Notes = @Notes, UpdatedAt = @UpdatedAt
                WHERE ID = @ID";
            saveCmd.Parameters.AddWithValue("@ID", hole.ID);
        }

        saveCmd.Parameters.AddWithValue("@RoundID", hole.RoundID);
        saveCmd.Parameters.AddWithValue("@HoleNumber", hole.HoleNumber);
        saveCmd.Parameters.AddWithValue("@Par", hole.Par);
        saveCmd.Parameters.AddWithValue("@Score", hole.Score);
        saveCmd.Parameters.AddWithValue("@IsScored", hole.IsScored ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@Putts", (object?)hole.Putts ?? DBNull.Value);

        // Keep legacy column populated for older app versions / compatibility.
        saveCmd.Parameters.AddWithValue("@FairwayHit", hole.FairwayResult == FairwayResult.Fairway ? 1 : 0);

        saveCmd.Parameters.AddWithValue("@FairwayResult", (int)hole.FairwayResult);
        saveCmd.Parameters.AddWithValue("@FairwayMissPenalty", hole.FairwayMissPenalty ? 1 : 0);

        saveCmd.Parameters.AddWithValue("@GreenInRegulation", hole.GreenInRegulation.HasValue ? (hole.GreenInRegulation.Value ? 1 : 0) : DBNull.Value);
        saveCmd.Parameters.AddWithValue("@Penalties", hole.Penalties);
        saveCmd.Parameters.AddWithValue("@Notes", (object?)hole.Notes ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@CreatedAt", hole.CreatedAt.ToString("o"));
        saveCmd.Parameters.AddWithValue("@UpdatedAt", hole.UpdatedAt.ToString("o"));

        var result = await saveCmd.ExecuteScalarAsync();
        if (hole.ID == 0)
        {
            hole.ID = Convert.ToInt32(result);
        }
    }

    public async Task<int> DeleteItemAsync(Round item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Round WHERE ID = @ID";
        deleteCmd.Parameters.AddWithValue("@ID", item.ID);

        return await deleteCmd.ExecuteNonQueryAsync();
    }
}
