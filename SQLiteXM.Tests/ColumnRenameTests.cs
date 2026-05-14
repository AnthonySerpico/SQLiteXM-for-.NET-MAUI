using Xunit;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for column rename functionality using [Rename] attribute.
/// </summary>
[Collection("Sequential")]
public class ColumnRenameTests : TestBase
{
    #region Test Entities

    /// <summary>
    /// Base entity for single-step rename test (Version 1: original).
    /// </summary>
    public class SingleStepRenameV1 : SxmEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Single-step rename (Version 2: Title → Name).
    /// </summary>
    public class SingleStepRenameV2 : SxmEntity
    {
        [Rename("Title")]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Multi-step rename (Version 1: original).
    /// </summary>
    public class MultiStepRenameV1 : SxmEntity
    {
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// Multi-step rename (Version 2: Title → Name).
    /// </summary>
    public class MultiStepRenameV2 : SxmEntity
    {
        [Rename("Title")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Multi-step rename (Version 3: Title → Name → ProductName).
    /// </summary>
    public class MultiStepRenameV3 : SxmEntity
    {
        [Rename("Title", "Name")]
        public string ProductName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Fresh install test (no old columns exist).
    /// </summary>
    public class FreshInstallEntity : SxmEntity
    {
        [Rename("OldName", "MiddleName")]
        public string FinalName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entity with invalid rename (old property still exists).
    /// </summary>
    public class InvalidRenameOldPropertyExists : SxmEntity
    {
        public string OldName { get; set; } = string.Empty;

        [Rename("OldName")]
        public string NewName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entity with duplicate rename claims.
    /// </summary>
    public class InvalidRenameDuplicateClaim : SxmEntity
    {
        [Rename("SharedOldName")]
        public string Name1 { get; set; } = string.Empty;

        [Rename("SharedOldName")]
        public string Name2 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entity with both [Rename] and [NotColumn] (invalid).
    /// </summary>
    public class InvalidRenameWithNotColumn : SxmEntity
    {
        [Rename("OldField")]
        [NotColumn]
        public string NewField { get; set; } = string.Empty;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a direct database connection for raw SQL operations.
    /// NOTE: SQLiteXM stores database files WITHOUT extensions (just the database name).
    /// </summary>
    private SqliteConnection GetDirectConnection()
    {
        string dbPath = Path.Combine(TestDatabaseFolder, TestDatabaseName);
        return new SqliteConnection($"Data Source={dbPath}");
    }

    /// <summary>
    /// Checks if a column exists in a table.
    /// </summary>
    private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        using var conn = GetDirectConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a test table directly with specified columns (bypassing entity registration).
    /// </summary>
    private async Task CreateTestTableDirectlyAsync(string tableName, params (string name, string type)[] columns)
    {
        using var conn = GetDirectConnection();
        await conn.OpenAsync();

        // Drop table if exists
        using (var dropCmd = conn.CreateCommand())
        {
            dropCmd.CommandText = $"DROP TABLE IF EXISTS {tableName}";
            await dropCmd.ExecuteNonQueryAsync();
        }

        // Create table
        var columnDefs = string.Join(", ", columns.Select(c => $"{c.name} {c.type}"));
        using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText = $"CREATE TABLE {tableName} (Id INTEGER PRIMARY KEY AUTOINCREMENT, {columnDefs})";
            await createCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Inserts test data into a table.
    /// </summary>
    private async Task InsertTestDataAsync(string tableName, Dictionary<string, object> values)
    {
        using var conn = GetDirectConnection();
        await conn.OpenAsync();
        var columns = string.Join(", ", values.Keys);
        var parameters = string.Join(", ", values.Keys.Select(k => $"@{k}"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
        foreach (var kvp in values)
        {
            cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads a column value from a table.
    /// </summary>
    private async Task<T?> ReadColumnValueAsync<T>(string tableName, string columnName, long id)
    {
        using var conn = GetDirectConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {columnName} FROM {tableName} WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? default : (T)result;
    }

    /// <summary>
    /// Drops a test table if it exists.
    /// </summary>
    private async Task DropTableIfExistsAsync(string tableName)
    {
        using var conn = GetDirectConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {tableName}";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Resets schema registration state for column rename test entities.
    /// This allows tests to re-register schemas without resetting the entire SQLiteXM system.
    /// </summary>
    private void ResetColumnRenameSchemaRegistration()
    {
        // Use reflection to access the private _registeredSchemas dictionary in SxmSchemaRegistration
        var schemaRegType = typeof(SxmEntity).Assembly.GetType("SQLiteXM.SxmSchemaRegistration");
        if (schemaRegType == null)
            return;

        var registeredSchemasField = schemaRegType.GetField("_registeredSchemas",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (registeredSchemasField == null)
            return;

        var registeredSchemas = registeredSchemasField.GetValue(null) as System.Collections.IDictionary;
        if (registeredSchemas == null)
            return;

        // Also clear the _initTasks cache
        var initTasksField = schemaRegType.GetField("_initTasks",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var initTasks = initTasksField?.GetValue(null) as System.Collections.IDictionary;

        // Also clear the column name and type dictionary from SxmEntity
        var columnDictField = typeof(SxmEntity).GetField("_columnNameAndTypeDict",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var columnDict = columnDictField?.GetValue(null) as System.Collections.IDictionary;

        // Remove only the column rename test entities
        var typesToReset = new[]
        {
            typeof(SingleStepRenameV1),
            typeof(SingleStepRenameV2),
            typeof(MultiStepRenameV1),
            typeof(MultiStepRenameV2),
            typeof(MultiStepRenameV3),
            typeof(FreshInstallEntity),
            typeof(InvalidRenameOldPropertyExists),
            typeof(InvalidRenameDuplicateClaim),
            typeof(InvalidRenameWithNotColumn)
        };

        var tableNamesToReset = typesToReset.Select(t => t.Name).ToArray();

        foreach (var type in typesToReset)
        {
            registeredSchemas.Remove(type);
        }

        // Clear init tasks and column dictionaries for the table names
        if (initTasks != null)
        {
            foreach (var tableName in tableNamesToReset)
            {
                initTasks.Remove(tableName);
            }
        }

        if (columnDict != null)
        {
            foreach (var tableName in tableNamesToReset)
            {
                columnDict.Remove(tableName);
            }
        }
    }

    #endregion

    #region Positive Tests (Happy Path)

    [Fact]
    public async Task SingleStepRename_ShouldPreserveData()
    {
        // Arrange: Create V1 table and insert data
        string tableName = nameof(SingleStepRenameV2);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("Title", "TEXT"), ("Description", "TEXT"));
        await InsertTestDataAsync(tableName, new Dictionary<string, object>
        {
            { "Title", "Original Title" },
            { "Description", "Test Description" }
        });

        // Act: Register V2 schema (should rename Title → Name)
        await SxmInit.RegisterSchemaAsync(typeof(SingleStepRenameV2));

        // Assert: Column renamed and data preserved
        Assert.False(await ColumnExistsAsync(tableName, "Title"), "Old column 'Title' should not exist");
        Assert.True(await ColumnExistsAsync(tableName, "Name"), "New column 'Name' should exist");

        var value = await ReadColumnValueAsync<string>(tableName, "Name", 1);
        Assert.Equal("Original Title", value);

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    [Fact]
    public async Task MultiStepRename_FromV1ToV3_ShouldSkipV2()
    {
        // Arrange: Create V1 table with "Title" column
        string tableName = nameof(MultiStepRenameV3);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("Title", "TEXT"));
        await InsertTestDataAsync(tableName, new Dictionary<string, object>
        {
            { "Title", "Original Title" }
        });

        // Act: Register V3 schema directly (skipping V2)
        // Should find "Title" and rename directly to "ProductName"
        await SxmInit.RegisterSchemaAsync(typeof(MultiStepRenameV3));

        // Assert: Title → ProductName (V2 "Name" never existed)
        Assert.False(await ColumnExistsAsync(tableName, "Title"), "Old column 'Title' should not exist");
        Assert.False(await ColumnExistsAsync(tableName, "Name"), "Intermediate column 'Name' should not exist");
        Assert.True(await ColumnExistsAsync(tableName, "ProductName"), "New column 'ProductName' should exist");

        var value = await ReadColumnValueAsync<string>(tableName, "ProductName", 1);
        Assert.Equal("Original Title", value);

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    [Fact]
    public async Task MultiStepRename_SequentialUpgrade_ShouldRenameInSteps()
    {
        // Arrange: Start with V1
        string tableName = nameof(MultiStepRenameV2);
        string v3TableName = nameof(MultiStepRenameV3);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        await DropTableIfExistsAsync(v3TableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("Title", "TEXT"));
        await InsertTestDataAsync(tableName, new Dictionary<string, object>
        {
            { "Title", "Original Title"  }
        });

        // Act 1: Upgrade to V2 (Title → Name)
        await SxmInit.RegisterSchemaAsync(typeof(MultiStepRenameV2));

        // Assert 1: Title renamed to Name
        Assert.False(await ColumnExistsAsync(tableName, "Title"));
        Assert.True(await ColumnExistsAsync(tableName, "Name"));
        var valueAfterV2 = await ReadColumnValueAsync<string>(tableName, "Name", 1);
        Assert.Equal("Original Title", valueAfterV2);

        // Arrange 2: Simulate V3 by creating the table for MultiStepRenameV3 with "Name" column
        // (We reuse the same data, renaming the table to match V3 entity)
        using (var conn = GetDirectConnection())
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {tableName} RENAME TO {v3TableName}";
            await cmd.ExecuteNonQueryAsync();
        }

        // Act 2: Upgrade to V3 (Name → ProductName)
        await SxmInit.RegisterSchemaAsync(typeof(MultiStepRenameV3));

        // Assert 2: Name renamed to ProductName
        Assert.False(await ColumnExistsAsync(v3TableName, "Title"));
        Assert.False(await ColumnExistsAsync(v3TableName, "Name"));
        Assert.True(await ColumnExistsAsync(v3TableName, "ProductName"));
        var valueAfterV3 = await ReadColumnValueAsync<string>(v3TableName, "ProductName", 1);
        Assert.Equal("Original Title", valueAfterV3);

        // Cleanup: Drop tables after test
        await DropTableIfExistsAsync(tableName);
        await DropTableIfExistsAsync(v3TableName);
    }

    [Fact]
    public async Task FreshInstall_NoOldColumnsExist_ShouldCreateNewColumn()
    {
        // Arrange: No pre-existing table
        string tableName = nameof(FreshInstallEntity);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        // Act: Register schema for fresh install
        await SxmInit.RegisterSchemaAsync(typeof(FreshInstallEntity));

        // Assert: New column created directly (no rename occurred)
        Assert.True(await ColumnExistsAsync(nameof(FreshInstallEntity), "FinalName"));
        Assert.False(await ColumnExistsAsync(nameof(FreshInstallEntity), "OldName"));
        Assert.False(await ColumnExistsAsync(nameof(FreshInstallEntity), "MiddleName"));

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    [Fact]
    public async Task Rename_WithPartialHistory_ShouldFindMostRecentMatch()
    {
        // Arrange: Create table with "Name" (V2) but not "Title" (V1)
        string tableName = nameof(MultiStepRenameV3);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("Name", "TEXT"));
        await InsertTestDataAsync(tableName, new Dictionary<string, object>
        {
            { "Name", "Test Name" }
        });

        // Act: Register V3 (should find "Name" and rename to "ProductName")
        await SxmInit.RegisterSchemaAsync(typeof(MultiStepRenameV3));

        // Assert: Name → ProductName (Title never existed)
        Assert.False(await ColumnExistsAsync(tableName, "Title"));
        Assert.False(await ColumnExistsAsync(tableName, "Name"));
        Assert.True(await ColumnExistsAsync(tableName, "ProductName"));

        var value = await ReadColumnValueAsync<string>(tableName, "ProductName", 1);
        Assert.Equal("Test Name", value);

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    #endregion

    #region Negative Tests (Validation)

    [Fact]
    public async Task Rename_OldPropertyStillExists_ShouldThrow()
    {
        // Arrange & Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await SxmInit.RegisterSchemaAsync(typeof(InvalidRenameOldPropertyExists));
        });

        Assert.Contains("SCHEMA ERROR", exception.Message);
        Assert.Contains("OldName", exception.Message);
        Assert.Contains("still exists", exception.Message);
    }

    [Fact]
    public async Task Rename_DuplicateClaim_ShouldThrow()
    {
        // Arrange & Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await SxmInit.RegisterSchemaAsync(typeof(InvalidRenameDuplicateClaim));
        });

        Assert.Contains("SCHEMA ERROR", exception.Message);
        Assert.Contains("Multiple properties claim", exception.Message);
        Assert.Contains("SharedOldName", exception.Message);
    }

    [Fact]
    public async Task Rename_WithNotColumn_ShouldThrow()
    {
        // Arrange & Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await SxmInit.RegisterSchemaAsync(typeof(InvalidRenameWithNotColumn));
        });

        Assert.Contains("SCHEMA ERROR", exception.Message);
        Assert.Contains("cannot have both", exception.Message);
        Assert.Contains("[Rename]", exception.Message);
        Assert.Contains("[NotColumn]", exception.Message);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Rename_AlreadyRenamed_ShouldBeIdempotent()
    {
        // Arrange: Create V2 table directly (already renamed)
        string tableName = nameof(SingleStepRenameV2);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("Name", "TEXT"), ("Description", "TEXT"));
        await InsertTestDataAsync(tableName, new Dictionary<string, object>
        {
            { "Name", "Already Renamed" },
            { "Description", "Test" }
        });

        // Act: Register V2 schema again (should be no-op)
        await SxmInit.RegisterSchemaAsync(typeof(SingleStepRenameV2));

        // Assert: No changes (already correct)
        Assert.False(await ColumnExistsAsync(tableName, "Title"));
        Assert.True(await ColumnExistsAsync(tableName, "Name"));

        var value = await ReadColumnValueAsync<string>(tableName, "Name", 1);
        Assert.Equal("Already Renamed", value);

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    [Fact]
    public async Task Rename_NoneOfHistoricalNamesExist_ShouldCreateNewColumn()
    {
        // Arrange: Create table without any of the historical names
        string tableName = nameof(MultiStepRenameV3);

        // Cleanup: Ensure clean state
        await DropTableIfExistsAsync(tableName);
        ResetColumnRenameSchemaRegistration();

        await CreateTestTableDirectlyAsync(tableName, ("SomeOtherColumn", "TEXT"));

        // Act: Register V3 (no "Title" or "Name" exist → create "ProductName")
        await SxmInit.RegisterSchemaAsync(typeof(MultiStepRenameV3));

        // Assert: ProductName created as new column
        Assert.True(await ColumnExistsAsync(tableName, "ProductName"));
        Assert.False(await ColumnExistsAsync(tableName, "Title"));
        Assert.False(await ColumnExistsAsync(tableName, "Name"));

        // Cleanup: Drop table after test
        await DropTableIfExistsAsync(tableName);
    }

    #endregion
}
