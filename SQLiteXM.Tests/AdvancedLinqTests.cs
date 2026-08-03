using FluentAssertions;
using SQLiteXM;
using System.Diagnostics;

namespace SQLiteXM.Tests;

/// <summary>
/// Advanced LINQ tests demonstrating complex query patterns, deferred execution,
/// aggregates, grouping, joins, set operations, and bulk operations.
/// </summary>
[Collection("Sequential")]
public class AdvancedLinqTests : TestBase
{
    [Fact]
    public async Task AdvancedLinq_InsertAsync_ShouldExecuteImmediately()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        // Act - Insert executes immediately within the context transaction
        var parent1 = new ParentEntity { ParentName = "ImmediateParent1" };
        var parent2 = new ParentEntity { ParentName = "ImmediateParent2" };

        await ctx.InsertAsync(parent1);
        await ctx.InsertAsync(parent2);

        // Assert - IDs are assigned immediately after InsertAsync
        parent1.id.Should().BeGreaterThan(0);
        parent2.id.Should().BeGreaterThan(0);

        // Cleanup
        await ctx.DeleteAsync(parent1);
        await ctx.DeleteAsync(parent2);
    }

    [Fact]
    public async Task AdvancedLinq_GroupByWithAggregates_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        // Create parent entities with unique names
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var parent1 = new ParentEntity { ParentName = $"GroupP1_{uniquePrefix}" };
        var parent2 = new ParentEntity { ParentName = $"GroupP2_{uniquePrefix}" };
        await ctx.InsertAsync(parent1);
        await ctx.InsertAsync(parent2);

        // Create child entities with different counts per parent
        var children = new[]
        {
            new ChildEntity { ChildName = $"Child1_{uniquePrefix}", ParentId = parent1.id },
            new ChildEntity { ChildName = $"Child2_{uniquePrefix}", ParentId = parent1.id },
            new ChildEntity { ChildName = $"Child3_{uniquePrefix}", ParentId = parent1.id },
            new ChildEntity { ChildName = $"Child4_{uniquePrefix}", ParentId = parent2.id },
            new ChildEntity { ChildName = $"Child5_{uniquePrefix}", ParentId = parent2.id }
        };

        foreach (var child in children)
        {
            await ctx.InsertAsync(child);
        }

        // Act - GroupBy with Count aggregate for our specific test data only
        var grouped = ctx.GetTable<ChildEntity>()
            .Where(c => c.ParentId == parent1.id || c.ParentId == parent2.id)
            .GroupBy(c => c.ParentId)
            .Select(g => new 
            { 
                ParentId = g.Key, 
                Count = g.Count()
            })
            .ToList();

        // Assert
        grouped.Should().HaveCount(2);
        grouped.Should().Contain(g => g.ParentId == parent1.id && g.Count == 3);
        grouped.Should().Contain(g => g.ParentId == parent2.id && g.Count == 2);

        // Cleanup
        foreach (var child in children) { await ctx.DeleteAsync(child); }
        await ctx.DeleteAsync(parent1);
        await ctx.DeleteAsync(parent2);
    }

    [Fact]
    public async Task AdvancedLinq_OrderBySkipTake_ShouldPageResults()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        // Create test data
        var entities = new[]
        {
            new SimpleEntity { Name = "Alpha", Age = 10 },
            new SimpleEntity { Name = "Beta", Age = 20 },
            new SimpleEntity { Name = "Gamma", Age = 30 },
            new SimpleEntity { Name = "Delta", Age = 40 },
            new SimpleEntity { Name = "Epsilon", Age = 50 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Get page 2 (skip 2, take 2)
        var page2 = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == "Alpha" || e.Name == "Beta" || e.Name == "Gamma" || 
                        e.Name == "Delta" || e.Name == "Epsilon")
            .OrderBy(e => e.Name)
            .Skip(2)
            .Take(2)
            .Select(e => new { e.id, e.Name })
            .ToList();

        // Assert
        page2.Should().HaveCount(2);
        page2[0].Name.Should().Be("Delta");
        page2[1].Name.Should().Be("Epsilon");

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_DistinctAndContains_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        // Create entities with duplicate ages
        var entities = new[]
        {
            new SimpleEntity { Name = "Distinct1", Age = 10 },
            new SimpleEntity { Name = "Distinct2", Age = 20 },
            new SimpleEntity { Name = "Distinct3", Age = 10 },
            new SimpleEntity { Name = "Distinct4", Age = 30 },
            new SimpleEntity { Name = "Distinct5", Age = 20 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act
        var distinctAges = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith("Distinct"))
            .Select(e => e.Age)
            .Distinct()
            .ToList();

        bool contains10 = distinctAges.Contains(10);
        bool contains99 = distinctAges.Contains(99);

        // Assert
        distinctAges.Should().HaveCount(3); // 10, 20, 30
        contains10.Should().BeTrue();
        contains99.Should().BeFalse();

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_AnyAllCount_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new[]
        {
            new SimpleEntity { Name = $"Test{uniquePrefix}_1", Age = 10 },
            new SimpleEntity { Name = $"Test{uniquePrefix}_2", Age = 50 },
            new SimpleEntity { Name = $"Test{uniquePrefix}_3", Age = 100 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act
        bool anyHigh = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Test{uniquePrefix}"))
            .Any(e => e.Age > 75);

        bool allPositive = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Test{uniquePrefix}"))
            .All(e => e.Age > 0);

        int count = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Test{uniquePrefix}"))
            .Count();

        // Assert
        anyHigh.Should().BeTrue("at least one entity has Age > 75");
        allPositive.Should().BeTrue("all entities have Age > 0");
        count.Should().Be(3);

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_LeftJoinWithDefaultIfEmpty_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Create two parents
        var parent1 = new ParentEntity { ParentName = $"JoinParent1_{uniquePrefix}" };
        var parent2 = new ParentEntity { ParentName = $"JoinParent2_{uniquePrefix}" };
        await ctx.InsertAsync(parent1);
        await ctx.InsertAsync(parent2);

        // Create children - both with valid parents
        var child1 = new ChildEntity { ChildName = $"WithParent_{uniquePrefix}", ParentId = parent1.id };
        var child2 = new ChildEntity { ChildName = $"OtherParent_{uniquePrefix}", ParentId = parent2.id };
        await ctx.InsertAsync(child1);
        await ctx.InsertAsync(child2);

        // Act - Left join where we filter to only show children from parent1
        // This simulates a left join where some parents might not have children
        var leftJoin = (from c in ctx.GetTable<ChildEntity>()
                        where c.ChildName == $"WithParent_{uniquePrefix}" || c.ChildName == $"OtherParent_{uniquePrefix}"
                        join p in ctx.GetTable<ParentEntity>() 
                            on c.ParentId equals p.id into gj
                        from maybeParent in gj.DefaultIfEmpty()
                        select new 
                        { 
                            ChildName = c.ChildName, 
                            ParentName = maybeParent != null ? maybeParent.ParentName : "(null)" 
                        })
                       .ToList();

        // Assert
        leftJoin.Should().HaveCount(2);
        leftJoin.Should().Contain(x => x.ChildName == $"WithParent_{uniquePrefix}" && x.ParentName == $"JoinParent1_{uniquePrefix}");
        leftJoin.Should().Contain(x => x.ChildName == $"OtherParent_{uniquePrefix}" && x.ParentName == $"JoinParent2_{uniquePrefix}");

        // Cleanup
        await ctx.DeleteAsync(child1);
        await ctx.DeleteAsync(child2);
        await ctx.DeleteAsync(parent1);
        await ctx.DeleteAsync(parent2);
    }

    [Fact]
    public async Task AdvancedLinq_UnionOperation_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        var entities = new[]
        {
            new SimpleEntity { Name = "SetA1", Age = 10 },
            new SimpleEntity { Name = "SetA2", Age = 20 },
            new SimpleEntity { Name = "SetB1", Age = 20 },
            new SimpleEntity { Name = "SetB2", Age = 30 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act
        var setA = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Age <= 20)
            .Select(e => e.Age);

        var setB = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Age >= 20)
            .Select(e => e.Age);

        var union = setA.Union(setB).ToList();

        // Assert
        union.Should().Contain(10);
        union.Should().Contain(20);
        union.Should().Contain(30);
        union.Distinct().Should().HaveCount(union.Count, "Union should remove duplicates");

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_AverageWithNullable_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        var entities = new[]
        {
            new SimpleEntity { Name = "Avg1", Age = 10 },
            new SimpleEntity { Name = "Avg2", Age = 20 },
            new SimpleEntity { Name = "Avg3", Age = 30 }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act
        var avgDouble = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith("Avg"))
            .Average(e => (double?)e.Age);

        // Assert
        avgDouble.Should().Be(20.0);

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_SelectMany_ShouldFlattenResults()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Create parents
        var parent1 = new ParentEntity { ParentName = $"SelectMany1_{uniquePrefix}" };
        var parent2 = new ParentEntity { ParentName = $"SelectMany2_{uniquePrefix}" };
        await ctx.InsertAsync(parent1);
        await ctx.InsertAsync(parent2);

        // Create children
        var child1 = new ChildEntity { ChildName = $"Child1_{uniquePrefix}", ParentId = parent1.id };
        var child2 = new ChildEntity { ChildName = $"Child2_{uniquePrefix}", ParentId = parent2.id };
        await ctx.InsertAsync(child1);
        await ctx.InsertAsync(child2);

        // Act - SelectMany (cartesian-style projection)
        var flattened = ctx.GetTable<ChildEntity>()
            .Where(c => c.ChildName == $"Child1_{uniquePrefix}" || c.ChildName == $"Child2_{uniquePrefix}")
            .SelectMany(c => ctx.GetTable<ParentEntity>().Where(p => p.id == c.ParentId),
                        (c, p) => new { c.ChildName, ParentName = p.ParentName })
            .ToList();

        // Assert
        flattened.Should().HaveCount(2);
        flattened.Should().Contain(x => x.ChildName == $"Child1_{uniquePrefix}" && x.ParentName == $"SelectMany1_{uniquePrefix}");
        flattened.Should().Contain(x => x.ChildName == $"Child2_{uniquePrefix}" && x.ParentName == $"SelectMany2_{uniquePrefix}");

        // Cleanup
        await ctx.DeleteAsync(child1);
        await ctx.DeleteAsync(child2);
        await ctx.DeleteAsync(parent1);
        await ctx.DeleteAsync(parent2);
    }

    [Fact]
    public async Task AdvancedLinq_ComplexPredicateWithStringFunctions_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        var entities = new[]
        {
            new SimpleEntity { Name = "ComplexTest1", Age = 10, IsActive = true },
            new SimpleEntity { Name = "ComplexTest2", Age = 20, IsActive = false },
            new SimpleEntity { Name = "OtherName", Age = 30, IsActive = true }
        };

        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // Act - Complex predicate with string functions
        var results = ctx.GetTable<SimpleEntity>()
            .Where(e => (e.Name != null && e.Name.Contains("Complex")) || e.Age > 25)
            .ToList();

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(2, "should match ComplexTest1, ComplexTest2, and OtherName");
        results.Should().Contain(e => e.Name == "ComplexTest1");
        results.Should().Contain(e => e.Name == "ComplexTest2");

        // Cleanup
        foreach (var entity in entities) { await ctx.DeleteAsync(entity); }
    }

    [Fact]
    public async Task AdvancedLinq_DeferredExecution_ShouldRerunQuery()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        var entity1 = new SimpleEntity { Name = "Deferred1", Age = 10 };
        await ctx.InsertAsync(entity1);

        // Create deferred query
        var query = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == "Deferred1");

        // First materialization
        var firstList = query.ToList();
        firstList.Should().HaveCount(1);

        // Add another entity
        var entity2 = new SimpleEntity { Name = "Deferred1", Age = 20 };
        await ctx.InsertAsync(entity2);

        // Act - Second materialization should rerun the query
        var secondList = query.ToList();

        // Assert
        secondList.Should().HaveCount(2, "deferred execution should rerun query and see new data");

        // Cleanup
        await ctx.DeleteAsync(entity1);
        await ctx.DeleteAsync(entity2);
    }

    [Fact]
    public async Task AdvancedLinq_BulkInsertUpdateDelete_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        using var ctx = new SxmLinqDbContext(TestDatabaseName);

        // Act - Bulk operations
        var entities = new[]
        {
            new SimpleEntity { Name = "Bulk1", Age = 100 },
            new SimpleEntity { Name = "Bulk2", Age = 200 },
            new SimpleEntity { Name = "Bulk3", Age = 300 }
        };

        // Insert via InsertAsync (immediate execution)
        foreach (var entity in entities)
        {
            await ctx.InsertAsync(entity);
        }

        // All should have IDs now
        entities.Should().OnlyContain(e => e.id > 0);

        // Update
        foreach (var entity in entities)
        {
            entity.Age += 10;
            await ctx.UpdateAsync(entity);
        }

        // Verify updates
        var retrieved = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == "Bulk1" || e.Name == "Bulk2" || e.Name == "Bulk3")
            .ToList();

        retrieved.Should().Contain(e => e.Name == "Bulk1" && e.Age == 110);
        retrieved.Should().Contain(e => e.Name == "Bulk2" && e.Age == 210);
        retrieved.Should().Contain(e => e.Name == "Bulk3" && e.Age == 310);

        // Delete via DeleteAsync (immediate execution)
        foreach (var entity in entities)
        {
            await ctx.DeleteAsync(entity);
        }

        // Assert - should be deleted
        var afterDelete = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == "Bulk1" || e.Name == "Bulk2" || e.Name == "Bulk3")
            .ToList();

        afterDelete.Should().BeEmpty();
    }
}
