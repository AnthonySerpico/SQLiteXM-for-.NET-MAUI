using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for LINQ query behavior with transaction rollback and commit scenarios.
/// Verifies that rolled-back transactions are not visible via LINQ context queries.
/// </summary>
[Collection("Sequential")]
public class LinqTransactionTests : TestBase
{
    [Fact]
    public async Task LinqQuery_AfterRollback_ShouldNotShowInsertedRecords()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        string uniqueName = "RollbackTest_" + Guid.NewGuid().ToString("N");
        int beforeCount;

        // Capture count before transaction
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            beforeCount = ctx.GetTable<SimpleEntity>().Count(e => e.Name == uniqueName);
        }

        // Act - Insert inside transaction and rollback
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            var entity = new SimpleEntity { Name = uniqueName, Age = 42, IsActive = true };
            await entity.SaveAsync(transaction);

            entity.id.Should().BeGreaterThan(0, "entity should receive an ID after insert");

            // Explicit rollback
            await transaction.RollbackTransactionAsync();
        }

        // Assert - Verify no new rows were persisted via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            int afterCount = ctx.GetTable<SimpleEntity>().Count(e => e.Name == uniqueName);
            afterCount.Should().Be(beforeCount, "rollback should prevent any records from being persisted");
        }
    }

    [Fact]
    public async Task LinqQuery_AfterCommit_ShouldShowInsertedRecords()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        string uniqueName = "CommitTest_" + Guid.NewGuid().ToString("N");
        int beforeCount;

        // Capture count before transaction
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            beforeCount = ctx.GetTable<SimpleEntity>().Count(e => e.Name == uniqueName);
        }

        // Act - Insert inside transaction and commit
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            var entity = new SimpleEntity { Name = uniqueName, Age = 55, IsActive = true };
            await entity.SaveAsync(transaction);

            // Explicit commit
            await transaction.CommitTransactionAsync();
        }

        // Assert - Verify record is visible via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            int afterCount = ctx.GetTable<SimpleEntity>().Count(e => e.Name == uniqueName);
            afterCount.Should().Be(beforeCount + 1, "commit should persist the record");

            var retrieved = ctx.GetTable<SimpleEntity>()
                .FirstOrDefault(e => e.Name == uniqueName);

            retrieved.Should().NotBeNull();
            retrieved!.Age.Should().Be(55);
        }
    }

    [Fact]
    public async Task LinqQuery_MultipleEntitiesRollback_ShouldNotShowAnyRecords()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        string parentName = "ParentRollback_" + Guid.NewGuid().ToString("N");
        string childName = "ChildRollback_" + Guid.NewGuid().ToString("N");

        int beforeParentCount, beforeChildCount;

        // Capture counts before transaction
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            beforeParentCount = ctx.GetTable<ParentEntity>().Count(p => p.ParentName == parentName);
            beforeChildCount = ctx.GetTable<ChildEntity>().Count(c => c.ChildName == childName);
        }

        // Act - Insert parent and child in transaction, then rollback
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            var parent = new ParentEntity { ParentName = parentName };
            await parent.SaveAsync(transaction);

            parent.id.Should().BeGreaterThan(0, "parent should receive an ID");

            var child = new ChildEntity { ChildName = childName, ParentId = parent.id };
            await child.SaveAsync(transaction);

            child.id.Should().BeGreaterThan(0, "child should receive an ID");

            // Explicit rollback
            await transaction.RollbackTransactionAsync();
        }

        // Assert - Verify no records were persisted via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            int afterParentCount = ctx.GetTable<ParentEntity>().Count(p => p.ParentName == parentName);
            int afterChildCount = ctx.GetTable<ChildEntity>().Count(c => c.ChildName == childName);

            afterParentCount.Should().Be(beforeParentCount, 
                "rollback should prevent parent record from being persisted");
            afterChildCount.Should().Be(beforeChildCount, 
                "rollback should prevent child record from being persisted");
        }
    }

    [Fact]
    public async Task LinqQuery_UpdateRollback_ShouldNotShowChanges()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Create and persist an entity
        var entity = new SimpleEntity { Name = "Original", Age = 30, IsActive = true };
        await entity.SaveAsync();

        long entityId = entity.id;
        string originalName = entity.Name!;

        // Act - Update inside transaction and rollback
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            entity.Name = "Modified";
            entity.Age = 99;
            await entity.SaveAsync(transaction);

            // Explicit rollback
            await transaction.RollbackTransactionAsync();
        }

        // Assert - Verify changes were not persisted via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            var retrieved = ctx.GetTable<SimpleEntity>()
                .FirstOrDefault(e => e.id == entityId);

            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be(originalName, "rollback should revert name change");
            retrieved.Age.Should().Be(30, "rollback should revert age change");
        }
    }

    [Fact]
    public async Task LinqQuery_DeleteRollback_ShouldStillShowRecord()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Create and persist an entity
        var entity = new SimpleEntity { Name = "ToDelete", Age = 45, IsActive = true };
        await entity.SaveAsync();

        long entityId = entity.id;

        // Verify it exists before transaction
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            ctx.GetTable<SimpleEntity>()
                .Any(e => e.id == entityId)
                .Should().BeTrue("entity should exist before delete transaction");
        }

        // Act - Delete inside transaction and rollback
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            await entity.DeleteAsync(transaction);

            // Explicit rollback
            await transaction.RollbackTransactionAsync();
        }

        // Assert - Verify record still exists via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            var retrieved = ctx.GetTable<SimpleEntity>()
                .FirstOrDefault(e => e.id == entityId);

            retrieved.Should().NotBeNull("rollback should prevent deletion");
            retrieved!.Name.Should().Be("ToDelete");
        }
    }

    [Fact]
    public async Task LinqQuery_MixedOperationsRollback_ShouldRevertAll()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Create one existing entity
        var existingEntity = new SimpleEntity { Name = "Existing", Age = 20, IsActive = true };
        await existingEntity.SaveAsync();
        long existingId = existingEntity.id;

        string newEntityName = "NewInRollback_" + Guid.NewGuid().ToString("N");

        // Act - Perform insert, update, and delete in one transaction, then rollback
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using (var transaction = await SxmSqlTransaction.CreateAsync(connection))
        {
            // Insert a new entity
            var newEntity = new SimpleEntity { Name = newEntityName, Age = 100, IsActive = false };
            await newEntity.SaveAsync(transaction);

            // Update existing entity
            existingEntity.Age = 999;
            await existingEntity.SaveAsync(transaction);

            // Delete existing entity
            await existingEntity.DeleteAsync(transaction);

            // Rollback everything
            await transaction.RollbackTransactionAsync();
        }

        // Assert - Verify all operations were rolled back via LINQ
        using (var ctx = new SxmDbContext(TestDatabaseName))
        {
            // New entity should not exist
            ctx.GetTable<SimpleEntity>()
                .Any(e => e.Name == newEntityName)
                .Should().BeFalse("new entity insert should be rolled back");

            // Existing entity should still exist with original values
            var retrieved = ctx.GetTable<SimpleEntity>()
                .FirstOrDefault(e => e.id == existingId);

            retrieved.Should().NotBeNull("delete should be rolled back");
            retrieved!.Age.Should().Be(20, "update should be rolled back");
        }
    }
}
