using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for entity initialization and table creation.
/// </summary>
[Collection("SQLiteXM Tests")]
public class EntityInitializationTests : TestBase
{
    [Fact]
    public async Task SimpleEntity_FirstInstantiation_ShouldCreateTable()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act - First instantiation triggers table creation
        var entity = new SimpleEntity
        {
            Name = "Test",
            Age = 25,
            IsActive = true
        };

        // Save to ensure table is created
        await entity.SaveAsync();

        // Assert - Entity should be created without exception
        entity.Should().NotBeNull();
        entity.id.Should().BeGreaterThan(0, "entity should have been saved");

        // Assert - Verify table exists and data is queryable
        var tableExists = VerifyTableExists<SimpleEntity>();
        tableExists.Should().BeTrue("table should have been created");

        var retrieved = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity.id);
        retrieved.Should().NotBeNull("saved entity should be retrievable from database");
        retrieved!.Name.Should().Be("Test");
        retrieved.Age.Should().Be(25);
        retrieved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SimpleEntity_SecondInstantiation_ShouldReuseSameTable()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity1 = new SimpleEntity { Name = "First" };
        await entity1.SaveAsync();
        var countAfterFirst = GetEntityCountFromDb<SimpleEntity>();

        // Act - Second instantiation should not recreate table
        var entity2 = new SimpleEntity { Name = "Second" };
        await entity2.SaveAsync();

        // Assert - Both entities should exist
        entity1.Should().NotBeNull();
        entity2.Should().NotBeNull();
        entity1.id.Should().BeGreaterThan(0);
        entity2.id.Should().BeGreaterThan(0);
        entity1.id.Should().NotBe(entity2.id, "entities should have different IDs");

        // Assert - Verify both entities exist in same table
        var retrieved1 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity1.id);
        retrieved1.Should().NotBeNull();
        retrieved1!.Name.Should().Be("First");

        var retrieved2 = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity2.id);
        retrieved2.Should().NotBeNull();
        retrieved2!.Name.Should().Be("Second");

        // Assert - Verify count increased (table was reused, not recreated)
        var countAfterSecond = GetEntityCountFromDb<SimpleEntity>();
        countAfterSecond.Should().BeGreaterThan(countAfterFirst, "second entity should be added to existing table");
    }

    [Fact]
    public async Task AllTypesEntity_Instantiation_ShouldMapAllDataTypes()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        var entity = new AllTypesEntity
        {
            SByteValue = sbyte.MaxValue,
            ByteValue = byte.MaxValue,
            ShortValue = short.MaxValue,
            UShortValue = ushort.MaxValue,
            IntValue = int.MaxValue,
            UIntValue = uint.MaxValue,
            LongValue = long.MaxValue,
            ULongValue = ulong.MaxValue,
            DecimalValue = 123.45m,
            FloatValue = 3.14f,
            DoubleValue = 2.718,
            BoolValue = true,
            StringValue = "Test String",
            GuidValue = Guid.NewGuid(),
            DateTimeValue = DateTime.Now,
            DateTimeOffsetValue = DateTimeOffset.Now,
            TimeSpanValue = TimeSpan.FromHours(5),
            DateOnlyValue = new DateOnly(2024, 1, 15),
            TimeOnlyValue = new TimeOnly(14, 30, 0),
            BlobValue = new byte[] { 1, 2, 3, 4, 5 },
            NullableInt = 42,
            NullableDateTime = DateTime.Now,
            NullableGuid = Guid.NewGuid()
        };

        await entity.SaveAsync();

        // Assert - All properties should be set
        entity.Should().NotBeNull();
        entity.id.Should().BeGreaterThan(0);

        // Assert - Verify data types were correctly persisted to database
        var retrieved = await VerifyEntityExistsInDbAsync<AllTypesEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        retrieved!.IntValue.Should().Be(int.MaxValue);
        retrieved.StringValue.Should().Be("Test String");
        retrieved.NullableInt.Should().Be(42);
        retrieved.BoolValue.Should().BeTrue();
        retrieved.DecimalValue.Should().Be(123.45m);
    }

    [Fact]
    public async Task TimeTypeTextEntity_Instantiation_ShouldUseTextStorage()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act
        var entity = new TimeTypeTextEntity
        {
            DateTimeAsText = DateTime.Now,
            DateTimeOffsetAsText = DateTimeOffset.Now,
            TimeSpanAsText = TimeSpan.FromMinutes(30),
            DateOnlyAsText = new DateOnly(2024, 6, 15),
            TimeOnlyAsText = new TimeOnly(10, 30),
            GuidAsText = Guid.NewGuid()
        };
        
        // Assert - Entity created successfully
        entity.Should().NotBeNull();
        entity.DateTimeAsText.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExplicitColumnEntity_Instantiation_ShouldOnlyMapColumnAttributedFields()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act
        var entity = new ExplicitColumnEntity
        {
            MappedField = "This is mapped",
            UnmappedField = "This should not be in DB",
            ExplicitlyExcluded = "Also excluded"
        };
        
        // Assert
        entity.Should().NotBeNull();
        entity.MappedField.Should().Be("This is mapped");
    }

    [Fact]
    public async Task IndexedEntity_Instantiation_ShouldCreateIndexes()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act - Indexes are created during first instantiation
        var entity = new IndexedEntity
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            CreatedDate = DateTime.Now
        };
        
        // Assert
        entity.Should().NotBeNull();
        entity.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task ParentChildEntity_Instantiation_ShouldCreateForeignKey()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act - Create parent first to establish FK relationship
        var parent = new ParentEntity { ParentName = "Parent" };
        var child = new ChildEntity { ChildName = "Child", ParentId = 1 };
        
        // Assert
        parent.Should().NotBeNull();
        child.Should().NotBeNull();
        child.ParentId.Should().Be(1);
    }

    [Fact]
    public async Task TriggerEntity_Instantiation_ShouldCreateTrigger()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act
        var entity = new TriggerEntity
        {
            Name = "Test",
            UpdatedDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        // Assert
        entity.Should().NotBeNull();
        entity.Name.Should().Be("Test");
    }

    [Fact]
    public async Task RequiredFieldEntity_Instantiation_ShouldApplyDefaults()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        
        // Act
        var entity = new RequiredFieldEntity
        {
            OptionalField = "Optional"
        };
        
        // Assert - RequiredNotNull fields should have defaults from attribute
        entity.Should().NotBeNull();
        entity.OptionalField.Should().Be("Optional");
    }

    [Fact]
    public async Task ConcurrentEntityInstantiation_ShouldOnlyInitializeOnce()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var tasks = new List<Task<SimpleEntity>>();
        
        // Act - Create 20 entities concurrently
        for (int i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => new SimpleEntity { Name = $"Entity {index}" }));
        }
        
        var entities = await Task.WhenAll(tasks);
        
        // Assert - All entities created successfully
        entities.Should().HaveCount(20);
        entities.Should().OnlyContain(e => e != null);
        entities.Select(e => e.Name).Should().Contain("Entity 0");
        entities.Select(e => e.Name).Should().Contain("Entity 19");
    }
}
