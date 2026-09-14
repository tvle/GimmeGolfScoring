using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing golf courses in the database.
/// </summary>
public class CourseRepository : RepositoryBase
{

    public CourseRepository(ILogger<CourseRepository> logger) : base(logger)
    {
    }

    private async Task Init()
    {
        await EnsureInitializedAsync();
    }

    protected override async Task InitializeInternalAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<List<Course>> ListAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, Name, Location, TotalPar, Holes, Rating, Slope, IsCustom, CreatedAt,
                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Course
            WHERE IsDeleted = 0
            ORDER BY Name";
        var courses = new List<Course>();

        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var course = new Course
            {
                ID = reader.GetInt32(0),
                Name = reader.GetString(1),
                Location = reader.IsDBNull(2) ? null : reader.GetString(2),
                TotalPar = reader.GetInt32(3),
                Holes = reader.GetInt32(4),
                Rating = reader.IsDBNull(5) ? null : (decimal?)reader.GetDouble(5),
                Slope = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                IsCustom = reader.GetInt32(7) == 1,
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(8)),
                PublicId = reader.GetString(9),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(10)),
                IsDeleted = reader.GetInt32(11) == 1,
                DeletedAtUtc = reader.IsDBNull(12) ? null : DatabaseDateTime.ParseUtc(reader.GetString(12)),
                ServerRevision = reader.IsDBNull(13) ? null : reader.GetString(13),
                PendingSync = reader.GetInt32(14) == 1
            };

            course.CourseHoles = await GetCourseHolesAsync(connection, course.ID);
            courses.Add(course);
        }

        return courses;
    }

    public async Task<Course?> GetAsync(int id, bool includeDeleted = false)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = $@"
            SELECT ID, Name, Location, TotalPar, Holes, Rating, Slope, IsCustom, CreatedAt,
                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Course
            WHERE ID = @id {(includeDeleted ? string.Empty : "AND IsDeleted = 0")}";
        selectCmd.Parameters.AddWithValue("@id", id);

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var course = new Course
            {
                ID = reader.GetInt32(0),
                Name = reader.GetString(1),
                Location = reader.IsDBNull(2) ? null : reader.GetString(2),
                TotalPar = reader.GetInt32(3),
                Holes = reader.GetInt32(4),
                Rating = reader.IsDBNull(5) ? null : (decimal?)reader.GetDouble(5),
                Slope = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                IsCustom = reader.GetInt32(7) == 1,
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(8)),
                PublicId = reader.GetString(9),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(10)),
                IsDeleted = reader.GetInt32(11) == 1,
                DeletedAtUtc = reader.IsDBNull(12) ? null : DatabaseDateTime.ParseUtc(reader.GetString(12)),
                ServerRevision = reader.IsDBNull(13) ? null : reader.GetString(13),
                PendingSync = reader.GetInt32(14) == 1
            };

            course.CourseHoles = await GetCourseHolesAsync(connection, course.ID, includeDeleted);
            return course;
        }

        return null;
    }

    private async Task<List<CourseHole>> GetCourseHolesAsync(SqliteConnection connection, int courseId, bool includeDeleted = false, SqliteTransaction? transaction = null)
    {
        var selectHolesCmd = connection.CreateCommand();
        selectHolesCmd.Transaction = transaction;
        selectHolesCmd.CommandText = $@"
            SELECT ID, CourseID, HoleNumber, Par, Handicap, Yardage,
                   PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM CourseHole
            WHERE CourseID = @courseId {(includeDeleted ? string.Empty : "AND IsDeleted = 0")}
            ORDER BY HoleNumber";
        selectHolesCmd.Parameters.AddWithValue("@courseId", courseId);

        var holes = new List<CourseHole>();
        await using var reader = await selectHolesCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            holes.Add(new CourseHole
            {
                ID = reader.GetInt32(0),
                CourseID = reader.GetInt32(1),
                HoleNumber = reader.GetInt32(2),
                Par = reader.GetInt32(3),
                Handicap = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Yardage = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                PublicId = reader.GetString(6),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(7)),
                IsDeleted = reader.GetInt32(8) == 1,
                DeletedAtUtc = reader.IsDBNull(9) ? null : DatabaseDateTime.ParseUtc(reader.GetString(9)),
                ServerRevision = reader.IsDBNull(10) ? null : reader.GetString(10),
                PendingSync = reader.GetInt32(11) == 1
            });
        }

        return holes;
    }

    public async Task<int> SaveItemAsync(Course item)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        using var transaction = connection.BeginTransaction();
        var saveCmd = connection.CreateCommand();
        saveCmd.Transaction = transaction;
        if (item.ID == 0)
        {
            item.CreatedAt = DatabaseDateTime.EnsureUtc(item.CreatedAt);
            MarkEntityForUpsert(item);

            saveCmd.CommandText = @"
                INSERT INTO Course (Name, Location, TotalPar, Holes, Rating, Slope, IsCustom, CreatedAt,
                                    PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                VALUES (@Name, @Location, @TotalPar, @Holes, @Rating, @Slope, @IsCustom, @CreatedAt,
                        @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                SELECT last_insert_rowid();";
        }
        else
        {
            item.CreatedAt = DatabaseDateTime.EnsureUtc(item.CreatedAt);
            MarkEntityForUpsert(item);
            saveCmd.CommandText = @"
                UPDATE Course
                SET Name = @Name, Location = @Location, TotalPar = @TotalPar, Holes = @Holes,
                    Rating = @Rating, Slope = @Slope, IsCustom = @IsCustom,
                    PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc, IsDeleted = @IsDeleted,
                    DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
                WHERE ID = @ID";
            saveCmd.Parameters.AddWithValue("@ID", item.ID);
        }

        saveCmd.Parameters.AddWithValue("@Name", item.Name);
        saveCmd.Parameters.AddWithValue("@Location", (object?)item.Location ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@TotalPar", item.TotalPar);
        saveCmd.Parameters.AddWithValue("@Holes", item.Holes);
        saveCmd.Parameters.AddWithValue("@Rating", item.Rating.HasValue ? (double)item.Rating.Value : DBNull.Value);
        saveCmd.Parameters.AddWithValue("@Slope", (object?)item.Slope ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@IsCustom", item.IsCustom ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@CreatedAt", DatabaseDateTime.ToUtcString(item.CreatedAt));
        saveCmd.Parameters.AddWithValue("@PublicId", item.PublicId);
        saveCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(item.SyncUpdatedAtUtc));
        saveCmd.Parameters.AddWithValue("@IsDeleted", item.IsDeleted ? 1 : 0);
        saveCmd.Parameters.AddWithValue("@DeletedAtUtc", item.DeletedAtUtc.HasValue ? DatabaseDateTime.ToUtcString(item.DeletedAtUtc.Value) : DBNull.Value);
        saveCmd.Parameters.AddWithValue("@ServerRevision", (object?)item.ServerRevision ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@PendingSync", item.PendingSync ? 1 : 0);

        var result = await saveCmd.ExecuteScalarAsync();
        if (item.ID == 0)
        {
            item.ID = Convert.ToInt32(result);
        }

        await QueueOutboxAsync(connection, transaction, nameof(Course), item, "upsert");
        await SaveCourseHolesAsync(connection, transaction, item);
        transaction.Commit();

        return item.ID;
    }

    private async Task SaveCourseHolesAsync(SqliteConnection connection, SqliteTransaction transaction, Course course)
    {
        var existingHoles = new Dictionary<int, CourseHole>();
        var loadExistingCmd = connection.CreateCommand();
        loadExistingCmd.Transaction = transaction;
        loadExistingCmd.CommandText = @"
            SELECT ID, HoleNumber, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM CourseHole
            WHERE CourseID = @courseId";
        loadExistingCmd.Parameters.AddWithValue("@courseId", course.ID);

        await using (var reader = await loadExistingCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                existingHoles[reader.GetInt32(1)] = new CourseHole
                {
                    ID = reader.GetInt32(0),
                    CourseID = course.ID,
                    HoleNumber = reader.GetInt32(1),
                    PublicId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    SyncUpdatedAtUtc = reader.IsDBNull(3) ? DateTime.UtcNow : DatabaseDateTime.ParseUtc(reader.GetString(3)),
                    IsDeleted = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                    DeletedAtUtc = reader.IsDBNull(5) ? null : DatabaseDateTime.ParseUtc(reader.GetString(5)),
                    ServerRevision = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PendingSync = !reader.IsDBNull(7) && reader.GetInt32(7) == 1
                };
            }
        }

        var retainedHoleNumbers = new HashSet<int>();
        foreach (var hole in course.CourseHoles)
        {
            hole.CourseID = course.ID;
            retainedHoleNumbers.Add(hole.HoleNumber);

            if (existingHoles.TryGetValue(hole.HoleNumber, out var existingHole))
            {
                hole.ID = existingHole.ID;
                hole.PublicId = existingHole.PublicId;
                hole.ServerRevision = existingHole.ServerRevision;
            }

            MarkEntityForUpsert(hole);

            var saveCmd = connection.CreateCommand();
            saveCmd.Transaction = transaction;
            if (hole.ID == 0)
            {
                saveCmd.CommandText = @"
                    INSERT INTO CourseHole (CourseID, HoleNumber, Par, Handicap, Yardage,
                                            PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                    VALUES (@CourseID, @HoleNumber, @Par, @Handicap, @Yardage,
                            @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                    SELECT last_insert_rowid();";
            }
            else
            {
                saveCmd.CommandText = @"
                    UPDATE CourseHole
                    SET CourseID = @CourseID, HoleNumber = @HoleNumber, Par = @Par, Handicap = @Handicap, Yardage = @Yardage,
                        PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc, IsDeleted = @IsDeleted,
                        DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
                    WHERE ID = @ID";
                saveCmd.Parameters.AddWithValue("@ID", hole.ID);
            }

            saveCmd.Parameters.AddWithValue("@CourseID", hole.CourseID);
            saveCmd.Parameters.AddWithValue("@HoleNumber", hole.HoleNumber);
            saveCmd.Parameters.AddWithValue("@Par", hole.Par);
            saveCmd.Parameters.AddWithValue("@Handicap", (object?)hole.Handicap ?? DBNull.Value);
            saveCmd.Parameters.AddWithValue("@Yardage", (object?)hole.Yardage ?? DBNull.Value);
            saveCmd.Parameters.AddWithValue("@PublicId", hole.PublicId);
            saveCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(hole.SyncUpdatedAtUtc));
            saveCmd.Parameters.AddWithValue("@IsDeleted", hole.IsDeleted ? 1 : 0);
            saveCmd.Parameters.AddWithValue("@DeletedAtUtc", hole.DeletedAtUtc.HasValue ? DatabaseDateTime.ToUtcString(hole.DeletedAtUtc.Value) : DBNull.Value);
            saveCmd.Parameters.AddWithValue("@ServerRevision", (object?)hole.ServerRevision ?? DBNull.Value);
            saveCmd.Parameters.AddWithValue("@PendingSync", hole.PendingSync ? 1 : 0);

            var result = await saveCmd.ExecuteScalarAsync();
            if (hole.ID == 0)
                hole.ID = Convert.ToInt32(result);

            await QueueOutboxAsync(connection, transaction, nameof(CourseHole), hole, "upsert");
        }

        foreach (var existingHole in existingHoles.Values.Where(h => !retainedHoleNumbers.Contains(h.HoleNumber) && !h.IsDeleted))
        {
            MarkEntityForDelete(existingHole);

            var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = @"
                UPDATE CourseHole
                SET IsDeleted = 1,
                    DeletedAtUtc = @DeletedAtUtc,
                    SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                    PendingSync = 1
                WHERE ID = @ID";
            deleteCmd.Parameters.AddWithValue("@ID", existingHole.ID);
            deleteCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(existingHole.DeletedAtUtc ?? DateTime.UtcNow));
            deleteCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(existingHole.SyncUpdatedAtUtc));
            await deleteCmd.ExecuteNonQueryAsync();

            await QueueOutboxAsync(connection, transaction, nameof(CourseHole), existingHole, "delete");
        }
    }

    public async Task<int> DeleteItemAsync(Course item)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        using var transaction = connection.BeginTransaction();
        MarkEntityForDelete(item);

        var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = @"
            UPDATE Course
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

        var courseRows = await deleteCmd.ExecuteNonQueryAsync();

        var holes = await GetCourseHolesAsync(connection, item.ID, includeDeleted: true, transaction: transaction);
        foreach (var hole in holes.Where(h => !h.IsDeleted))
        {
            MarkEntityForDelete(hole);

            var holeDeleteCmd = connection.CreateCommand();
            holeDeleteCmd.Transaction = transaction;
            holeDeleteCmd.CommandText = @"
                UPDATE CourseHole
                SET IsDeleted = 1,
                    DeletedAtUtc = @DeletedAtUtc,
                    SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                    PendingSync = 1
                WHERE ID = @ID";
            holeDeleteCmd.Parameters.AddWithValue("@ID", hole.ID);
            holeDeleteCmd.Parameters.AddWithValue("@DeletedAtUtc", DatabaseDateTime.ToUtcString(hole.DeletedAtUtc ?? DateTime.UtcNow));
            holeDeleteCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", DatabaseDateTime.ToUtcString(hole.SyncUpdatedAtUtc));
            await holeDeleteCmd.ExecuteNonQueryAsync();

            await QueueOutboxAsync(connection, transaction, nameof(CourseHole), hole, "delete");
        }

        await QueueOutboxAsync(connection, transaction, nameof(Course), item, "delete");
        transaction.Commit();
        return courseRows;
    }
}
