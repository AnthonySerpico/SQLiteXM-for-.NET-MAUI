using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for LINQ query support via SxmLinqDbContext.
/// These tests clean data before each test for isolation.
/// </summary>
[Collection("Sequential")]
public class LinqContextTests : TestBase
{
    public LinqContextTests()
    {
        // Clean data before each test 
        CleanupTableDataAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetTable_ShouldReturnQueryableTable()
    {
        // Arrange - data cleaned by constructor

        // Create and save some test data
        var entity1 = new SimpleEntity { Name = "Alice", Age = 25, IsActive = true };
        var entity2 = new SimpleEntity { Name = "Bob", Age = 30, IsActive = true };
        var entity3 = new SimpleEntity { Name = "Charlie", Age = 35, IsActive = false };

        await entity1.SaveAsync();
        await entity2.SaveAsync();
        await entity3.SaveAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var table = context.GetTable<SimpleEntity>();

            // Assert
            table.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LinqQuery_Where_ShouldFilterResults()
    {
        // Arrange - data cleaned by constructor

        var entity1 = new SimpleEntity { Name = "Young", Age = 20, IsActive = true };
        var entity2 = new SimpleEntity { Name = "Old", Age = 60, IsActive = true };

        await entity1.SaveAsync();
        await entity2.SaveAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var results = context.GetTable<SimpleEntity>()
            .Where(e => e.Age > 50)
            .ToList();

            // Assert
            results.Should().HaveCount(1);
            results[0].Name.Should().Be("Old");
        }
    }

    [Fact]
    public async Task LinqQuery_OrderBy_ShouldSortResults()
    {
        // Arrange - data cleaned by constructor

        var entity1 = new SimpleEntity { Name = "Charlie", Age = 35 };
        var entity2 = new SimpleEntity { Name = "Alice", Age = 25 };
        var entity3 = new SimpleEntity { Name = "Bob", Age = 30 };

        await entity1.SaveAsync();
        await entity2.SaveAsync();
        await entity3.SaveAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var results = context.GetTable<SimpleEntity>()
                .OrderBy(e => e.Age)
                .ToList();

            // Assert
            results.Should().HaveCount(3);
            results[0].Name.Should().Be("Alice");
            results[1].Name.Should().Be("Bob");
            results[2].Name.Should().Be("Charlie");
        }
    }

    [Fact]
    public async Task LinqQuery_Select_ShouldProjectProperties()
    {
        // Arrange - data cleaned by constructor

        var entity = new SimpleEntity { Name = "Test User", Age = 28, IsActive = true };
        await entity.SaveAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var names = context.GetTable<SimpleEntity>()
                .Select(e => e.Name)
                .ToList();

            // Assert
            names.Should().Contain("Test User");
        }
    }

    [Fact]
    public async Task LinqQuery_FirstOrDefault_ShouldReturnSingleEntity()
    {
        // Arrange - data cleaned by constructor

        var entity = new SimpleEntity { Name = "Single", Age = 42, IsActive = true };
        await entity.SaveAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var result = context.GetTable<SimpleEntity>()
                .FirstOrDefault(e => e.Age == 42);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Single");
        }
    }

    [Fact]
    public async Task LinqQuery_Count_ShouldReturnCorrectNumber()
    {
        // Arrange - data cleaned by constructor

        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        for (int i = 0; i < 5; i++)
        {
            var entity = new SimpleEntity { Name = $"Entity {i}", Age = i * 10 };
            await entity.SaveAsync(transaction);
        }

        await transaction.CommitTransactionAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var count = context.GetTable<SimpleEntity>().Count();

            // Assert
            count.Should().Be(5);
        }
    }

    [Fact]
    public async Task LinqQuery_ComplexFilter_ShouldWorkCorrectly()
    {
        // Arrange - data cleaned by constructor

        var entities = new[]
        {
            new SimpleEntity { Name = "Active Young", Age = 20, IsActive = true },
            new SimpleEntity { Name = "Inactive Young", Age = 25, IsActive = false },
            new SimpleEntity { Name = "Active Old", Age = 60, IsActive = true },
            new SimpleEntity { Name = "Inactive Old", Age = 65, IsActive = false }
        };

        var connection = new SxmConnection(TestDatabaseName, shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        foreach (var entity in entities)
        {
            await entity.SaveAsync(transaction);
        }

        await transaction.CommitTransactionAsync();

        // Act
        await using (var context = new SxmLinqDbContext(TestDatabaseName))
        {
            var results = context.GetTable<SimpleEntity>()
                .Where(e => e.IsActive && e.Age > 30)
                .ToList();

            // Assert
            results.Should().HaveCount(1);
            results[0].Name.Should().Be("Active Old");
        }
    }
}
