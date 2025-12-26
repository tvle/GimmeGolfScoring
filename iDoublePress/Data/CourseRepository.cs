using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing golf courses in the database.
/// </summary>
public class CourseRepository
{
    private bool _hasBeenInitialized = false;
    private readonly ILogger _logger;

    public CourseRepository(ILogger<CourseRepository> logger)
    {
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
            var createCourseTableCmd = connection.CreateCommand();
            createCourseTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Course (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Location TEXT,
                    TotalPar INTEGER NOT NULL,
                    Holes INTEGER DEFAULT 18,
                    Rating REAL,
                    Slope INTEGER,
                    IsCustom INTEGER DEFAULT 0,
                    CreatedAt TEXT NOT NULL
                );";
            await createCourseTableCmd.ExecuteNonQueryAsync();

            var createCourseHoleTableCmd = connection.CreateCommand();
            createCourseHoleTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS CourseHole (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CourseID INTEGER NOT NULL,
                    HoleNumber INTEGER NOT NULL,
                    Par INTEGER NOT NULL,
                    Handicap INTEGER,
                    Yardage INTEGER,
                    FOREIGN KEY (CourseID) REFERENCES Course(ID) ON DELETE CASCADE,
                    UNIQUE(CourseID, HoleNumber)
                );";
            await createCourseHoleTableCmd.ExecuteNonQueryAsync();

            var createIndexCmd = connection.CreateCommand();
            createIndexCmd.CommandText = "CREATE INDEX IF NOT EXISTS IDX_CourseHole_CourseID ON CourseHole(CourseID);";
            await createIndexCmd.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating Course tables");
            throw;
        }

        _hasBeenInitialized = true;
    }

    public async Task<List<Course>> ListAsync()
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Course ORDER BY Name";
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
                CreatedAt = DateTime.Parse(reader.GetString(8))
            };

            course.CourseHoles = await GetCourseHolesAsync(connection, course.ID);
            courses.Add(course);
        }

        return courses;
    }

    public async Task<Course?> GetAsync(int id)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Course WHERE ID = @id";
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
                CreatedAt = DateTime.Parse(reader.GetString(8))
            };

            course.CourseHoles = await GetCourseHolesAsync(connection, course.ID);
            return course;
        }

        return null;
    }

    private async Task<List<CourseHole>> GetCourseHolesAsync(SqliteConnection connection, int courseId)
    {
        var selectHolesCmd = connection.CreateCommand();
        selectHolesCmd.CommandText = "SELECT * FROM CourseHole WHERE CourseID = @courseId ORDER BY HoleNumber";
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
                Yardage = reader.IsDBNull(5) ? null : reader.GetInt32(5)
            });
        }

        return holes;
    }

    public async Task<int> SaveItemAsync(Course item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var saveCmd = connection.CreateCommand();
        if (item.ID == 0)
        {
            item.CreatedAt = DateTime.Now;

            saveCmd.CommandText = @"
                INSERT INTO Course (Name, Location, TotalPar, Holes, Rating, Slope, IsCustom, CreatedAt)
                VALUES (@Name, @Location, @TotalPar, @Holes, @Rating, @Slope, @IsCustom, @CreatedAt);
                SELECT last_insert_rowid();";
        }
        else
        {
            saveCmd.CommandText = @"
                UPDATE Course
                SET Name = @Name, Location = @Location, TotalPar = @TotalPar, Holes = @Holes,
                    Rating = @Rating, Slope = @Slope, IsCustom = @IsCustom
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
        saveCmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("o"));

        var result = await saveCmd.ExecuteScalarAsync();
        if (item.ID == 0)
        {
            item.ID = Convert.ToInt32(result);
        }

        // Save course holes
        await SaveCourseHolesAsync(connection, item);

        return item.ID;
    }

    private async Task SaveCourseHolesAsync(SqliteConnection connection, Course course)
    {
        // Delete existing holes
        var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM CourseHole WHERE CourseID = @courseId";
        deleteCmd.Parameters.AddWithValue("@courseId", course.ID);
        await deleteCmd.ExecuteNonQueryAsync();

        // Insert new holes
        foreach (var hole in course.CourseHoles)
        {
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO CourseHole (CourseID, HoleNumber, Par, Handicap, Yardage)
                VALUES (@CourseID, @HoleNumber, @Par, @Handicap, @Yardage);";
            insertCmd.Parameters.AddWithValue("@CourseID", course.ID);
            insertCmd.Parameters.AddWithValue("@HoleNumber", hole.HoleNumber);
            insertCmd.Parameters.AddWithValue("@Par", hole.Par);
            insertCmd.Parameters.AddWithValue("@Handicap", (object?)hole.Handicap ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Yardage", (object?)hole.Yardage ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> DeleteItemAsync(Course item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Course WHERE ID = @ID";
        deleteCmd.Parameters.AddWithValue("@ID", item.ID);

        return await deleteCmd.ExecuteNonQueryAsync();
    }
}
