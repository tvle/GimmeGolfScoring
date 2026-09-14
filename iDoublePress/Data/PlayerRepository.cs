using iDoublePress.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace iDoublePress.Data;

/// <summary>
/// Repository class for managing players in the database.
/// </summary>
public class PlayerRepository : RepositoryBase
{

    public PlayerRepository(ILogger<PlayerRepository> logger) : base(logger)
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

    public async Task<List<Player>> ListAsync()
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT ID, Name, Handicap, Email, CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Player
            WHERE IsDeleted = 0
            ORDER BY Name";
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
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(4)),
                UpdatedAt = DatabaseDateTime.ParseUtc(reader.GetString(5)),
                PublicId = reader.GetString(6),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(7)),
                IsDeleted = reader.GetInt32(8) == 1,
                DeletedAtUtc = reader.IsDBNull(9) ? null : DatabaseDateTime.ParseUtc(reader.GetString(9)),
                ServerRevision = reader.IsDBNull(10) ? null : reader.GetString(10),
                PendingSync = reader.GetInt32(11) == 1
            });
        }

        return players;
    }

    public async Task<Player?> GetAsync(int id, bool includeDeleted = false)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = $@"
            SELECT ID, Name, Handicap, Email, CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync
            FROM Player
            WHERE ID = @id {(includeDeleted ? string.Empty : "AND IsDeleted = 0")}";
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
                CreatedAt = DatabaseDateTime.ParseUtc(reader.GetString(4)),
                UpdatedAt = DatabaseDateTime.ParseUtc(reader.GetString(5)),
                PublicId = reader.GetString(6),
                SyncUpdatedAtUtc = DatabaseDateTime.ParseUtc(reader.GetString(7)),
                IsDeleted = reader.GetInt32(8) == 1,
                DeletedAtUtc = reader.IsDBNull(9) ? null : DatabaseDateTime.ParseUtc(reader.GetString(9)),
                ServerRevision = reader.IsDBNull(10) ? null : reader.GetString(10),
                PendingSync = reader.GetInt32(11) == 1
            };
        }

        return null;
    }

    public async Task<int> SaveItemAsync(Player item)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        using var transaction = connection.BeginTransaction();
        var saveCmd = connection.CreateCommand();
        saveCmd.Transaction = transaction;
        if (item.ID == 0)
        {
            item.CreatedAt = DatabaseDateTime.EnsureUtc(item.CreatedAt);
            item.UpdatedAt = item.CreatedAt;
            MarkEntityForUpsert(item);

            saveCmd.CommandText = @"
                INSERT INTO Player (Name, Handicap, Email, CreatedAt, UpdatedAt, PublicId, SyncUpdatedAtUtc, IsDeleted, DeletedAtUtc, ServerRevision, PendingSync)
                VALUES (@Name, @Handicap, @Email, @CreatedAt, @UpdatedAt, @PublicId, @SyncUpdatedAtUtc, @IsDeleted, @DeletedAtUtc, @ServerRevision, @PendingSync);
                SELECT last_insert_rowid();";
        }
        else
        {
            item.CreatedAt = DatabaseDateTime.EnsureUtc(item.CreatedAt);
            item.UpdatedAt = DateTime.UtcNow;
            MarkEntityForUpsert(item);

            saveCmd.CommandText = @"
                UPDATE Player
                SET Name = @Name, Handicap = @Handicap, Email = @Email, UpdatedAt = @UpdatedAt,
                    PublicId = @PublicId, SyncUpdatedAtUtc = @SyncUpdatedAtUtc, IsDeleted = @IsDeleted,
                    DeletedAtUtc = @DeletedAtUtc, ServerRevision = @ServerRevision, PendingSync = @PendingSync
                WHERE ID = @ID";
            saveCmd.Parameters.AddWithValue("@ID", item.ID);
        }

        saveCmd.Parameters.AddWithValue("@Name", item.Name);
        saveCmd.Parameters.AddWithValue("@Handicap", (double)item.Handicap);
        saveCmd.Parameters.AddWithValue("@Email", (object?)item.Email ?? DBNull.Value);
        saveCmd.Parameters.AddWithValue("@CreatedAt", DatabaseDateTime.ToUtcString(item.CreatedAt));
        saveCmd.Parameters.AddWithValue("@UpdatedAt", DatabaseDateTime.ToUtcString(item.UpdatedAt));
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

        await QueueOutboxAsync(connection, transaction, nameof(Player), item, "upsert");
        transaction.Commit();

        return item.ID;
    }

    public async Task<int> DeleteItemAsync(Player item)
    {
        await EnsureInitializedAsync();
        await using var connection = await CreateConnectionAsync();

        using var transaction = connection.BeginTransaction();
        MarkEntityForDelete(item);

        var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = @"
            UPDATE Player
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

        var rows = await deleteCmd.ExecuteNonQueryAsync();
        await QueueOutboxAsync(connection, transaction, nameof(Player), item, "delete");
        transaction.Commit();
        return rows;
    }
}
