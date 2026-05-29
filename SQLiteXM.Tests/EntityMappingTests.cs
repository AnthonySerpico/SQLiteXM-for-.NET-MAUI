using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for entity property mapping functionality.
/// </summary>
[Collection("Sequential")]
public class EntityMappingTests : TestBase
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class SourceDto
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public string? ExtraField { get; set; }
    }

    [Fact]
    public async Task MapProperties_ShouldCopyMatchingProperties()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var source = new SourceDto
        {
            Name = "Mapped User",
            Age = 45,
            IsActive = true,
            ExtraField = "This won\'t map"
        };
        
        var target = new SimpleEntity();
        
        // Act
        target.MapProperties(source);
        
        // Assert
        target.Name.Should().Be("Mapped User");
        target.Age.Should().Be(45);
        target.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task MapAndSaveAsync_ShouldMapAndPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var source = new SourceDto
        {
            Name = "Map and Save",
            Age = 28,
            IsActive = false
        };

        var target = new SimpleEntity();

        // Act
        await target.MapAndSaveAsync(source);

        // Assert - Verify ID was populated
        target.id.Should().BeGreaterThan(0, "entity should be persisted");

        // Assert - Verify mapped data in memory
        target.Name.Should().Be("Map and Save");
        target.Age.Should().Be(28);
        target.IsActive.Should().BeFalse();

        // Assert - Verify data was actually persisted to database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(target.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.Name.Should().Be("Map and Save");
        retrieved.Age.Should().Be(28);
        retrieved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task MapProperties_WithNullSource_ShouldThrow()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var target = new SimpleEntity();
        
        // Act & Assert
        Action act = () => target.MapProperties(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MapProperties_WithMismatchedTypes_ShouldSkipProperty()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        var source = new { Name = "Test", Age = "Not an int" };  // Age is string, not int
        var target = new SimpleEntity();
        
        // Act
        target.MapProperties(source);
        
        // Assert - Name should map, Age should not
        target.Name.Should().Be("Test");
        target.Age.Should().Be(0, "mismatched type should not be copied");
    }
}
