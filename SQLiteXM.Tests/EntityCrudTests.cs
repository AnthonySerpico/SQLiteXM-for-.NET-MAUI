using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for entity CRUD operations (Create, Read, Update, Delete).
/// </summary>
[Collection("SQLiteXM Tests")]
public class EntityCrudTests : TestBase
{
    [Fact]
    public async Task SaveAsync_NewEntity_ShouldInsertAndPopulateId()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity
        {
            Name = "John Doe",
            Age = 30,
            IsActive = true
        };

        // Act
        await entity.SaveAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0, "id should be auto-populated after insert");

        // Assert - Verify data was actually written to database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.Name.Should().Be("John Doe");
        retrieved.Age.Should().Be(30);
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ExistingEntity_ShouldUpdate()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity
        {
            Name = "Original Name",
            Age = 25,
            IsActive = true
        };

        await entity.SaveAsync();
        var originalId = entity.id;

        // Act - Modify and save again
        entity.Name = "Updated Name";
        entity.Age = 30;
        await entity.SaveAsync();

        // Assert - Verify ID stayed the same
        entity.id.Should().Be(originalId, "id should remain the same after update");

        // Assert - Verify updated data in database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.Name.Should().Be("Updated Name", "name should be updated in database");
        retrieved.Age.Should().Be(30, "age should be updated in database");

        // Assert - Verify only one record exists (no duplicate insert)
        var allEntities = GetAllEntitiesFromDb<SimpleEntity>();
        allEntities.Count(e => e.id == originalId).Should().Be(1, "only one record with this ID should exist");
    }

    [Fact]
    public async Task InsertOrUpdateAsync_NewEntity_ShouldInsert()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "New Entity", Age = 20 };

        // Act
        await entity.InsertOrUpdateAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify data in database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.Name.Should().Be("New Entity");
        retrieved.Age.Should().Be(20);
    }

    [Fact]
    public async Task InsertOrUpdateAsync_ExistingEntity_ShouldUpdate()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "Original", Age = 25 };
        await entity.SaveAsync();
        var originalId = entity.id;

        // Act
        entity.Name = "Modified";
        await entity.InsertOrUpdateAsync();

        // Assert - Verify ID stayed the same
        entity.id.Should().Be(originalId);

        // Assert - Verify updated data in database
        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.Name.Should().Be("Modified", "name should be updated in database");
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntity_ShouldRemoveFromDatabase()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "To Delete", Age = 40 };
        await entity.SaveAsync();
        var savedId = entity.id;

        // Verify entity exists before deletion
        var beforeDelete = await VerifyEntityExistsInDbAsync<SimpleEntity>(savedId);
        beforeDelete.Should().NotBeNull("entity should exist before deletion");

        // Act
        await entity.DeleteAsync();

        // Assert - id should remain in memory
        entity.id.Should().Be(savedId);

        // Assert - Verify entity was actually deleted from database
        await VerifyEntityNotInDbAsync<SimpleEntity>(savedId);

        // Verify deletion by creating new entity - it should get a different id
        var newEntity = new SimpleEntity { Name = "After Delete" };
        await newEntity.SaveAsync();
        newEntity.id.Should().NotBe(savedId, "new entity should get different id");
    }

    [Fact]
    public async Task SaveAsync_MultipleEntities_ShouldHaveUniqueIds()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity1 = new SimpleEntity { Name = "Entity 1", Age = 10 };
        var entity2 = new SimpleEntity { Name = "Entity 2", Age = 20 };
        var entity3 = new SimpleEntity { Name = "Entity 3", Age = 30 };

        // Act
        await entity1.SaveAsync();
        await entity2.SaveAsync();
        await entity3.SaveAsync();

        // Assert - Verify IDs are unique and positive
        entity1.id.Should().BeGreaterThan(0);
        entity2.id.Should().BeGreaterThan(0);
        entity3.id.Should().BeGreaterThan(0);
        entity1.id.Should().NotBe(entity2.id);
        entity2.id.Should().NotBe(entity3.id);

        // Assert - Verify all three entities exist in database with correct data
        var retrieved1 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity1.id);
        retrieved1.Should().NotBeNull();
        retrieved1!.Name.Should().Be("Entity 1");
        retrieved1.Age.Should().Be(10);

        var retrieved2 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity2.id);
        retrieved2.Should().NotBeNull();
        retrieved2!.Name.Should().Be("Entity 2");

        var retrieved3 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity3.id);
        retrieved3.Should().NotBeNull();
        retrieved3!.Name.Should().Be("Entity 3");
    }

    [Fact]
    public async Task SaveAsync_AllDataTypes_ShouldPersistCorrectly()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var now = DateTime.UtcNow;
        var guid = Guid.NewGuid();
        var entity = new AllTypesEntity
        {
            IntValue = 42,
            StringValue = "Test String",
            BoolValue = true,
            DoubleValue = 3.14159,
            DecimalValue = 99.99m,
            GuidValue = guid,
            DateTimeValue = now,
            NullableInt = 100,
            BlobValue = new byte[] { 0x01, 0x02, 0x03 }
        };

        // Act
        await entity.SaveAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify all data types were persisted correctly in database
        var retrieved = await VerifyEntityExistsInDbAsync<AllTypesEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.IntValue.Should().Be(42);
        retrieved.StringValue.Should().Be("Test String");
        retrieved.BoolValue.Should().BeTrue();
        retrieved.DoubleValue.Should().BeApproximately(3.14159, 0.00001);
        retrieved.DecimalValue.Should().Be(99.99m);
        retrieved.GuidValue.Should().Be(guid);
        retrieved.DateTimeValue.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        retrieved.NullableInt.Should().Be(100);
        retrieved.BlobValue.Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public async Task SaveAsync_NullableFields_ShouldHandleNulls()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new AllTypesEntity
        {
            IntValue = 1,
            StringValue = null,
            NullableInt = null,
            NullableDateTime = null,
            NullableGuid = null
        };

        // Act
        await entity.SaveAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify null values were persisted correctly in database
        var retrieved = await VerifyEntityExistsInDbAsync<AllTypesEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.StringValue.Should().BeNull("null string should be persisted as null");
        retrieved.NullableInt.Should().BeNull("null int should be persisted as null");
        retrieved.NullableDateTime.Should().BeNull("null DateTime should be persisted as null");
        retrieved.NullableGuid.Should().BeNull("null Guid should be persisted as null");
    }

    [Fact]
    public async Task SaveAsync_TimeTypesAsText_ShouldPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var now = DateTime.Now;
        var entity = new TimeTypeTextEntity
        {
            DateTimeAsText = now,
            DateTimeOffsetAsText = DateTimeOffset.Now,
            TimeSpanAsText = TimeSpan.FromHours(2),
            DateOnlyAsText = new DateOnly(2024, 12, 25),
            TimeOnlyAsText = new TimeOnly(14, 30, 0),
            GuidAsText = Guid.NewGuid()
        };

        // Act
        await entity.SaveAsync();

        // Assert - Verify ID was populated
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify time types stored as TEXT were persisted correctly in database
        var retrieved = await VerifyEntityExistsInDbAsync<TimeTypeTextEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.DateTimeAsText.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        retrieved.DateOnlyAsText.Should().Be(new DateOnly(2024, 12, 25));
        retrieved.TimeOnlyAsText.Should().Be(new TimeOnly(14, 30, 0));
        retrieved.TimeSpanAsText.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentEntity_ShouldNotThrow()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity = new SimpleEntity { Name = "Never Saved" };
        // Don\'t save - id will be 0
        
        // Act & Assert - Should not throw
        var act = () => entity.DeleteAsync();
        await act.Should().NotThrowAsync("deleting non-existent entity should be a no-op");
    }

    [Fact]
    public async Task ConcurrentSaves_ShouldNotCorruptData()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var tasks = new List<Task<SimpleEntity>>();

        // Act - Save 10 entities concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var entity = new SimpleEntity
                {
                    Name = $"Concurrent Entity {index}",
                    Age = index * 10
                };
                await entity.SaveAsync();
                return entity;
            }));
        }

        var entities = await Task.WhenAll(tasks);

        // Assert - All saves should succeed
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());

        // Assert - Verify all entities were actually persisted to database
        foreach (var entity in entities)
        {
            var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
            retrieved.Should().NotBeNull($"entity {entity.id} should exist in database");
            retrieved!.Name.Should().StartWith("Concurrent Entity");
        }

        // Assert - Verify correct count in database
        var count = GetEntityCountFromDb<SimpleEntity>();
        count.Should().BeGreaterOrEqualTo(10, "at least 10 concurrent entities should be in database");
    }
}
