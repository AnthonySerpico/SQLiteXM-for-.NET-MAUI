using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for bulk LINQ update and delete operations via SxmLinqExtensions.
/// These operations execute immediately inside the context transaction (started lazily on the first write).
/// The transaction auto-commits on dispose (or CommitAsync) and rolls back via RollbackAsync.
/// </summary>
[Collection("Sequential")]
public class BulkLinqOperationsTests : TestBase
{
    #region Bulk Update Tests

    [Fact]
    public async Task BulkUpdate_SetSingleProperty_ShouldUpdateMatchingRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new[]
        {
            new SimpleEntity { Name = $"BulkTest_{uniquePrefix}_1", Age = 10, IsActive = false },
            new SimpleEntity { Name = $"BulkTest_{uniquePrefix}_2", Age = 20, IsActive = false },
            new SimpleEntity { Name = $"BulkTest_{uniquePrefix}_3", Age = 30, IsActive = true }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Bulk update Age for entities where Age < 25
        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"BulkTest_{uniquePrefix}") && e.Age < 25)
            .Set(e => e.Age, 99)
            .UpdateAsync();

        // Assert - immediate execution returns the real row count
        updated.Should().Be(2, "two entities had Age < 25");

        var results = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"BulkTest_{uniquePrefix}"))
            .ToList();

        results.Should().HaveCount(3);
        results.Where(e => e.Age == 99).Should().HaveCount(2, "two entities had Age < 25");
        results.Single(e => e.Name!.Contains("_3")).Age.Should().Be(30, "should not be updated");

        // Cleanup
        foreach (var entity in results)
        {
            await ctx.DeleteAsync(entity);
        }
    }

    [Fact]
    public async Task BulkUpdate_SetWithExpression_ShouldIncrementValues()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new[]
        {
            new SimpleEntity { Name = $"Increment_{uniquePrefix}_1", Age = 10 },
            new SimpleEntity { Name = $"Increment_{uniquePrefix}_2", Age = 20 },
            new SimpleEntity { Name = $"Increment_{uniquePrefix}_3", Age = 30 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Increment Age by 5 using expression
        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Increment_{uniquePrefix}"))
            .Set(e => e.Age, e => e.Age + 5)
            .UpdateAsync();

        // Assert
        updated.Should().Be(3);

        var results = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Increment_{uniquePrefix}"))
            .OrderBy(e => e.Name)
            .ToList();

        results.Should().HaveCount(3);
        results[0].Age.Should().Be(15);
        results[1].Age.Should().Be(25);
        results[2].Age.Should().Be(35);

        // Cleanup
        foreach (var entity in results)
        {
            await ctx.DeleteAsync(entity);
        }
    }

    [Fact]
    public async Task BulkUpdate_SetMultipleProperties_ShouldUpdateAll()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entity = new SimpleEntity
        {
            Name = $"MultiUpdate_{uniquePrefix}",
            Age = 10,
            IsActive = false
        };

        await ctx.InsertAsync(entity);

        // Act - Update multiple properties in chain
        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == $"MultiUpdate_{uniquePrefix}")
            .Set(e => e.Age, 50)
            .Set(e => e.IsActive, true)
            .UpdateAsync();

        // Assert
        updated.Should().Be(1);

        var result = ctx.GetTable<SimpleEntity>()
            .Single(e => e.Name == $"MultiUpdate_{uniquePrefix}");

        result.Age.Should().Be(50);
        result.IsActive.Should().BeTrue();

        // Cleanup
        await ctx.DeleteAsync(result);
    }

    [Fact]
    public async Task BulkUpdate_RollbackAsync_ShouldNotPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        long entityId;

        // Insert and commit in a first context
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var entity = new SimpleEntity { Name = "NoContextTest", Age = 10 };
            await ctx.InsertAsync(entity);
            entityId = entity.id;
        } // auto-commit on dispose

        // Act - perform bulk update then explicitly roll back
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == entityId)
                .Set(e => e.Age, 999)
                .UpdateAsync();

            await ctx.RollbackTransactionAsync();
        }

        // Assert - Update should NOT be persisted
        await using var ctx2 = new SxmTransaction(TestDatabaseName);
        var result = ctx2.GetTable<SimpleEntity>().Single(e => e.id == entityId);
        result.Age.Should().Be(10, "update was rolled back");

        // Cleanup
        await ctx2.DeleteAsync(result);
    }

    [Fact]
    public async Task BulkUpdate_AutoCommitOnDispose_ShouldPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        long entityId;

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var entity = new SimpleEntity { Name = $"AutoCommit_{uniquePrefix}", Age = 10 };
            await ctx.InsertAsync(entity);
            entityId = entity.id;

            // Act - bulk update; no explicit commit
            await ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == entityId)
                .Set(e => e.Age, 999)
                .UpdateAsync();
        } // auto-commit on dispose

        // Assert - Changes SHOULD be persisted
        await using var ctx2 = new SxmTransaction(TestDatabaseName);
        var result = ctx2.GetTable<SimpleEntity>()
            .Single(e => e.id == entityId);

        result.Age.Should().Be(999, "auto-commit on dispose persisted the update");

        // Cleanup
        await ctx2.DeleteAsync(result);
    }

    #endregion

    #region Bulk Delete Tests

    [Fact]
    public async Task BulkDelete_WithWhereClause_ShouldDeleteMatchingRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new[]
        {
            new SimpleEntity { Name = $"Delete_{uniquePrefix}_1", Age = 10 },
            new SimpleEntity { Name = $"Delete_{uniquePrefix}_2", Age = 20 },
            new SimpleEntity { Name = $"Delete_{uniquePrefix}_3", Age = 30 },
            new SimpleEntity { Name = $"Keep_{uniquePrefix}_4", Age = 40 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Bulk delete entities with Age < 25
        int deleted = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Delete_{uniquePrefix}") && e.Age < 25)
            .DeleteAsync();

        // Assert
        deleted.Should().Be(2);

        var remaining = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.Contains(uniquePrefix))
            .ToList();

        remaining.Should().HaveCount(2, "two entities had Age >= 25");
        remaining.Should().NotContain(e => e.Name!.EndsWith("_1"));
        remaining.Should().NotContain(e => e.Name!.EndsWith("_2"));
        remaining.Should().Contain(e => e.Name!.EndsWith("_3"));
        remaining.Should().Contain(e => e.Name!.EndsWith("_4"));

        // Cleanup
        foreach (var entity in remaining)
        {
            await ctx.DeleteAsync(entity);
        }
    }

    [Fact]
    public async Task BulkDelete_AllMatchingRows_ShouldDeleteAll()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new[]
        {
            new SimpleEntity { Name = $"DeleteAll_{uniquePrefix}_1", Age = 10 },
            new SimpleEntity { Name = $"DeleteAll_{uniquePrefix}_2", Age = 20 },
            new SimpleEntity { Name = $"DeleteAll_{uniquePrefix}_3", Age = 30 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Delete all entities with this prefix
        int deleted = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"DeleteAll_{uniquePrefix}"))
            .DeleteAsync();

        // Assert
        deleted.Should().Be(3);

        var remaining = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"DeleteAll_{uniquePrefix}"))
            .ToList();

        remaining.Should().BeEmpty("all matching entities should be deleted");
    }

    [Fact]
    public async Task BulkDelete_RollbackAsync_ShouldNotDeleteRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        long entityId;

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var entity = new SimpleEntity { Name = "NoContextDeleteTest", Age = 10 };
            await ctx.InsertAsync(entity);
            entityId = entity.id;
        } // auto-commit

        // Act - Bulk delete then explicitly roll back
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == entityId)
                .DeleteAsync();

            await ctx.RollbackTransactionAsync();
        }

        // Assert - Entity should still exist
        await using var ctx2 = new SxmTransaction(TestDatabaseName);
        var result = ctx2.GetTable<SimpleEntity>().SingleOrDefault(e => e.id == entityId);
        result.Should().NotBeNull("delete was rolled back");

        // Cleanup
        await ctx2.DeleteAsync(result!);
    }

    #endregion

    #region Transaction Atomicity Tests

    [Fact]
    public async Task BulkOperations_MixedWithEntityDML_ShouldBeAtomic()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Create initial entities
        var entities = new[]
        {
            new SimpleEntity { Name = $"Atomic_{uniquePrefix}_1", Age = 10 },
            new SimpleEntity { Name = $"Atomic_{uniquePrefix}_2", Age = 20 },
            new SimpleEntity { Name = $"Atomic_{uniquePrefix}_3", Age = 30 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Mix entity DML with bulk operations in one transaction; all execute immediately
        var newEntity = new SimpleEntity { Name = $"Atomic_{uniquePrefix}_4", Age = 40 };
        await ctx.InsertAsync(newEntity);  // Entity insert

        await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Atomic_{uniquePrefix}") && e.Age == 10)
            .Set(e => e.Age, 100)
            .UpdateAsync();  // Bulk update

        await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Atomic_{uniquePrefix}") && e.Age == 20)
            .DeleteAsync();  // Bulk delete

        // Assert - all operations are visible within the same transaction/connection
        var results = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Atomic_{uniquePrefix}"))
            .OrderBy(e => e.Name)
            .ToList();

        results.Should().HaveCount(3, "1 deleted, 3 remain including 1 inserted");
        results.Should().Contain(e => e.Age == 100, "bulk update applied");
        results.Should().NotContain(e => e.Age == 20, "bulk delete applied");
        results.Should().Contain(e => e.Age == 40, "entity insert applied");

        // Cleanup
        foreach (var entity in results)
        {
            await ctx.DeleteAsync(entity);
        }
    }

    [Fact]
    public async Task BulkOperations_RollbackAsync_ShouldRollbackAll()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        long entityId;

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var entity = new SimpleEntity { Name = $"FailTest_{uniquePrefix}", Age = 10 };
            await ctx.InsertAsync(entity);
            entityId = entity.id;
        } // auto-commit

        // Act - bulk update, then explicit rollback
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == entityId)
                .Set(e => e.Age, 999)
                .UpdateAsync();

            await ctx.RollbackTransactionAsync();
        }

        // Assert - Bulk update should NOT be applied
        await using var ctx2 = new SxmTransaction(TestDatabaseName);
        var result = ctx2.GetTable<SimpleEntity>()
            .Single(e => e.id == entityId);

        result.Age.Should().Be(10, "bulk update was rolled back");

        // Cleanup
        await ctx2.DeleteAsync(result);
    }

    #endregion

    #region Explicit Commit Tests

    [Fact]
    public async Task CommitAsync_ShouldPersistImmediately()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entity = new SimpleEntity { Name = $"Commit_{uniquePrefix}", Age = 10 };
        await ctx.InsertAsync(entity);

        // Act - commit early
        await ctx.CommitTransactionAsync();

        // Assert - visible from another context before the first is disposed
        await using var ctx2 = new SxmTransaction(TestDatabaseName);
        var result = ctx2.GetTable<SimpleEntity>().SingleOrDefault(e => e.id == entity.id);
        result.Should().NotBeNull("explicit commit persisted the insert");

        // Cleanup
        await ctx2.DeleteAsync(result!);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task BulkUpdate_LargeDataset_ShouldBeEfficient()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Insert 100 entities
        for (int i = 0; i < 100; i++)
        {
            await ctx.InsertAsync(new SimpleEntity
            {
                Name = $"Perf_{uniquePrefix}_{i}",
                Age = i
            });
        }

        // Act - Bulk update all 100 entities
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Perf_{uniquePrefix}"))
            .Set(e => e.Age, e => e.Age + 1000)
            .UpdateAsync();

        stopwatch.Stop();

        // Assert
        updated.Should().Be(100);

        var updatedRows = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Perf_{uniquePrefix}"))
            .ToList();

        updatedRows.Should().HaveCount(100);
        updatedRows.Should().OnlyContain(e => e.Age >= 1000);

        // Performance assertion (bulk update should be fast)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            "bulk update of 100 rows should complete in under 1 second");

        // Cleanup
        await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Perf_{uniquePrefix}"))
            .DeleteAsync();
    }

    #endregion
}
