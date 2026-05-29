using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for fail-fast behavior when using deterministic schema registration incorrectly.
/// </summary>
[Collection("Sequential")]
public class FailFastTests : TestBase
{
    [Fact]
    public async Task RegisterSchemaAsync_WithNonEntityType_ShouldThrow()
    {
        // Arrange
        Type nonEntityType = typeof(string);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await SxmDatabase.RegisterEntitiesAsync(nonEntityType));

        Assert.Contains("must inherit from SxmEntity", ex.Message);
    }

    [Fact]
    public async Task RegisterSchemaAsync_WithAbstractType_ShouldThrow()
    {
        // Arrange - AbstractEntity is defined below
        Type abstractType = typeof(AbstractEntity);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await SxmDatabase.RegisterEntitiesAsync(abstractType));

        Assert.Contains("cannot be abstract", ex.Message);
    }

    [Fact]
    public void EntityConstructor_WithoutRegistration_ShouldThrow()
    {
        // Arrange - UnregisteredEntity is deliberately not registered

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => { var entity = new UnregisteredEntity(); });

        Assert.Contains("has not been registered", ex.Message);
        Assert.Contains("RegisterEntitiesAsync", ex.Message);
    }

    [Fact]
    public async Task EntityConstructor_WithRegistration_ShouldSucceed()
    {
        // Arrange - RegisteredEntity will be registered in this test
        await SxmDatabase.RegisterEntitiesAsync(typeof(RegisteredEntity));

        // Act & Assert - should not throw
        var entity = new RegisteredEntity { Name = "Test" };
        Assert.NotNull(entity);
        Assert.Equal("Test", entity.Name);
    }

    [Fact]
    public async Task EntitySave_WithRegistration_ShouldSucceed()
    {
        // Arrange
        await SxmDatabase.RegisterEntitiesAsync(typeof(RegisteredEntity));
        var entity = new RegisteredEntity { Name = "TestSave" };

        // Act
        await entity.SaveAsync();

        // Assert
        Assert.True(entity.id > 0);
    }

    #region Test Entity Definitions

    /// <summary>
    /// Abstract entity type used for testing fail-fast on abstract type registration.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public abstract class AbstractEntity : SxmEntity
    {
        public string? Name { get; set; }
    }

    /// <summary>
    /// Unregistered entity type used for testing fail-fast on instantiation without registration.
    /// NOTE: This type is deliberately NOT added to TestBase registration list.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class UnregisteredEntity : SxmEntity
    {
        public string? Name { get; set; }
    }

    /// <summary>
    /// Entity type that will be registered during tests to verify correct behavior.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class RegisteredEntity : SxmEntity
    {
        public string? Name { get; set; }
    }

    #endregion
}
