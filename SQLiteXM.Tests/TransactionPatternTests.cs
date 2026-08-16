using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Comprehensive tests for the 5 key transaction patterns in SQLiteXM:
/// 1. Mixed Operations (Entity DML, LINQ, SQL) in one transaction
/// 2. Fault behavior - exceptions cause rollback even if caught; subsequent operations are skipped
/// 3. Fault recovery via RollbackTransactionAsync()
/// 4. Multiple commits in one transaction block
/// 5. All operations (Entity DML, LINQ, SQL) respect these patterns
/// </summary>
[Collection("Sequential")]
public class TransactionPatternTests : TestBase
{
    #region Pattern 1: Mixed Operations in SxmTransaction

    [Fact]
    public async Task Pattern1_MixedOperations_AllShareSameTransaction()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // 1. Entity DML - Insert via SaveAsync
            var customer = new SimpleEntity { Name = "Pattern1_Customer", Age = 30 };
            await customer.SaveAsync();
            customer.id.Should().BeGreaterThan(0, "entity should have ID after insert");

            // 2. LINQ - Query the entity we just inserted
            var linqResult = ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == customer.id)
                .FirstOrDefault();
            linqResult.Should().NotBeNull("LINQ should see uncommitted entity in same transaction");
            linqResult!.Name.Should().Be("Pattern1_Customer");

            // 3. SQL - Update via embedded SQL
            var updateParams = new Dictionary<string, object?> 
            { 
                ["NewAge"] = 31,
                ["Id"] = customer.id
            };
            await ctx.RunStatementAsync(
                "UPDATE SimpleEntity SET Age = @NewAge WHERE id = @Id", 
                updateParams);

            // 4. LINQ - Verify SQL update was visible
            var updatedEntity = ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == customer.id)
                .Select(e => new { e.Age })
                .First();
            updatedEntity.Age.Should().Be(31, "SQL update should be visible to LINQ");

            // 5. Entity DML - Update via SaveAsync
            // Sync the entity Age with the SQL update to avoid stale data overwrite
            customer.Age = 31;
            customer.Name = "Pattern1_Updated";
            await customer.SaveAsync();

            // 6. LINQ - Verify entity update was visible
            var finalCheck = ctx.GetTable<SimpleEntity>()
                .Where(e => e.id == customer.id)
                .FirstOrDefault();
            finalCheck!.Name.Should().Be("Pattern1_Updated", "entity update should be visible to LINQ");
            finalCheck.Age.Should().Be(31, "age should still be 31");

            // Let auto-commit handle the commit on dispose
        }

        // Assert - All changes were committed together
        var persisted = await VerifyEntityExistsInDbAsync<SimpleEntity>(
            (await SxmSql.RunStatementAsync<SimpleEntity>("SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = 'Pattern1_Updated'", TestDatabaseName))[0].id);
        persisted.Should().NotBeNull("committed entity should exist");
        persisted!.Name.Should().Be("Pattern1_Updated");
        persisted.Age.Should().Be(31);

        // Cleanup
        await persisted.DeleteAsync();
    }

    [Fact]
    public async Task Pattern1_MixedOperations_RollbackAffectsAll()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        long entityId = 0;
        long linqInsertId = 0;

        // Act - Explicitly rollback instead of committing
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Entity DML insert
            var entity = new SimpleEntity { Name = "Pattern1_Rollback", Age = 25 };
            await entity.SaveAsync();
            entityId = entity.id;
            entityId.Should().BeGreaterThan(0);

            // LINQ insert
            var linqEntity = new SimpleEntity { Name = "Pattern1_Linq", Age = 26 };
            await ctx.InsertAsync(linqEntity);
            linqInsertId = linqEntity.id;
            linqInsertId.Should().BeGreaterThan(0);

            // SQL insert
            var sqlParams = new Dictionary<string, object?> 
            { 
                ["Name"] = "Pattern1_SQL",
                ["Age"] = 27,
                ["IsActive"] = true
            };
            await ctx.RunStatementAsync(
                "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)", 
                sqlParams);

            // All three should be visible inside the transaction
            var count = ctx.GetTable<SimpleEntity>()
                .Count(e => e.Name.StartsWith("Pattern1_"));
            count.Should().BeGreaterThanOrEqualTo(3, "all inserts should be visible in transaction");

            // Explicitly rollback instead of committing
            await ctx.RollbackTransactionAsync();
        }

        // Assert - Nothing was committed (explicit rollback)
        await VerifyEntityNotInDbAsync<SimpleEntity>(entityId);
        await VerifyEntityNotInDbAsync<SimpleEntity>(linqInsertId);
    }

    #endregion

    #region Pattern 2: Fault Behavior

    [Fact]
    public async Task Pattern2_FaultBehavior_ExceptionCausesRollback_EvenIfCaught()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        long savedId = 0;

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Insert a valid entity
            var entity = new SimpleEntity { Name = "Pattern2_BeforeError", Age = 40 };
            await entity.SaveAsync();
            savedId = entity.id;

            // Entity should be visible in transaction before error
            ctx.GetTable<SimpleEntity>()
                .Count(e => e.id == savedId)
                .Should().Be(1, "entity should exist before exception");

            try
            {
                // Cause a SQL error with truly invalid SQL
                await ctx.RunStatementAsync("COMPLETELY INVALID SQL SYNTAX");
            }
            catch (Exception)
            {
                // Exception was caught, but transaction should still be faulted
            }

            // Try to insert another entity - should be skipped due to fault state
            var entity2 = new SimpleEntity { Name = "Pattern2_AfterError", Age = 41 };
            var result = await ctx.InsertAsync(entity2);
            result.Should().Be(0, "insert should be skipped when transaction is faulted");
            entity2.id.Should().Be(0, "entity should not get an ID when skipped");
        }
        // Dispose auto-rolls back because transaction is faulted

        // Assert - First entity was not persisted despite being saved before the error
        await VerifyEntityNotInDbAsync<SimpleEntity>(savedId);
    }

    [Fact]
    public async Task Pattern2_FaultBehavior_SubsequentOperationsSkipped()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            try
            {
                // First operation succeeds
                var entity1 = new SimpleEntity { Name = "Pattern2_First", Age = 50 };
                await entity1.SaveAsync();
                entity1.id.Should().BeGreaterThan(0, "first entity should save successfully");

                // Cause an error with invalid SQL
                await ctx.RunStatementAsync("INVALID SQL THAT WILL THROW");
            }
            catch
            {
                // Caught the error
            }

            // Try additional operations - should be silently skipped
            var entity2 = new SimpleEntity { Name = "Pattern2_Second", Age = 51 };
            var result = await ctx.InsertAsync(entity2);
            result.Should().Be(0, "insert after fault should return 0 (skipped)");
            entity2.id.Should().Be(0, "entity should not have ID when operation skipped");

            // Even entity DML operations are skipped
            var entity3 = new SimpleEntity { Name = "Pattern2_Third", Age = 52 };
            await entity3.SaveAsync();
            entity3.id.Should().Be(0, "entity DML should be skipped when transaction is faulted");
        }
    }

    [Fact]
    public async Task Pattern2_FaultBehavior_CannotCommitFaultedTransaction()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        long entityId = 0;

        // Act & Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Insert a valid entity
            var entity = new SimpleEntity { Name = "Pattern2_CommitTest", Age = 45 };
            await entity.SaveAsync();
            entityId = entity.id;
            entityId.Should().BeGreaterThan(0, "entity should be inserted successfully");

            try
            {
                // Cause an error - this faults the transaction
                await ctx.RunStatementAsync("BAD SQL");
            }
            catch
            {
                // Expected - the error faults the transaction internally
            }

            // Try to commit a faulted transaction - should throw InvalidOperationException
            var act = async () => await ctx.CommitTransactionAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Cannot commit*previous operation*failed*");
        }

        // Assert - Nothing was committed because the faulted transaction was rolled back on dispose
        // This verifies the ALL-OR-NOTHING semantics: even the successful insert was rolled back
        await VerifyEntityNotInDbAsync<SimpleEntity>(entityId);
    }

    #endregion

    #region Pattern 3: Fault Recovery via RollbackTransactionAsync

    [Fact]
    public async Task Pattern3_FaultRecovery_RollbackClearsFaultAndAllowsReuse()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // First batch - will fail
            long firstId = 0;
            try
            {
                var entity1 = new SimpleEntity { Name = "Pattern3_First", Age = 60 };
                await entity1.SaveAsync();
                firstId = entity1.id;
                firstId.Should().BeGreaterThan(0);

                // Cause an error
                await ctx.RunStatementAsync("SELECT * FROM NonExistentTable");
            }
            catch
            {
                // Expected error
            }

            // Transaction is now faulted - verify by checking subsequent ops are skipped
            var skippedEntity = new SimpleEntity { Name = "Pattern3_Skipped", Age = 61 };
            var skippedResult = await ctx.InsertAsync(skippedEntity);
            skippedResult.Should().Be(0, "operations should be skipped when faulted");

            // Recover by rolling back
            await ctx.RollbackTransactionAsync();

            // Second batch - should succeed now
            var entity2 = new SimpleEntity { Name = "Pattern3_Second", Age = 62 };
            var insertResult = await ctx.InsertAsync(entity2);
            insertResult.Should().Be(1, "insert should succeed after rollback recovery");
            entity2.id.Should().BeGreaterThan(0);

            // Can also use entity DML after recovery
            var entity3 = new SimpleEntity { Name = "Pattern3_Third", Age = 63 };
            await entity3.SaveAsync();
            entity3.id.Should().BeGreaterThan(0);

            // Let auto-commit handle the successful work
        }

        // Assert - First entity not persisted (rolled back), but second and third were (auto-committed)
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern3_%'", 
            TestDatabaseName);

        all.Should().HaveCount(2, "only recovered entities should exist");
        all.Should().Contain(e => e.Name == "Pattern3_Second");
        all.Should().Contain(e => e.Name == "Pattern3_Third");
        all.Should().NotContain(e => e.Name == "Pattern3_First");

        // Cleanup
        foreach (var entity in all)
        {
            await entity.DeleteAsync();
        }
    }

    [Fact]
    public async Task Pattern3_FaultRecovery_MultipleRecoveryCycles()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Cycle 1: Insert, fault, rollback
            try
            {
                var e1 = new SimpleEntity { Name = "Pattern3_Cycle1", Age = 70 };
                await e1.SaveAsync();
                await ctx.RunStatementAsync("BAD SQL 1");
            }
            catch { }

            await ctx.RollbackTransactionAsync();

            // Cycle 2: Insert, fault, rollback
            try
            {
                var e2 = new SimpleEntity { Name = "Pattern3_Cycle2", Age = 71 };
                await e2.SaveAsync();
                await ctx.RunStatementAsync("BAD SQL 2");
            }
            catch { }

            await ctx.RollbackTransactionAsync();

            // Cycle 3: Insert successfully
            var e3 = new SimpleEntity { Name = "Pattern3_Cycle3", Age = 72 };
            await e3.SaveAsync();
            e3.id.Should().BeGreaterThan(0);

            // Auto-commit on dispose
        }

        // Assert - Only cycle 3 persisted
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern3_Cycle%'", 
            TestDatabaseName);

        all.Should().HaveCount(1);
        all[0].Name.Should().Be("Pattern3_Cycle3");

        // Cleanup
        await all[0].DeleteAsync();
    }

    #endregion

    #region Pattern 4: Multiple Commits

    [Fact]
    public async Task Pattern4_MultipleCommits_EachBatchCommittedSeparately()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Batch 1
            var entity1 = new SimpleEntity { Name = "Pattern4_Batch1", Age = 80 };
            await entity1.SaveAsync();
            await ctx.CommitTransactionAsync();

            // Batch 2
            var entity2 = new SimpleEntity { Name = "Pattern4_Batch2", Age = 81 };
            await entity2.SaveAsync();
            await ctx.CommitTransactionAsync();

            // Batch 3 - no explicit commit, will auto-commit on dispose
            var entity3 = new SimpleEntity { Name = "Pattern4_Batch3", Age = 82 };
            await entity3.SaveAsync();
        }

        // Assert - All three batches persisted
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern4_Batch%'", 
            TestDatabaseName);

        all.Should().HaveCount(3);

        // Cleanup
        foreach (var entity in all)
        {
            await entity.DeleteAsync();
        }
    }

    [Fact]
    public async Task Pattern4_MultipleCommits_IntermediateFailureDoesNotAffectPriorCommits()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // First batch - commit successfully
            var entity1 = new SimpleEntity { Name = "Pattern4_Committed", Age = 90 };
            await entity1.SaveAsync();
            await ctx.CommitTransactionAsync();

            // Verify first batch persisted
            var committed = await VerifyEntityExistsInDbAsync<SimpleEntity>(entity1.id);
            committed.Should().NotBeNull("first committed batch should persist");

            // Second batch - will fail
            try
            {
                var entity2 = new SimpleEntity { Name = "Pattern4_Failed", Age = 91 };
                await entity2.SaveAsync();

                // Cause an error
                await ctx.RunStatementAsync("INVALID SQL STATEMENT");
            }
            catch
            {
                // Expected - SQL error
            }

            // Dispose will rollback the faulted transaction (second batch only)
        }

        // Assert - First commit should still be there
        var check1 = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = 'Pattern4_Committed'", 
            TestDatabaseName);
        check1.Should().HaveCount(1, "committed batch should survive later failure");
        check1[0].Name.Should().Be("Pattern4_Committed");

        // Cleanup
        await check1[0].DeleteAsync();
    }

    [Fact]
    public async Task Pattern4_MultipleCommits_WithMixedOperations()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Batch 1: Entity DML
            var e1 = new SimpleEntity { Name = "Pattern4_Mixed1", Age = 100 };
            await e1.SaveAsync();
            await ctx.CommitTransactionAsync();

            // Batch 2: LINQ
            var e2 = new SimpleEntity { Name = "Pattern4_Mixed2", Age = 101 };
            await ctx.InsertAsync(e2);
            await ctx.CommitTransactionAsync();

            // Batch 3: SQL
            var sqlParams = new Dictionary<string, object?> 
            { 
                ["Name"] = "Pattern4_Mixed3",
                ["Age"] = 102,
                ["IsActive"] = false
            };
            await ctx.RunStatementAsync(
                "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)", 
                sqlParams);
            // Auto-commit on dispose
        }

        // Assert - All three batches persisted
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern4_Mixed%'", 
            TestDatabaseName);

        all.Should().HaveCount(3);

        // Cleanup
        foreach (var entity in all)
        {
            await entity.DeleteAsync();
        }
    }

    #endregion

    #region Pattern 5: All Operations Respect All Patterns

    [Fact]
    public async Task Pattern5_AllOperations_RespectFaultBehavior()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Insert via three different methods
            var entity1 = new SimpleEntity { Name = "Pattern5_Entity", Age = 110 };
            await entity1.SaveAsync();

            var entity2 = new SimpleEntity { Name = "Pattern5_LINQ", Age = 111 };
            await ctx.InsertAsync(entity2);

            // Insert via SQL
            var sqlParams = new Dictionary<string, object?> { ["Name"] = "Pattern5_SQL", ["Age"] = 112, ["IsActive"] = false };
            var sqlResult = await ctx.RunStatementAsync<SimpleEntity>(
                "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive) RETURNING id, Name, Age, IsActive",
                sqlParams);
            var entity3Id = sqlResult[0].id;

            // All should be visible
            ctx.GetTable<SimpleEntity>()
                .Count(e => e.id == entity1.id || e.id == entity2.id || e.id == entity3Id)
                .Should().Be(3, "all three operations should be visible before fault");

            // Cause a fault
            try { await ctx.RunStatementAsync("BAD SQL"); } catch { }

            // Try each operation type after fault - all should be skipped
            var entitySkip = new SimpleEntity { Name = "Skipped", Age = 999 };
            await entitySkip.SaveAsync();
            entitySkip.id.Should().Be(0, "entity DML should be skipped when transaction is faulted");

            var linqSkip = new SimpleEntity { Name = "Skipped", Age = 999 };
            var linqResult = await ctx.InsertAsync(linqSkip);
            linqResult.Should().Be(0, "LINQ insert should be skipped after fault");

            var sqlSkipParams = new Dictionary<string, object?> { ["Name"] = "Skipped", ["Age"] = 999 };
            var sqlSkipResult = await ctx.RunStatementAsync(
                "INSERT INTO SimpleEntity (Name, Age) VALUES (@Name, @Age)", 
                sqlSkipParams);
            sqlSkipResult.Should().BeEmpty("SQL insert should return empty list when skipped");
        }
        // Auto-rollback because faulted

        // Nothing should have persisted
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern5_%'", 
            TestDatabaseName);
        all.Should().BeEmpty("all operations should be rolled back");
    }

    [Fact]
    public async Task Pattern5_AllOperations_RespectRecovery()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Fault, rollback, then insert via all three methods
            try
            {
                var temp = new SimpleEntity { Name = "Temp", Age = 1 };
                await temp.SaveAsync();
                await ctx.RunStatementAsync("CAUSE ERROR");
            }
            catch { }

            await ctx.RollbackTransactionAsync();

            // Now insert via all three methods
            var e1 = new SimpleEntity { Name = "Pattern5_Recovered_Entity", Age = 120 };
            await e1.SaveAsync();

            var e2 = new SimpleEntity { Name = "Pattern5_Recovered_LINQ", Age = 121 };
            await ctx.InsertAsync(e2);

            var sqlParams = new Dictionary<string, object?> 
            { 
                ["Name"] = "Pattern5_Recovered_SQL",
                ["Age"] = 122,
                ["IsActive"] = true
            };
            await ctx.RunStatementAsync(
                "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)", 
                sqlParams);

            // Auto-commit on dispose
        }

        // Assert - All three methods worked after recovery
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern5_Recovered%'", 
            TestDatabaseName);

        all.Should().HaveCount(3);

        // Cleanup
        foreach (var entity in all)
        {
            await entity.DeleteAsync();
        }
    }

    [Fact]
    public async Task Pattern5_AllOperations_RespectMultipleCommits()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Commit 1: Entity DML
            var e1 = new SimpleEntity { Name = "Pattern5_CommitTest1", Age = 130 };
            await e1.SaveAsync();
            await ctx.CommitTransactionAsync();

            // Commit 2: LINQ
            var e2 = new SimpleEntity { Name = "Pattern5_CommitTest2", Age = 131 };
            await ctx.InsertAsync(e2);
            await ctx.CommitTransactionAsync();

            // Commit 3: SQL (auto-commit on dispose)
            var sqlParams = new Dictionary<string, object?> 
            { 
                ["Name"] = "Pattern5_CommitTest3",
                ["Age"] = 132,
                ["IsActive"] = false
            };
            await ctx.RunStatementAsync(
                "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)", 
                sqlParams);
        }

        // Assert
        var all = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern5_CommitTest%'", 
            TestDatabaseName);

        all.Should().HaveCount(3);

        // Cleanup
        foreach (var entity in all)
        {
            await entity.DeleteAsync();
        }
    }

    [Fact]
    public async Task Pattern5_ComplexScenario_AllPatternsIntegrated()
    {
        // This test combines all 5 patterns in one complex scenario
        await InitializeSqliteXMAsync();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            // Phase 1: Mixed operations (Pattern 1) + commit (Pattern 4)
            var customer1 = new SimpleEntity { Name = "Pattern5_Customer1", Age = 140 };
            await customer1.SaveAsync();

            var customer2 = new SimpleEntity { Name = "Pattern5_Customer2", Age = 141 };
            await ctx.InsertAsync(customer2);

            var sqlParams1 = new Dictionary<string, object?> { ["Name"] = "Pattern5_Customer3", ["Age"] = 142, ["IsActive"] = true };
            await ctx.RunStatementAsync("INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)", sqlParams1);

            await ctx.CommitTransactionAsync();

            // Phase 2: Fault (Pattern 2)
            var preErrorEntity = new SimpleEntity { Name = "Pattern5_PreError", Age = 143 };
            await preErrorEntity.SaveAsync();

            try
            {
                await ctx.RunStatementAsync("INVALID SQL TO CAUSE FAULT");
            }
            catch { }

            // Operations after fault are skipped (Pattern 2)
            var postErrorEntity = new SimpleEntity { Name = "Pattern5_PostError", Age = 144 };
            var skipped = await ctx.InsertAsync(postErrorEntity);
            skipped.Should().Be(0, "operation after fault should be skipped");

            // Phase 3: Recovery (Pattern 3)
            await ctx.RollbackTransactionAsync();

            // Phase 4: Continue with new operations after recovery (Pattern 3 + 5)
            var recovered1 = new SimpleEntity { Name = "Pattern5_Recovered1", Age = 145 };
            await recovered1.SaveAsync();

            var recovered2 = new SimpleEntity { Name = "Pattern5_Recovered2", Age = 146 };
            await ctx.InsertAsync(recovered2);

            // Don't commit - let auto-commit handle it
        }

        // Assert Phase 1 committed (survives fault)
        var phase1 = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern5_Customer%'", 
            TestDatabaseName);
        phase1.Should().HaveCount(3, "phase 1 commits should persist through later fault");

        // Assert Phase 2 rolled back
        var phase2 = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = 'Pattern5_PreError'", 
            TestDatabaseName);
        phase2.Should().BeEmpty("phase 2 should be rolled back");

        // Assert Phase 4 committed
        var phase4 = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE 'Pattern5_Recovered%'", 
            TestDatabaseName);
        phase4.Should().HaveCount(2, "recovered operations should commit");

        // Cleanup
        foreach (var entity in phase1.Concat(phase4))
        {
            await entity.DeleteAsync();
        }
    }

    #endregion
}
