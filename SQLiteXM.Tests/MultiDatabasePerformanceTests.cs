using SQLiteXM;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Performance and scale validation tests for multi-database support.
/// These tests validate that the multi-database architecture performs well
/// with larger datasets and under concurrent load.
/// </summary>
/// <remarks>
/// These tests take approximately 9-10 minutes to execute.
/// To exclude from quick test runs: dotnet test --filter "Category!=Performance"
/// To run only performance tests: dotnet test --filter "Category=Performance"
/// </remarks>
[Collection("Performance")]
[Trait("Category", "Performance")]
public class MultiDatabasePerformanceTests : IDisposable
{
    private static readonly string TestFolder;
    private readonly string _testStatementsPath;
    private readonly string _testId;
    private readonly string _testDbFolder;
    private bool _disposed = false;

    static MultiDatabasePerformanceTests()
    {
        TestFolder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "MultiDatabasePerformance");
        Directory.CreateDirectory(TestFolder);
    }

    public MultiDatabasePerformanceTests()
    {
        _testId = Guid.NewGuid().ToString("N");
        _testStatementsPath = Path.Combine(TestFolder, $"statements_{_testId}.json");
        _testDbFolder = Path.Combine(TestFolder, _testId);
        Directory.CreateDirectory(_testDbFolder);
    }

    public void Dispose()
    {
        if (_disposed) return;

#if !KEEP_PERF_TEST_FILES
        try
        {
            if (File.Exists(_testStatementsPath))
                File.Delete(_testStatementsPath);

            if (Directory.Exists(_testDbFolder))
            {
                Directory.Delete(_testDbFolder, true);
            }
        }
        catch { }
#else
        Console.WriteLine($"Performance test files preserved at: {_testDbFolder}");
        Console.WriteLine($"Test ID: {_testId}");
#endif

        // CRITICAL: Reset SQLiteXM state so subsequent tests work correctly
        try
        {
#if DEBUG
            SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();
#endif
            // Re-initialize with the standard test configuration from TestBase
            var initOptions = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "test_database")
            };
            var testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
            using var stream = File.OpenRead(testStatementsPath);
            SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();

            // Re-register standard test entities
            SxmDatabase.RegisterEntitiesAsync(
                typeof(SimpleEntity),
                typeof(AllTypesEntity),
                typeof(TimeTypeTextEntity),
                typeof(ExplicitColumnEntity),
                typeof(IndexedEntity),
                typeof(ParentEntity),
                typeof(ChildEntity),
                typeof(TriggerEntity),
                typeof(RequiredFieldEntity)
            ).GetAwaiter().GetResult();
        }
        catch { }

        _disposed = true;
    }

    private void CreateMultiDatabaseSqlStatements()
    {
        var json = @"{
  ""version"": 1,
  ""databases"": [
    {
      ""database"": ""products"",
      ""isDefault"": true
    },
    {
      ""database"": ""orders"",
      ""isDefault"": false
    },
    {
      ""database"": ""audit"",
      ""isDefault"": false
    }
  ]
}";
        File.WriteAllText(_testStatementsPath, json);
    }

    #region Bulk Insert Performance Tests

    [Fact]
    public async Task BulkInsert_10000Products_CompletesInReasonableTime()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream1 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream1, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var stopwatch = Stopwatch.StartNew();

        // Act - Insert 10,000 products using a transaction for efficiency
        var connection = new SxmConnection("products", shared: false);
        await using (var transaction = await SxmDbContext.CreateAsync(connection))
        {
            for (int i = 0; i < 10_000; i++)
            {
                await new Product
                {
                    Name = $"Product {i}",
                    Price = i * 1.5m,
                    InStock = i % 2 == 0
                }.SaveAsync();
            }
            await transaction.CommitTransactionAsync();
        }

        stopwatch.Stop();

        // Assert - Should complete in under 60 seconds
        Console.WriteLine($"10,000 inserts completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 60,
            $"10K inserts took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <60s)");

        // Verify all entities were created
        await using (var context = new SxmDbContext("products"))
        {
            var count = context.GetTable<Product>().Count();
            Assert.Equal(10_000, count);
        }
    }

    [Fact]
    public async Task BulkInsert_AcrossMultipleDatabases_PerformsWell()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream2 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream2, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order), typeof(AuditLog));

        var stopwatch = Stopwatch.StartNew();

        // Act - Insert 5,000 entities into each of 3 databases (15,000 total) using sequential transactions
        // Products database
        var productConnection = new SxmConnection("products", shared: false);
        await using (var productTransaction = await SxmDbContext.CreateAsync(productConnection))
        {
            for (int i = 0; i < 5_000; i++)
            {
                await new Product { Name = $"Product {i}", Price = i, InStock = true }.SaveAsync();
            }
            await productTransaction.CommitTransactionAsync();
        }

        // Orders database
        var orderConnection = new SxmConnection("orders", shared: false);
        await using (var orderTransaction = await SxmDbContext.CreateAsync(orderConnection))
        {
            for (int i = 0; i < 5_000; i++)
            {
                await new Order { CustomerName = $"Customer {i}", Total = i * 10, IsPaid = true }.SaveAsync();
            }
            await orderTransaction.CommitTransactionAsync();
        }

        // Audit database
        var auditConnection = new SxmConnection("audit", shared: false);
        await using (var auditTransaction = await SxmDbContext.CreateAsync(auditConnection))
        {
            for (int i = 0; i < 5_000; i++)
            {
                await new AuditLog { Action = $"Action {i}", Timestamp = DateTime.Now }.SaveAsync();
            }
            await auditTransaction.CommitTransactionAsync();
        }

        stopwatch.Stop();

        // Assert - Should complete in under 90 seconds (3 databases)
        Console.WriteLine($"15,000 inserts across 3 databases completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 90,
            $"15K inserts across 3 DBs took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <90s)");

        // Verify counts
        using (var productContext = new SxmDbContext("products"))
        {
            Assert.Equal(5_000, productContext.GetTable<Product>().Count());
        }
        using (var orderContext = new SxmDbContext("orders"))
        {
            Assert.Equal(5_000, orderContext.GetTable<Order>().Count());
        }
        using (var auditContext = new SxmDbContext("audit"))
        {
            Assert.Equal(5_000, auditContext.GetTable<AuditLog>().Count());
        }
    }

    #endregion

    #region Query Performance Tests

    [Fact]
    public async Task Query_LargeDataset_50KRecords_PerformsEfficiently()
    {
        // Arrange - Create 50K products
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream3 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream3, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        Console.WriteLine("Creating 50,000 test products...");
        var insertStopwatch = Stopwatch.StartNew();

        var connection = new SxmConnection("products", shared: false);
        await using (var transaction = await SxmDbContext.CreateAsync(connection))
        {
            for (int i = 0; i < 50_000; i++)
            {
                await new Product
                {
                    Name = $"Product {i}",
                    Price = i % 1000,
                    InStock = i % 3 == 0
                }.SaveAsync();
            }
            await transaction.CommitTransactionAsync();
        }

        insertStopwatch.Stop();
        Console.WriteLine($"Test data created in {insertStopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Act - Query with filtering and ordering
        var queryStopwatch = Stopwatch.StartNew();

        await using (var context = new SxmDbContext("products"))
        {
            var results = context.GetTable<Product>()
                .Where(p => p.Price > 500 && p.InStock)
                .OrderBy(p => p.Price)
                .Take(100)
                .ToList();

            queryStopwatch.Stop();

            // Assert
            Assert.True(results.Count > 0);
            Assert.True(results.Count <= 100);
            Console.WriteLine($"Query of 50K records completed in {queryStopwatch.Elapsed.TotalMilliseconds:F0}ms");
            Assert.True(queryStopwatch.Elapsed.TotalSeconds < 5,
                $"Query took {queryStopwatch.Elapsed.TotalSeconds:F2}s (expected <5s)");
        }
    }

    [Fact]
    public async Task Query_ComplexLinq_LargeDataset_PerformsWell()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream4 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream4, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        // Create 20K products with varied data
        for (int i = 0; i < 20_000; i++)
        {
            await new Product
            {
                Name = $"Product {i % 100}",  // Repeated names to test grouping
                Price = (i % 500) * 2.5m,
                InStock = i % 5 != 0
            }.SaveAsync();
        }

        // Act - Complex query with multiple operations
        var stopwatch = Stopwatch.StartNew();

        await using (var context = new SxmDbContext("products"))
        {
            var results = context.GetTable<Product>()
                .Where(p => p.InStock && p.Price >= 100 && p.Price <= 800)
                .OrderByDescending(p => p.Price)
                .ThenBy(p => p.Name)
                .Skip(50)
                .Take(100)
                .ToList();

            stopwatch.Stop();

            // Assert
            Assert.True(results.Count > 0);
            Assert.True(results.Count <= 100);
            Console.WriteLine($"Complex query on 20K records completed in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
            Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
                $"Complex query took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <5s)");

            // Verify ordering
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.True(results[i].Price >= results[i + 1].Price ||
                           (results[i].Price == results[i + 1].Price &&
                            string.Compare(results[i].Name, results[i + 1].Name) <= 0));
            }
        }
    }

    [Fact]
    public async Task Aggregates_LargeDataset_PerformEfficiently()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream5 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream5, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Order));

        // Create 30K orders
        for (int i = 0; i < 30_000; i++)
        {
            await new Order
            {
                CustomerName = $"Customer {i % 1000}",
                Total = i * 5.0m,
                IsPaid = i % 4 != 0
            }.SaveAsync();
        }

        // Act - Run multiple aggregates
        var stopwatch = Stopwatch.StartNew();

        await using (var context = new SxmDbContext("orders"))
        {
            var count = context.GetTable<Order>().Count(o => o.IsPaid);
            var sum = context.GetTable<Order>().Where(o => o.IsPaid).Sum(o => o.Total);
            var average = context.GetTable<Order>().Where(o => o.IsPaid).Average(o => o.Total);
            var max = context.GetTable<Order>().Max(o => o.Total);
            var min = context.GetTable<Order>().Min(o => o.Total);

            stopwatch.Stop();

            // Assert
            Assert.True(count > 0);
            Assert.True(sum > 0);
            Assert.True(average > 0);
            Assert.True(max > min);
            Console.WriteLine($"5 aggregate operations on 30K records completed in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
            Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
                $"Aggregates took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <5s)");
        }
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentWrites_MultipleDatabases_100Operations_NoDeadlocks()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream6 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream6, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order));

        // Act - Simulate 100 concurrent operations across both databases (50 each)
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await new Product
                {
                    Name = $"Product {index}",
                    Price = index * 10,
                    InStock = true
                }.SaveAsync();
            }));
            tasks.Add(Task.Run(async () =>
            {
                await new Order
                {
                    CustomerName = $"Customer {index}",
                    Total = index * 100,
                    IsPaid = true
                }.SaveAsync();
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - No exceptions, completes in reasonable time
        Console.WriteLine($"100 concurrent writes completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 30,
            $"100 concurrent writes took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <30s)");

        // Verify all entities were created
        using (var productContext = new SxmDbContext("products"))
        {
            var productCount = productContext.GetTable<Product>().Count();
            Assert.Equal(50, productCount);
        }
        using (var orderContext = new SxmDbContext("orders"))
        {
            var orderCount = orderContext.GetTable<Order>().Count();
            Assert.Equal(50, orderCount);
        }
    }

    [Fact]
    public async Task HighConcurrency_200SimultaneousOperations_HandlesGracefully()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream7 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream7, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order), typeof(AuditLog));

        // Act - Simulate 200 concurrent operations across 3 databases
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 200).Select(async i =>
        {
            // Each task does a write to one of the three databases
            if (i % 3 == 0)
            {
                await new Product { Name = $"P{i}", Price = i, InStock = true }.SaveAsync();
            }
            else if (i % 3 == 1)
            {
                await new Order { CustomerName = $"C{i}", Total = i * 5, IsPaid = true }.SaveAsync();
            }
            else
            {
                await new AuditLog { Action = $"Action{i}", Timestamp = DateTime.Now }.SaveAsync();
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Console.WriteLine($"200 concurrent operations across 3 databases completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 45,
            $"200 operations took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <45s)");

        // Verify correct distribution
        using (var productContext = new SxmDbContext("products"))
        {
            var productCount = productContext.GetTable<Product>().Count();
            Assert.InRange(productCount, 60, 70); // ~67 expected (200/3)
        }
    }

    [Fact]
    public async Task MixedReadWrite_HighConcurrency_PerformsWell()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream8 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream8, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order));

        // Pre-populate with some data
        for (int i = 0; i < 100; i++)
        {
            await new Product { Name = $"InitialProduct{i}", Price = i * 5, InStock = true }.SaveAsync();
            await new Order { CustomerName = $"InitialCustomer{i}", Total = i * 10, IsPaid = true }.SaveAsync();
        }

        // Act - Simulate 100 users doing mixed read/write operations
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            // Each "user" does: create product, query products, create order, query orders
            var product = new Product { Name = $"User{i}Product", Price = i * 2, InStock = true };
            await product.SaveAsync();

            using (var productContext = new SxmDbContext("products"))
            {
                var products = productContext.GetTable<Product>().Where(p => p.Price < 500).ToList();
                Assert.NotEmpty(products);
            }

            var order = new Order { CustomerName = $"User{i}", Total = i * 20, IsPaid = true };
            await order.SaveAsync();

            using (var orderContext = new SxmDbContext("orders"))
            {
                var orders = orderContext.GetTable<Order>().Where(o => o.Total > 100).ToList();
                Assert.NotEmpty(orders);
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Console.WriteLine($"100 concurrent users (400 operations) completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 60,
            $"Mixed operations took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <60s)");

        // Verify final counts
        using (var productContext = new SxmDbContext("products"))
        {
            Assert.Equal(200, productContext.GetTable<Product>().Count());
        }
        using (var orderContext = new SxmDbContext("orders"))
        {
            Assert.Equal(200, orderContext.GetTable<Order>().Count());
        }
    }

    #endregion

    #region Memory & Resource Tests

    [Fact]
    public async Task LongRunningOperations_1000Iterations_NoMemoryLeak()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream9 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream9, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        // Force GC and get baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Perform 1000 operations with contexts
        for (int i = 0; i < 1000; i++)
        {
            await new Product { Name = $"Product{i}", Price = i, InStock = true }.SaveAsync();

            await using (var context = new SxmDbContext("products"))
            {
                var products = context.GetTable<Product>().Where(p => p.InStock).ToList();
            }

            // Periodic GC to prevent false positives
            if (i % 100 == 0)
            {
                GC.Collect(0);
            }
        }

        // Force GC and measure final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        // Assert - Memory growth should be reasonable (<100MB for 1000 operations)
        var memoryGrowthMB = (finalMemory - initialMemory) / 1024.0 / 1024.0;
        Console.WriteLine($"Memory growth after 1000 operations: {memoryGrowthMB:F2}MB");
        Assert.True(memoryGrowthMB < 100,
            $"Memory grew by {memoryGrowthMB:F2}MB (expected <100MB)");

        // Verify all operations completed
        await using (var context = new SxmDbContext("products"))
        {
            Assert.Equal(1000, context.GetTable<Product>().Count());
        }
    }

    [Fact]
    public async Task UpdateOperations_LargeDataset_PerformEfficiently()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream10 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream10, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        // Create 5000 products
        var products = new List<Product>();
        for (int i = 0; i < 5000; i++)
        {
            var product = new Product
            {
                Name = $"Product {i}",
                Price = i,
                InStock = false
            };
            await product.SaveAsync();
            products.Add(product);
        }

        // Act - Update all products
        var stopwatch = Stopwatch.StartNew();

        foreach (var product in products)
        {
            product.InStock = true;
            product.Price = product.Price * 1.1m;
            await product.SaveAsync();
        }

        stopwatch.Stop();

        // Assert
        Console.WriteLine($"5000 updates completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 60,
            $"5000 updates took {stopwatch.Elapsed.TotalSeconds:F2}s (expected <60s)");

        // Verify updates
        await using (var context = new SxmDbContext("products"))
        {
            var allInStock = context.GetTable<Product>().All(p => p.InStock);
            Assert.True(allInStock);
        }
    }

    #endregion

    #region Test Entity Classes

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "products")]
    public class Product : SxmEntity
    {
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public bool InStock { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "orders")]
    public class Order : SxmEntity
    {
        public string? CustomerName { get; set; }
        public decimal Total { get; set; }
        public bool IsPaid { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false, Database = "audit")]
    public class AuditLog : SxmEntity
    {
        public string? Action { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
