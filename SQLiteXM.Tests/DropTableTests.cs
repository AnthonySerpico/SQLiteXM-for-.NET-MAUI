using FluentAssertions;
using Microsoft.Data.Sqlite;
using SQLiteXM;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for SxmStatement.DropTableAsync public API.
/// Validates table dropping behavior including force flag and foreign key handling.
/// </summary>
[Collection("SQLiteXM Tests")]
public class DropTableTests : TestBase
{
    [Fact]
    public async Task DropTableAsync_NonExistentTable_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync("NonExistentTable_12345");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_EmptyTable_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity1 { Name = "Test" };
        await entity.SaveAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity1));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_TableWithData_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var e1 = new DropTestEntity2 { Name = "Test1" };
        var e2 = new DropTestEntity2 { Name = "Test2" };
        await e1.SaveAsync();
        await e2.SaveAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity2));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_ParentTableWithForeignKey_ForceDefault_ShouldThrow()
    {
        await InitializeSqliteXMAsync();
        var parent = new DropTestParentEntity3 { ParentName = "Parent1" };
        await parent.SaveAsync();
        var child = new DropTestChildEntity3 { ChildName = "Child1", ParentId = parent.id };
        await child.SaveAsync();

        // Attempting to drop parent table without force=true should throw due to FK constraint
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestParentEntity3));
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task DropTableAsync_ParentTableWithForeignKey_ForceTrue_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var parent = new DropTestParentEntity4 { ParentName = "Parent2" };
        await parent.SaveAsync();
        var child = new DropTestChildEntity4 { ChildName = "Child2", ParentId = parent.id };
        await child.SaveAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestParentEntity4), dbName: null, force: true);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_ChildTable_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var parent = new DropTestParentEntity5 { ParentName = "Parent3" };
        await parent.SaveAsync();
        var child = new DropTestChildEntity5 { ChildName = "Child3", ParentId = parent.id };
        await child.SaveAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestChildEntity5));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_DroppingSameTableTwice_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity8 { Name = "Test" };
        await entity.SaveAsync();
        await SxmStatement.DropTableAsync(nameof(DropTestEntity8));
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity8));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_TableNameWithSpecialCharacters_ShouldHandleCorrectly()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity9 { Name = "Test" };
        await entity.SaveAsync();

        // Table name with special characters should be handled via QuoteIdentifier
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity9));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_NullTableName_ShouldThrow()
    {
        await InitializeSqliteXMAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Identifier cannot be null or whitespace*");
    }

    [Fact]
    public async Task DropTableAsync_WhitespaceTableName_ShouldThrow()
    {
        await InitializeSqliteXMAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync("   ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Identifier cannot be null or whitespace*");
    }

    [Fact]
    public async Task DropTableAsync_EmptyStringTableName_ShouldThrow()
    {
        await InitializeSqliteXMAsync();
        Func<Task> act = async () => await SxmStatement.DropTableAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Identifier cannot be null or whitespace*");
    }

    [Fact]
    public async Task DropTableAsync_MultipleTablesInSequence_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        var entity1 = new DropTestEntity10 { Name = "Test1" };
        var entity2 = new DropTestEntity11 { Name = "Test2" };
        var entity3 = new DropTestEntity12 { Name = "Test3" };

        await entity1.SaveAsync();
        await entity2.SaveAsync();
        await entity3.SaveAsync();

        await SxmStatement.DropTableAsync(nameof(DropTestEntity10));
        await SxmStatement.DropTableAsync(nameof(DropTestEntity11));
        await SxmStatement.DropTableAsync(nameof(DropTestEntity12));

        // All drops should succeed
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity10));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_TableWithIndexes_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity13 { Name = "Test", IndexedField = "Value" };
        await entity.SaveAsync();

        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity13));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_ForceFalse_WithChildTable_ShouldThrow()
    {
        await InitializeSqliteXMAsync();

        var parent = new DropTestParentEntity14 { ParentName = "Parent" };
        await parent.SaveAsync();
        var child = new DropTestChildEntity14 { ChildName = "Child", ParentId = parent.id };
        await child.SaveAsync();

        // Explicitly setting force=false should throw when child records exist
        Func<Task> act = async () => await SxmStatement.DropTableAsync(
            nameof(DropTestParentEntity14), 
            dbName: null, 
            force: false);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task DropTableAsync_WithCustomDbName_ShouldUseCorrectDatabase()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity15 { Name = "Test" };
        await entity.SaveAsync();

        // Using the test database name explicitly
        Func<Task> act = async () => await SxmStatement.DropTableAsync(
            nameof(DropTestEntity15), 
            dbName: TestDatabaseName);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_ComplexForeignKeyChain_WithForce_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        // Create a chain: GrandParent -> Parent -> Child
        var grandParent = new DropTestGrandParentEntity16 { Name = "GrandParent" };
        await grandParent.SaveAsync();

        var parent = new DropTestParentEntity16 { Name = "Parent", GrandParentId = grandParent.id };
        await parent.SaveAsync();

        var child = new DropTestChildEntity16 { Name = "Child", ParentId = parent.id };
        await child.SaveAsync();

        // Drop the middle table with force
        Func<Task> act = async () => await SxmStatement.DropTableAsync(
            nameof(DropTestParentEntity16), 
            force: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_ConcurrentDrops_DifferentTables_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        var entity1 = new DropTestEntity17 { Name = "Test1" };
        var entity2 = new DropTestEntity18 { Name = "Test2" };

        await entity1.SaveAsync();
        await entity2.SaveAsync();

        // Drop tables concurrently
        var task1 = SxmStatement.DropTableAsync(nameof(DropTestEntity17));
        var task2 = SxmStatement.DropTableAsync(nameof(DropTestEntity18));

        await Task.WhenAll(task1, task2);

        // Both should succeed
        task1.IsCompletedSuccessfully.Should().BeTrue();
        task2.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task DropTableAsync_TableWithConstraints_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntity19 { Name = "Test", UniqueField = "Unique1" };
        await entity.SaveAsync();

        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity19));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_VeryLongTableName_ShouldHandleCorrectly()
    {
        await InitializeSqliteXMAsync();
        var entity = new DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling { Name = "Test" };
        await entity.SaveAsync();

        Func<Task> act = async () => await SxmStatement.DropTableAsync(
            nameof(DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_MultipleChildTables_WithForce_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        var parent = new DropTestParentEntity20 { Name = "Parent" };
        await parent.SaveAsync();

        var child1 = new DropTestChildEntity20A { Name = "Child1", ParentId = parent.id };
        var child2 = new DropTestChildEntity20B { Name = "Child2", ParentId = parent.id };

        await child1.SaveAsync();
        await child2.SaveAsync();

        // Drop parent table with multiple children
        Func<Task> act = async () => await SxmStatement.DropTableAsync(
            nameof(DropTestParentEntity20), 
            force: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_AfterSaveAndDelete_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        var entity = new DropTestEntity21 { Name = "Test" };
        await entity.SaveAsync();
        await entity.DeleteAsync();

        // Drop table even after entity deleted
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity21));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DropTableAsync_WithMultipleRecords_ShouldSucceed()
    {
        await InitializeSqliteXMAsync();

        // Create and save multiple entities with data
        var entity1 = new DropTestEntity22 { Name = "Test1", Value = 100 };
        var entity2 = new DropTestEntity22 { Name = "Test2", Value = 200 };
        var entity3 = new DropTestEntity22 { Name = "Test3", Value = 300 };
        await entity1.SaveAsync();
        await entity2.SaveAsync();
        await entity3.SaveAsync();

        // Verify entities were saved with valid IDs
        entity1.id.Should().BeGreaterThan(0);
        entity2.id.Should().BeGreaterThan(0);
        entity3.id.Should().BeGreaterThan(0);

        // Drop the table with all its data
        Func<Task> act = async () => await SxmStatement.DropTableAsync(nameof(DropTestEntity22));
        await act.Should().NotThrowAsync("dropping table with multiple records should succeed");
    }

    #region Test Entity Definitions

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity1 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity2 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity3 : SxmEntity { public string? ParentName { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity3 : SxmEntity 
    { 
        public string? ChildName { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity3))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity4 : SxmEntity { public string? ParentName { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity4 : SxmEntity 
    { 
        public string? ChildName { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity4))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity5 : SxmEntity { public string? ParentName { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity5 : SxmEntity 
    { 
        public string? ChildName { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity5))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity8 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity9 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity10 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity11 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity12 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity13 : SxmEntity 
    { 
        public string? Name { get; set; }
        public string? IndexedField { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity14 : SxmEntity { public string? ParentName { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity14 : SxmEntity 
    { 
        public string? ChildName { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity14))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity15 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestGrandParentEntity16 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity16 : SxmEntity 
    { 
        public string? Name { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestGrandParentEntity16))]
        public long GrandParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity16 : SxmEntity 
    { 
        public string? Name { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity16))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity17 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity18 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity19 : SxmEntity 
    { 
        public string? Name { get; set; }
        public string? UniqueField { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling : SxmEntity
    { 
        public string? Name { get; set; } 
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestParentEntity20 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity20A : SxmEntity 
    { 
        public string? Name { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity20))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestChildEntity20B : SxmEntity 
    { 
        public string? Name { get; set; }
        [ForeignKey(foreignTable: nameof(DropTestParentEntity20))]
        public long ParentId { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity21 : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DropTestEntity22 : SxmEntity 
    { 
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    #endregion
}
