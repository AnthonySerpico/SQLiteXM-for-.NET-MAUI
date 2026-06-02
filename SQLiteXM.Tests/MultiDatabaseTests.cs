using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for multi-database SqlStatements functionality.
/// These tests validate that SQLiteXM correctly handles SqlStatements files with multiple database definitions.
/// </summary>
[Collection("MultiDatabase")]
public class MultiDatabaseTests : IDisposable
{
    private static readonly string MultiDbTestFolder;
    private readonly string _testStatementsPath;
    private readonly string _testId;
    private bool _disposed = false;

    static MultiDatabaseTests()
    {
        // Create a separate test folder for multi-database tests to avoid conflicts
        MultiDbTestFolder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "MultiDatabase");
        Directory.CreateDirectory(MultiDbTestFolder);
    }

    public MultiDatabaseTests()
    {
        // Each test gets its own unique statements file
        _testId = Guid.NewGuid().ToString("N");
        _testStatementsPath = Path.Combine(MultiDbTestFolder, $"statements_{_testId}.json");
    }

    public void Dispose()
    {
        if (_disposed) return;

#if !KEEP_MULTI_DB_TEST_FILES
        try
        {
            // Clean up test statements file
            if (File.Exists(_testStatementsPath))
            {
                File.Delete(_testStatementsPath);
            }

            // Clean up any database files created during tests
            var dbFiles = Directory.GetFiles(MultiDbTestFolder, $"*{_testId}*");
            foreach (var file in dbFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
        catch
        {
            // Best effort cleanup
        }
#else
        // Files are preserved for inspection when KEEP_MULTI_DB_TEST_FILES is defined
        // Location: %TEMP%\SQLiteXM.Tests\MultiDatabase\
        Console.WriteLine($"Test files preserved at: {MultiDbTestFolder}");
        Console.WriteLine($"Test ID: {_testId}");
#endif

        // CRITICAL: Reset SQLiteXM state so subsequent tests that use TestBase's standard "test_database"
        // configuration will work correctly. Multi-database tests temporarily override the SQL statements
        // and database descriptors, so we must clear all caches.
        try
        {
            SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();

            // Re-initialize with the standard test configuration from TestBase
            var initOptions = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "test_database")
            };
            var testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
            using var stream = File.OpenRead(testStatementsPath);
            SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();

            // Re-register standard test entities (matching TestBase.RegisterAllTestEntitySchemasSync)
            SxmDatabase.RegisterEntitiesAsync(
                typeof(SimpleEntity),
                typeof(AllTypesEntity),
                typeof(TimeTypeTextEntity),
                typeof(ExplicitColumnEntity),
                typeof(IndexedEntity),
                typeof(ParentEntity),
                typeof(ChildEntity),
                typeof(TriggerEntity),
                typeof(RequiredFieldEntity)
            ).GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort - if reset fails, at least we tried
        }

        _disposed = true;
    }

    /// <summary>
    /// Creates a SqlStatements file with multiple database definitions.
    /// </summary>
    /// <param name="databases">Array of database definitions with name and isDefault flag</param>
    /// <param name="triggers">Optional array of trigger definitions with Database scope</param>
    private void CreateMultiDatabaseSqlStatementsFile(
        (string name, bool isDefault)[] databases,
        (string database, string tableName, string statement)[]? triggers = null)
    {
        var dbArray = databases.Select(db => new
        {
            database = db.name,
            isDefault = db.isDefault
        }).ToArray();

        var config = new Dictionary<string, object>
        {
            ["version"] = 1L,
            ["databases"] = dbArray
        };

        // Add triggers if provided
        if (triggers != null && triggers.Length > 0)
        {
            var triggerArray = triggers.Select(t => new Dictionary<string, object>
            {
                ["Database"] = t.database,
                ["Table Name"] = t.tableName,  // Note: Space in property name matches expected format
                ["Statement"] = t.statement
            }).ToArray();
            config["trigger"] = triggerArray;
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testStatementsPath, json);
    }

    /// <summary>
    /// Creates a simple single-database SqlStatements file.
    /// </summary>
    private void CreateSingleDatabaseSqlStatementsFile(string databaseName)
    {
        CreateMultiDatabaseSqlStatementsFile(new[] { (databaseName, true) });
    }

    /// <summary>
    /// Helper to get internal Databases list from SxmProcessSQLStatements via reflection.
    /// </summary>
    private IReadOnlyList<string> GetParsedDatabases()
    {
        var type = typeof(SxmDatabase).Assembly.GetType("SQLiteXM.SxmProcessSQLStatements");
        var prop = type?.GetProperty("Databases", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return (IReadOnlyList<string>)(prop?.GetValue(null) ?? new List<string>());
    }

    /// <summary>
    /// Helper to get internal DefaultDatabaseName from SxmProcessSQLStatements via reflection.
    /// </summary>
    private string GetDefaultDatabaseName()
    {
        var type = typeof(SxmDatabase).Assembly.GetType("SQLiteXM.SxmProcessSQLStatements");
        var prop = type?.GetProperty("DefaultDatabaseName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return (string)(prop?.GetValue(null) ?? string.Empty);
    }

    [Fact]
    public async Task ParseMultipleDatabases_StoresAllDatabaseNames()
    {
        // Arrange
        var testDb1 = $"testdb1_{_testId}";
        var testDb2 = $"testdb2_{_testId}";
        var testDb3 = $"testdb3_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (testDb1, false),
            (testDb2, true),   // default
            (testDb3, false)
        });

        // Create test options with separate folder
        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var streamFix1 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(streamFix1, options);

        // Assert
        var databases = GetParsedDatabases();
        Assert.Equal(3, databases.Count);
        Assert.Contains(testDb1, databases);
        Assert.Contains(testDb2, databases);
        Assert.Contains(testDb3, databases);

        var defaultDb = GetDefaultDatabaseName();
        Assert.Equal(testDb2, defaultDb);
    }

    [Fact]
    public async Task DefaultDatabase_IdentifiedCorrectly()
    {
        // Arrange
        var testDb1 = $"primary_{_testId}";
        var testDb2 = $"secondary_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (testDb1, true),   // This should be the default
            (testDb2, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);

        // Assert
        var defaultDb = GetDefaultDatabaseName();
        Assert.Equal(testDb1, defaultDb);
        Assert.NotEqual(testDb2, defaultDb);
    }

    [Fact]
    public async Task SingleDatabase_IsAutomaticallyDefault()
    {
        // Arrange
        var testDb = $"singledb_{_testId}";
        CreateSingleDatabaseSqlStatementsFile(testDb);

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var streamFix2 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(streamFix2, options);

        // Assert
        var databases = GetParsedDatabases();
        Assert.Single(databases);
        Assert.Equal(testDb, databases[0]);

        var defaultDb = GetDefaultDatabaseName();
        Assert.Equal(testDb, defaultDb);
    }

    [Fact]
    public async Task TriggerWithValidDatabaseScope_ParsesSuccessfully()
    {
        // Arrange
        var testDb1 = $"db1_{_testId}";
        var testDb2 = $"db2_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(
            new[]
            {
                (testDb1, true),
                (testDb2, false)
            },
            new[]
            {
                (testDb1, "test_table", "CREATE TRIGGER test_trigger AFTER INSERT ON test_table BEGIN SELECT 1; END;"),
                (testDb2, "other_table", "CREATE TRIGGER other_trigger AFTER UPDATE ON other_table BEGIN SELECT 2; END;")
            }
        );

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act & Assert - should not throw
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);

        var databases = GetParsedDatabases();
        Assert.Equal(2, databases.Count);
    }

    [Fact]
    public async Task MissingDefaultDatabase_ThrowsException()
    {
        // Arrange - create file with no default database
        var testDb1 = $"db1_{_testId}";
        var testDb2 = $"db2_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (testDb1, false),
            (testDb2, false)  // Both are not default
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act & Assert
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var stream = File.OpenRead(_testStatementsPath);
            await SxmDatabase.InitializeAsync(stream, options);
        });

        Assert.Contains("default", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleDefaultDatabases_ThrowsException()
    {
        // Arrange - create file with multiple default databases
        var testDb1 = $"db1_{_testId}";
        var testDb2 = $"db2_{_testId}";
        var testDb3 = $"db3_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (testDb1, true),  // First default
            (testDb2, true),  // Second default - invalid!
            (testDb3, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act & Assert
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var stream = File.OpenRead(_testStatementsPath);
            await SxmDatabase.InitializeAsync(stream, options);
        });

        Assert.Contains("default", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TriggerReferencesNonExistentDatabase_ThrowsException()
    {
        // Arrange - create file with trigger referencing a database that doesn't exist
        var testDb1 = $"db1_{_testId}";
        var testDb2 = $"db2_{_testId}";
        var nonExistentDb = $"nonexistent_{_testId}";

        CreateMultiDatabaseSqlStatementsFile(
            new[]
            {
                (testDb1, true),
                (testDb2, false)
            },
            new[]
            {
                (nonExistentDb, "test_table", "CREATE TRIGGER bad_trigger AFTER INSERT ON test_table BEGIN SELECT 1; END;")
            }
        );

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act & Assert
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var stream = File.OpenRead(_testStatementsPath);
            await SxmDatabase.InitializeAsync(stream, options);
        });

        Assert.Contains("database", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InsertEntitiesIntoMultipleDatabases_CreatesCorrectDatabaseFiles()
    {
        // Arrange - create SqlStatements with two databases (use static names to match entity attributes)
        var dbA = "databaseA";
        var dbB = "databaseB";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (dbA, true),    // databaseA is default
            (dbB, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

        // Act - Initialize and register entities
#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DatabaseAEntity),
            typeof(SecondDatabaseAEntity),
            typeof(DatabaseBEntity)
        );

        // Create and save entities in different databases
        var entityA = new DatabaseAEntity
        {
            Name = "Test A",
            Description = "Entity in Database A",
            Count = 42
        };
        await entityA.SaveAsync();

        var entityA2 = new SecondDatabaseAEntity
        {
            Category = "Category 1",
            Amount = 99.99
        };
        await entityA2.SaveAsync();

        var entityB = new DatabaseBEntity
        {
            Title = "Test B",
            Content = "Entity in Database B",
            CreatedDate = DateTime.Now
        };
        await entityB.SaveAsync();

        // Assert - Verify database files were created
        var dbAPath = Path.Combine(testFolder, dbA);
        var dbBPath = Path.Combine(testFolder, dbB);

        Assert.True(File.Exists(dbAPath), $"Database A file should exist at {dbAPath}");
        Assert.True(File.Exists(dbBPath), $"Database B file should exist at {dbBPath}");

        // Verify entities were assigned IDs (indicating successful insert)
        Assert.True(entityA.id > 0, "DatabaseAEntity should have been assigned an ID");
        Assert.True(entityA2.id > 0, "SecondDatabaseAEntity should have been assigned an ID");
        Assert.True(entityB.id > 0, "DatabaseBEntity should have been assigned an ID");
    }

    [Fact]
    public async Task DataIsolation_EntitiesInDifferentDatabasesDoNotInterfere()
    {
        // Arrange - create SqlStatements with two databases (use static names to match entity attributes)
        var dbA = "databaseA";
        var dbB = "databaseB";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (dbA, true),
            (dbB, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DatabaseAEntity),
            typeof(DatabaseBEntity)
        );

        // Act - Create entities in both databases
        var entityA1 = new DatabaseAEntity { Name = "A1", Description = "First in A", Count = 1 };
        var entityA2 = new DatabaseAEntity { Name = "A2", Description = "Second in A", Count = 2 };
        var entityB1 = new DatabaseBEntity { Title = "B1", Content = "First in B", CreatedDate = DateTime.Now };
        var entityB2 = new DatabaseBEntity { Title = "B2", Content = "Second in B", CreatedDate = DateTime.Now };

        await entityA1.SaveAsync();
        await entityA2.SaveAsync();
        await entityB1.SaveAsync();
        await entityB2.SaveAsync();

        // Assert - Query each database and verify isolation
        using (var contextA = new SxmLinqDbContext(dbA))
        {
            var entitiesInA = contextA.GetTable<DatabaseAEntity>().ToList();
            Assert.Equal(2, entitiesInA.Count);
            Assert.Contains(entitiesInA, e => e.Name == "A1");
            Assert.Contains(entitiesInA, e => e.Name == "A2");
        }

        using (var contextB = new SxmLinqDbContext(dbB))
        {
            var entitiesInB = contextB.GetTable<DatabaseBEntity>().ToList();
            Assert.Equal(2, entitiesInB.Count);
            Assert.Contains(entitiesInB, e => e.Title == "B1");
            Assert.Contains(entitiesInB, e => e.Title == "B2");
        }

        // Verify Database A doesn't contain Database B entities
        using (var contextA = new SxmLinqDbContext(dbA))
        {
            // DatabaseBEntity table shouldn't exist in Database A
            var tables = contextA.GetTable<DatabaseBEntity>();
            var exception = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => tables.ToList());
            Assert.Contains("no such table", exception.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UpdateOperations_WorkAcrossMultipleDatabases()
    {
        // Arrange (use static names to match entity attributes)
        var dbA = "databaseA";
        var dbB = "databaseB";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (dbA, true),
            (dbB, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DatabaseAEntity),
            typeof(DatabaseBEntity)
        );

        // Act - Create, then update entities in both databases
        var entityA = new DatabaseAEntity { Name = "Original A", Description = "Before Update", Count = 10 };
        await entityA.SaveAsync();
        var entityAId = entityA.id;

        var entityB = new DatabaseBEntity { Title = "Original B", Content = "Before Update", CreatedDate = DateTime.Now };
        await entityB.SaveAsync();
        var entityBId = entityB.id;

        // Update both entities
        entityA.Name = "Updated A";
        entityA.Description = "After Update";
        entityA.Count = 20;
        await entityA.SaveAsync();  // SaveAsync handles both insert and update

        entityB.Title = "Updated B";
        entityB.Content = "After Update";
        await entityB.SaveAsync();  // SaveAsync handles both insert and update

        // Assert - Verify updates persisted in each database
        using (var contextA = new SxmLinqDbContext(dbA))
        {
            var updated = contextA.GetTable<DatabaseAEntity>().First(e => e.id == entityAId);
            Assert.Equal("Updated A", updated.Name);
            Assert.Equal("After Update", updated.Description);
            Assert.Equal(20, updated.Count);
        }

        using (var contextB = new SxmLinqDbContext(dbB))
        {
            var updated = contextB.GetTable<DatabaseBEntity>().First(e => e.id == entityBId);
            Assert.Equal("Updated B", updated.Title);
            Assert.Equal("After Update", updated.Content);
        }
    }

    [Fact]
    public async Task DeleteOperations_WorkAcrossMultipleDatabases()
    {
        // Arrange (use static names to match entity attributes)
        var dbA = "databaseA";
        var dbB = "databaseB";

        CreateMultiDatabaseSqlStatementsFile(new[]
        {
            (dbA, true),
            (dbB, false)
        });

        var testFolder = Path.Combine(MultiDbTestFolder, _testId);
        Directory.CreateDirectory(testFolder);
        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = testFolder
        };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DatabaseAEntity),
            typeof(DatabaseBEntity)
        );

        // Act - Create entities in both databases
        var entityA1 = new DatabaseAEntity { Name = "To Delete A", Description = "Will be deleted", Count = 99 };
        var entityA2 = new DatabaseAEntity { Name = "To Keep A", Description = "Will remain", Count = 100 };
        await entityA1.SaveAsync();
        await entityA2.SaveAsync();
        var entityA1Id = entityA1.id;

        var entityB1 = new DatabaseBEntity { Title = "To Delete B", Content = "Will be deleted", CreatedDate = DateTime.Now };
        var entityB2 = new DatabaseBEntity { Title = "To Keep B", Content = "Will remain", CreatedDate = DateTime.Now };
        await entityB1.SaveAsync();
        await entityB2.SaveAsync();
        var entityB1Id = entityB1.id;

        // Delete one entity from each database
        await entityA1.DeleteAsync();
        await entityB1.DeleteAsync();

        // Assert - Verify deletions in each database
        using (var contextA = new SxmLinqDbContext(dbA))
        {
            var remaining = contextA.GetTable<DatabaseAEntity>().ToList();
            Assert.Single(remaining);
            Assert.Equal("To Keep A", remaining[0].Name);
            Assert.DoesNotContain(remaining, e => e.id == entityA1Id);
        }

        using (var contextB = new SxmLinqDbContext(dbB))
        {
            var remaining = contextB.GetTable<DatabaseBEntity>().ToList();
            Assert.Single(remaining);
            Assert.Equal("To Keep B", remaining[0].Title);
            Assert.DoesNotContain(remaining, e => e.id == entityB1Id);
        }
    }

    /// <summary>
    /// Test entity for multi-database tests.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class MultiDbTestEntity : SxmEntity
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Test entity explicitly assigned to Database A.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "databaseA")]
    public class DatabaseAEntity : SxmEntity
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Test entity explicitly assigned to Database B.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "databaseB")]
    public class DatabaseBEntity : SxmEntity
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// Another entity for Database A to test multiple tables in same database.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "databaseA")]
    public class SecondDatabaseAEntity : SxmEntity
    {
        public string? Category { get; set; }
        public double Amount { get; set; }
    }
}
