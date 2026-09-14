using Microsoft.Data.Sqlite;

namespace iDoublePress.Data;

public static class DatabaseMigrator
{
    private const string MigrationTableName = "__SchemaMigration";
    private const string OutboxTableName = "SyncOutbox";

    private static readonly (int Version, Func<SqliteConnection, SqliteTransaction, Task> ApplyAsync)[] Migrations =
    {
        (1, ApplyInitialSchemaAsync),
        (2, ApplySyncMetadataAsync)
    };

    public static async Task ConfigureConnectionAsync(SqliteConnection connection)
    {
        var pragmaFk = connection.CreateCommand();
        pragmaFk.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaFk.ExecuteNonQueryAsync();

        var pragmaBusy = connection.CreateCommand();
        pragmaBusy.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragmaBusy.ExecuteNonQueryAsync();
    }

    public static async Task MigrateAsync(SqliteConnection connection)
    {
        await ConfigureConnectionAsync(connection);
        await EnsureMigrationTableAsync(connection);

        var appliedVersions = await GetAppliedVersionsAsync(connection);
        foreach (var migration in Migrations.OrderBy(m => m.Version))
        {
            if (appliedVersions.Contains(migration.Version))
                continue;

            using var transaction = connection.BeginTransaction();
            try
            {
                await migration.ApplyAsync(connection, transaction);

                var recordMigration = connection.CreateCommand();
                recordMigration.Transaction = transaction;
                recordMigration.CommandText = $@"
                    INSERT INTO {MigrationTableName} (Version, AppliedAtUtc)
                    VALUES (@Version, @AppliedAtUtc);";
                recordMigration.Parameters.AddWithValue("@Version", migration.Version);
                recordMigration.Parameters.AddWithValue("@AppliedAtUtc", DatabaseDateTime.ToUtcString(DateTime.UtcNow));
                await recordMigration.ExecuteNonQueryAsync();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    private static async Task EnsureMigrationTableAsync(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {MigrationTableName} (
                Version INTEGER PRIMARY KEY,
                AppliedAtUtc TEXT NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Version FROM {MigrationTableName};";

        var versions = new HashSet<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task ApplyInitialSchemaAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        var createCmd = connection.CreateCommand();
        createCmd.Transaction = transaction;
        createCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Player (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Handicap REAL DEFAULT 0,
                Email TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

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
            );

            CREATE TABLE IF NOT EXISTS CourseHole (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                CourseID INTEGER NOT NULL,
                HoleNumber INTEGER NOT NULL,
                Par INTEGER NOT NULL,
                Handicap INTEGER,
                Yardage INTEGER,
                FOREIGN KEY (CourseID) REFERENCES Course(ID) ON DELETE CASCADE,
                UNIQUE(CourseID, HoleNumber)
            );

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
            );

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
            );

            CREATE TABLE IF NOT EXISTS ShotSegment (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                HoleID INTEGER NOT NULL,
                Sequence INTEGER NOT NULL,
                Latitude REAL NOT NULL,
                Longitude REAL NOT NULL,
                Tag TEXT,
                CreatedAt TEXT NOT NULL,
                Accuracy REAL,
                FOREIGN KEY (HoleID) REFERENCES Hole(ID) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IDX_CourseHole_CourseID ON CourseHole(CourseID);
            CREATE INDEX IF NOT EXISTS IDX_Round_PlayerID ON Round(PlayerID);
            CREATE INDEX IF NOT EXISTS IDX_Round_COURSEID ON Round(CourseID);
            CREATE INDEX IF NOT EXISTS IDX_Round_StartTime ON Round(StartTime DESC);
            CREATE INDEX IF NOT EXISTS IDX_Round_Status ON Round(Status);
            CREATE INDEX IF NOT EXISTS IDX_Hole_RoundID ON Hole(RoundID);
            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID ON ShotSegment(HoleID);
            CREATE INDEX IF NOT EXISTS IDX_ShotSegment_HoleID_Sequence ON ShotSegment(HoleID, Sequence);";
        await createCmd.ExecuteNonQueryAsync();

        await AddColumnIfMissingAsync(connection, transaction, "Hole", "Putts", "INTEGER");
        await AddColumnIfMissingAsync(connection, transaction, "Hole", "IsScored", "INTEGER DEFAULT 0");
        await AddColumnIfMissingAsync(connection, transaction, "Hole", "FairwayResult", "INTEGER DEFAULT 0");
        await AddColumnIfMissingAsync(connection, transaction, "Hole", "FairwayMissPenalty", "INTEGER DEFAULT 0");
        await AddColumnIfMissingAsync(connection, transaction, "Hole", "Proximity", "TEXT");
        await AddColumnIfMissingAsync(connection, transaction, "Hole", "Yardage", "INTEGER");
        await AddColumnIfMissingAsync(connection, transaction, "ShotSegment", "Tag", "TEXT");
        await AddColumnIfMissingAsync(connection, transaction, "ShotSegment", "Accuracy", "REAL");

        if (await ColumnExistsAsync(connection, transaction, "Hole", "FairwayHit")
            && await ColumnExistsAsync(connection, transaction, "Hole", "FairwayResult"))
        {
            var migrateCmd = connection.CreateCommand();
            migrateCmd.Transaction = transaction;
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

    private static async Task ApplySyncMetadataAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        var tables = new[]
        {
            "Player",
            "Course",
            "CourseHole",
            "Round",
            "Hole",
            "ShotSegment"
        };

        foreach (var table in tables)
        {
            await AddColumnIfMissingAsync(connection, transaction, table, "PublicId", "TEXT");
            await AddColumnIfMissingAsync(connection, transaction, table, "SyncUpdatedAtUtc", "TEXT");
            await AddColumnIfMissingAsync(connection, transaction, table, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync(connection, transaction, table, "DeletedAtUtc", "TEXT");
            await AddColumnIfMissingAsync(connection, transaction, table, "ServerRevision", "TEXT");
            await AddColumnIfMissingAsync(connection, transaction, table, "PendingSync", "INTEGER NOT NULL DEFAULT 0");
        }

        var outboxCmd = connection.CreateCommand();
        outboxCmd.Transaction = transaction;
        outboxCmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {OutboxTableName} (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                EntityType TEXT NOT NULL,
                EntityPublicId TEXT NOT NULL,
                Operation TEXT NOT NULL,
                QueuedAtUtc TEXT NOT NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                LastError TEXT,
                UNIQUE(EntityType, EntityPublicId)
            );

            CREATE INDEX IF NOT EXISTS IDX_{OutboxTableName}_QueuedAtUtc ON {OutboxTableName}(QueuedAtUtc);";
        await outboxCmd.ExecuteNonQueryAsync();

        await BackfillSyncMetadataAsync(connection, transaction, "Player", "UpdatedAt", "CreatedAt");
        await BackfillSyncMetadataAsync(connection, transaction, "Course", null, "CreatedAt");
        await BackfillSyncMetadataAsync(connection, transaction, "CourseHole", null, null);
        await BackfillSyncMetadataAsync(connection, transaction, "Round", "UpdatedAt", "CreatedAt");
        await BackfillSyncMetadataAsync(connection, transaction, "Hole", "UpdatedAt", "CreatedAt");
        await BackfillSyncMetadataAsync(connection, transaction, "ShotSegment", null, "CreatedAt");
    }

    private static async Task BackfillSyncMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string? updatedColumn,
        string? createdColumn)
    {
        var sourceColumns = new List<string> { "ID", "PublicId", "SyncUpdatedAtUtc", "DeletedAtUtc", "IsDeleted" };
        if (!string.IsNullOrWhiteSpace(updatedColumn))
            sourceColumns.Add(updatedColumn);
        if (!string.IsNullOrWhiteSpace(createdColumn) && !sourceColumns.Contains(createdColumn))
            sourceColumns.Add(createdColumn);

        var selectCmd = connection.CreateCommand();
        selectCmd.Transaction = transaction;
        selectCmd.CommandText = $"SELECT {string.Join(", ", sourceColumns)} FROM {tableName};";

        var updates = new List<(long Id, string PublicId, string SyncUpdatedAtUtc, bool IsDeleted, string? DeletedAtUtc)>();
        await using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var publicId = reader.IsDBNull(1) ? null : reader.GetString(1);
                var syncUpdatedAtUtc = reader.IsDBNull(2) ? null : reader.GetString(2);
                var deletedAtUtc = reader.IsDBNull(3) ? null : reader.GetString(3);
                var isDeleted = !reader.IsDBNull(4) && reader.GetInt32(4) == 1;

                if (string.IsNullOrWhiteSpace(publicId))
                    publicId = Guid.NewGuid().ToString("D");

                if (string.IsNullOrWhiteSpace(syncUpdatedAtUtc))
                {
                    syncUpdatedAtUtc = TryReadUtc(reader, sourceColumns, updatedColumn)
                        ?? TryReadUtc(reader, sourceColumns, createdColumn)
                        ?? DatabaseDateTime.ToUtcString(DateTime.UtcNow);
                }

                if (isDeleted && !string.IsNullOrWhiteSpace(deletedAtUtc))
                    deletedAtUtc = DatabaseDateTime.ToUtcString(DatabaseDateTime.ParseUtc(deletedAtUtc));

                updates.Add((id, publicId, syncUpdatedAtUtc, isDeleted, deletedAtUtc));
            }
        }

        foreach (var update in updates)
        {
            var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = $@"
                UPDATE {tableName}
                SET PublicId = @PublicId,
                    SyncUpdatedAtUtc = @SyncUpdatedAtUtc,
                    IsDeleted = @IsDeleted,
                    DeletedAtUtc = @DeletedAtUtc
                WHERE ID = @ID;";
            updateCmd.Parameters.AddWithValue("@ID", update.Id);
            updateCmd.Parameters.AddWithValue("@PublicId", update.PublicId);
            updateCmd.Parameters.AddWithValue("@SyncUpdatedAtUtc", update.SyncUpdatedAtUtc);
            updateCmd.Parameters.AddWithValue("@IsDeleted", update.IsDeleted ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@DeletedAtUtc", (object?)update.DeletedAtUtc ?? DBNull.Value);
            await updateCmd.ExecuteNonQueryAsync();
        }
    }

    private static string? TryReadUtc(SqliteDataReader reader, IReadOnlyList<string> columns, string? sourceColumn)
    {
        if (string.IsNullOrWhiteSpace(sourceColumn))
            return null;

        var index = -1;
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], sourceColumn, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || reader.IsDBNull(index))
            return null;

        return DatabaseDateTime.ToUtcString(DatabaseDateTime.ParseUtc(reader.GetString(index)));
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName, string columnName)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (await ColumnExistsAsync(connection, transaction, tableName, columnName))
            return;

        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await cmd.ExecuteNonQueryAsync();
    }
}
