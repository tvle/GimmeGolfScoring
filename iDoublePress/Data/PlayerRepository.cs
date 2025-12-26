using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing players in the database.
/// </summary>
public class PlayerRepository
{
    private bool _hasBeenInitialized = false;
    private readonly ILogger _logger;

    public PlayerRepository(ILogger<PlayerRepository> logger)
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
            var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Player (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Handicap REAL DEFAULT 0,
                    Email TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );";
            await createTableCmd.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating Player table");
            throw;
        }

        _hasBeenInitialized = true;
    }

    public async Task<List<Player>> ListAsync()
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Player ORDER BY Name";
        var players = new List<Player>();

        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            players.Add(new Player
            {
                ID = reader.GetInt32(0),
                Name = reader.GetString(1),
                Handicap = (decimal)reader.GetDouble(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            });
        }

        return players;
    }

    public async Task<Player?> GetAsync(int id)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM Player WHERE ID = @id";
        selectCmd.Parameters.AddWithValue("@id", id);

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Player
            {
                ID = reader.GetInt32(0),
                Name = reader.GetString(1),
                Handicap = (decimal)reader.GetDouble(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            };
        }

        return null;
    }

    public async Task<int> SaveItemAsync(Player item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var saveCmd = connection.CreateCommand();
        if (item.ID == 0)
        {
            item.CreatedAt = DateTime.Now;
            item.UpdatedAt = DateTime.Now;

            saveCmd.CommandText = @"
                INSERT INTO Player (Name, Handicap, Email, CreatedAt, UpdatedAt)
                VALUES (@Name, @Handicap, @Email, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
        }
        else
        {
            item.UpdatedAt = DateTime.Now;

            saveCmd.CommandText = @"
                UPDATE Player
                SET Name = @Name, Handicap = @Handicap, Email = @Email, UpdatedAt = @UpdatedAt
                WHERE ID = @ID";
            saveCmd.Parameters.AddWithValue("@ID", item.ID);
        }

        saveCmd.Parameters.AddWithValue("@Name", item.Name);
        saveCmd.Parameters.AddWithValue("@Handicap", (double)item.Handicap);
        saveCmd.Parameters.AddWithValue("@Email", (object?)item.Email ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("o"));
        saveCmd.Parameters.AddWithValue("@UpdatedAt", item.UpdatedAt.ToString("o"));

        var result = await saveCmd.ExecuteScalarAsync();
        if (item.ID == 0)
        {
            item.ID = Convert.ToInt32(result);
        }

        return item.ID;
    }

    public async Task<int> DeleteItemAsync(Player item)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Player WHERE ID = @ID";
        deleteCmd.Parameters.AddWithValue("@ID", item.ID);

        return await deleteCmd.ExecuteNonQueryAsync();
    }
}
