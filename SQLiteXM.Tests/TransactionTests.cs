using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for transaction support with entities.
/// </summary>
[Collection("SQLiteXM Tests")]
public class TransactionTests : TestBase
{
    [Fact]
    public async Task SaveAsync_WithTransaction_ShouldCommit()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "Transactional", Age = 35 };

        // Act
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmTransaction.CreateAsync(connection);
        await entity.SaveAsync(transaction);
        await transaction.CommitTransactionAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify data was committed to database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("committed entity should exist in database");
        retrieved!.Name.Should().Be("Transactional");
        retrieved.Age.Should().Be(35);
    }

    [Fact]
    public async Task SaveAsync_WithTransactionRollback_ShouldNotPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "Rollback Test", Age = 40 };

        // Act
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmTransaction.CreateAsync(connection);
        await entity.SaveAsync(transaction);
        var tempId = entity.id;
        await transaction.RollbackTransactionAsync();

        // Assert - id gets populated during save but rollback should prevent persistence
        tempId.Should().BeGreaterThan(0, "ID is generated during save operation");

        // Assert - CRITICAL: Verify data was NOT persisted to database after rollback
        await VerifyEntityNotInDbAsync<SimpleEntity>(tempId);
    }

    [Fact]
    public async Task MultipleOperations_InTransaction_ShouldBeAtomic()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity1 = new SimpleEntity { Name = "Entity 1", Age = 10 };
        var entity2 = new SimpleEntity { Name = "Entity 2", Age = 20 };

        // Act
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmTransaction.CreateAsync(connection);
        await entity1.SaveAsync(transaction);
        await entity2.SaveAsync(transaction);
        await transaction.CommitTransactionAsync();

        // Assert - Verify IDs were populated
        entity1.id.Should().BeGreaterThan(0);
        entity2.id.Should().BeGreaterThan(0);

        // Assert - Verify both entities were committed atomically to database
        var retrieved1 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity1.id);
        retrieved1.Should().NotBeNull("first entity should exist in database");
        retrieved1!.Name.Should().Be("Entity 1");

        var retrieved2 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity2.id);
        retrieved2.Should().NotBeNull("second entity should exist in database");
        retrieved2!.Name.Should().Be("Entity 2");
    }

    [Fact]
    public async Task DeleteAsync_WithTransaction_ShouldCommit()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "To Delete", Age = 50 };
        await entity.SaveAsync();
        var savedId = entity.id;

        // Verify entity exists before deletion
        var beforeDelete = await VerifyEntityExistsInDbAsync<SimpleEntity>(savedId);
        beforeDelete.Should().NotBeNull("entity should exist before transaction");

        // Act
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmTransaction.CreateAsync(connection);
        await entity.DeleteAsync(transaction);
        await transaction.CommitTransactionAsync();

        // Assert - ID should remain in memory
        entity.id.Should().Be(savedId);

        // Assert - Verify entity was deleted from database after commit
        await VerifyEntityNotInDbAsync<SimpleEntity>(savedId);
    }

    [Fact]
    public async Task AmbientTransaction_ShouldUseCurrentTransaction()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity1 = new SimpleEntity { Name = "Ambient 1", Age = 15 };
        var entity2 = new SimpleEntity { Name = "Ambient 2", Age = 25 };

        // Act
        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmTransaction.CreateAsync(connection);

        // SaveAsync() without transaction parameter uses ambient transaction
        await entity1.SaveAsync();
        await entity2.SaveAsync();

        await transaction.CommitTransactionAsync();

        // Assert - Verify IDs were populated
        entity1.id.Should().BeGreaterThan(0);
        entity2.id.Should().BeGreaterThan(0);

        // Assert - Verify both entities were committed via ambient transaction
        var retrieved1 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity1.id);
        retrieved1.Should().NotBeNull("first entity should exist in database");
        retrieved1!.Name.Should().Be("Ambient 1");

        var retrieved2 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity2.id);
        retrieved2.Should().NotBeNull("second entity should exist in database");
        retrieved2!.Name.Should().Be("Ambient 2");
    }
}
