using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests proving that entity writes (SxmEntity.SaveAsync/DeleteAsync), embedded SQL and LINQ
/// operations all participate in the same unit of work when an SxmTransaction is active.
/// The context either creates an ambient SxmSqlTransaction (owning) or joins an existing one.
/// </summary>
[Collection("Sequential")]
public class MixedUnitOfWorkTests : TestBase
{
    [Fact]
    public async Task EntitySave_And_LinqInsert_ShouldCommitTogether_OnDispose()
    {
        await InitializeSqliteXMAsync();
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            // Entity write via ambient transaction created by the context
            var person = new SimpleEntity { Name = $"Mixed_{uniquePrefix}_Save", Age = 40, IsActive = true };
            await person.SaveAsync();
            person.id.Should().BeGreaterThan(0);

            // LINQ insert via the context
            var linqEntity = new SimpleEntity { Name = $"Mixed_{uniquePrefix}_Linq", Age = 41, IsActive = true };
            await ctx.InsertAsync(linqEntity);
            linqEntity.id.Should().BeGreaterThan(0);

            // Both rows are visible inside the shared transaction
            var visible = ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith($"Mixed_{uniquePrefix}"))
                .ToList();
            visible.Should().HaveCount(2);
        } // auto-commit

        // Verify persisted after commit
        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        var results = verifyCtx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Mixed_{uniquePrefix}"))
            .ToList();
        results.Should().HaveCount(2);

        // Cleanup
        foreach (var e in results)
            await verifyCtx.DeleteAsync(e);
    }

    [Fact]
    public async Task EntitySave_LinqInsert_And_RawSql_ShouldRollbackTogether()
    {
        await InitializeSqliteXMAsync();
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var person = new SimpleEntity { Name = $"MixedRb_{uniquePrefix}_Save", Age = 50, IsActive = true };
            await person.SaveAsync();

            var linqEntity = new SimpleEntity { Name = $"MixedRb_{uniquePrefix}_Linq", Age = 51, IsActive = true };
            await ctx.InsertAsync(linqEntity);

            // Raw SQL participates in the same shared transaction
            var rows = await ctx.QueryAsync(
                "SELECT COUNT(*) AS cnt FROM SimpleEntity WHERE Name LIKE @p0",
                $"MixedRb_{uniquePrefix}%");
            Convert.ToInt64(rows[0]["cnt"]).Should().Be(2);

            // Discard everything
            await ctx.RollbackTransactionAsync();
        }

        // Nothing persisted
        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        var results = verifyCtx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"MixedRb_{uniquePrefix}"))
            .ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Context_ShouldJoin_ExistingAmbientTransaction_AndRollbackTogether()
    {
        await InitializeSqliteXMAsync();
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var tx = new SxmTransaction(TestDatabaseName))
        {
            // Entity write via the outer ambient transaction
            var person = new SimpleEntity { Name = $"Join_{uniquePrefix}_Save", Age = 60, IsActive = true };
            await person.SaveAsync();

            // LINQ context joins the outer ambient transaction (same connection/transaction)
            await using (var ctx = new SxmTransaction())
            {
                var linqEntity = new SimpleEntity { Name = $"Join_{uniquePrefix}_Linq", Age = 61, IsActive = true };
                await ctx.InsertAsync(linqEntity);

                // Both writes are visible inside the shared transaction
                var visible = ctx.GetTable<SimpleEntity>()
                    .Where(e => e.Name!.StartsWith($"Join_{uniquePrefix}"))
                    .ToList();
                visible.Should().HaveCount(2);
            } // joined context does not commit; outer transaction decides

            // Roll back the outer transaction - discards the LINQ insert too
            await tx.RollbackTransactionAsync();
        }

        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        var results = verifyCtx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Join_{uniquePrefix}"))
            .ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Context_ShouldJoin_ExistingAmbientTransaction_AndCommitTogether()
    {
        await InitializeSqliteXMAsync();
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var tx = new SxmTransaction(TestDatabaseName))
        {
            var person = new SimpleEntity { Name = $"JoinC_{uniquePrefix}_Save", Age = 62, IsActive = true };
            await person.SaveAsync();

            await using (var ctx = new SxmTransaction())
            {
                var linqEntity = new SimpleEntity { Name = $"JoinC_{uniquePrefix}_Linq", Age = 63, IsActive = true };
                await ctx.InsertAsync(linqEntity);
            }
        } // outer ambient transaction auto-commits on dispose

        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        var results = verifyCtx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"JoinC_{uniquePrefix}"))
            .ToList();
        results.Should().HaveCount(2);

        // Cleanup
        foreach (var e in results)
            await verifyCtx.DeleteAsync(e);
    }

    [Fact]
    public async Task FaultedContext_ShouldSkipSubsequentWrites_AndRollbackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string uniquePrefix = Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var person = new SimpleEntity { Name = $"Fault_{uniquePrefix}_Save", Age = 70, IsActive = true };
            await person.SaveAsync();

            var linqEntity = new SimpleEntity { Name = $"Fault_{uniquePrefix}_Linq", Age = 71, IsActive = true };
            await ctx.InsertAsync(linqEntity);
            linqEntity.id.Should().BeGreaterThan(0);

            // Force a write failure through the context: FK violation (parent does not exist)
            var orphan = new ChildEntity { ChildName = $"Fault_{uniquePrefix}_Orphan", ParentId = long.MaxValue };
            Func<Task> act = async () => await ctx.InsertAsync(orphan);
            await act.Should().ThrowAsync<Exception>();

            ctx.Faulted.Should().BeTrue();

            // Subsequent writes are skipped (returning 0), consistent with SxmSqlTransaction
            int skipped = await ctx.InsertAsync(new SimpleEntity { Name = $"Fault_{uniquePrefix}_Skipped", Age = 72 });
            skipped.Should().Be(0, "writes after a fault are skipped");
        }

        // Nothing persisted because the faulted context rolled back on dispose
        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        var results = verifyCtx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith($"Fault_{uniquePrefix}"))
            .ToList();
        results.Should().BeEmpty();
    }
}
