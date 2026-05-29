using SQLiteXM;
using Xunit;

namespace SQLiteXM.Tests;

[Collection("Sequential")]
public class SubmitChangesRefactorTests : TestBase
{
    public SubmitChangesRefactorTests()
    {
        // Ensure database is initialized and all entities are registered
        // Multi-database tests call ResetForTestingAsync() which clears registrations
        InitializeSqliteXMAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SubmitChanges_AllSucceeded_GetErrorSummary_ShouldReturnSuccessMessage()
    {
        // Arrange
        using var context = new SxmLinqDbContext();
        context.InsertOnSubmit(new SimpleEntity { Name = "Success1" });
        context.InsertOnSubmit(new SimpleEntity { Name = "Success2" });

        // Act
        var result = await context.SubmitChangesAsync();

        // Assert
        Assert.True(result.AllSucceeded);
        Assert.Equal("All operations succeeded.", result.GetErrorSummary());
    }

    [Fact]
    public async Task SubmitChanges_WithThrowIfFailed_PassesWhenAllSucceed()
    {
        // Arrange
        using var context = new SxmLinqDbContext();

        var entity = new SimpleEntity { Name = "Test" };
        context.InsertOnSubmit(entity);

        // Act: ThrowIfFailed should not throw when all operations succeed
        (await context.SubmitChangesAsync()).ThrowIfFailed();

        // Assert
        Assert.True(entity.id > 0);
    }

    [Fact]
    public async Task SubmitChanges_ManualInspection_AllowsGracefulHandling()
    {
        // Arrange: Create a simple success case
        using var context = new SxmLinqDbContext();
        context.InsertOnSubmit(new SimpleEntity { Name = "Test1" });
        context.InsertOnSubmit(new SimpleEntity { Name = "Test2" });

        // Act: Manual inspection
        var result = await context.SubmitChangesAsync();

        // Assert: Should succeed
        Assert.True(result.AllSucceeded);
        Assert.False(result.AnyFailed);
        Assert.Equal(2, result.Succeeded.Count);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public async Task SubmitChanges_ResultProperties_ShouldBeCorrect()
    {
        // Arrange
        using var context = new SxmLinqDbContext();
        context.InsertOnSubmit(new SimpleEntity { Name = "Test1" });
        context.InsertOnSubmit(new SimpleEntity { Name = "Test2" });
        context.InsertOnSubmit(new SimpleEntity { Name = "Test3" });

        // Act
        var result = await context.SubmitChangesAsync();

        // Assert
        Assert.True(result.AllSucceeded);
        Assert.False(result.AnyFailed);
        Assert.Equal(3, result.TotalOperations);
        Assert.Equal(3, result.Succeeded.Count);
        Assert.Empty(result.Failed);
        Assert.False(result.Partial);
    }
}
