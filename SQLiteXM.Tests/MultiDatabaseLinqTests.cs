using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for LINQ query operations across multiple databases.
/// Validates that LinqToDB query generation and execution work correctly
/// when entities are distributed across different database files.
/// </summary>
[Collection("MultiDatabase")]
public class MultiDatabaseLinqTests : IDisposable
{
    private static readonly string TestFolder;
    private readonly string _testStatementsPath;
    private readonly string _testId;
    private readonly string _testDbFolder;
    private bool _disposed = false;

    static MultiDatabaseLinqTests()
    {
        TestFolder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "MultiDatabaseLinq");
        Directory.CreateDirectory(TestFolder);
    }

    public MultiDatabaseLinqTests()
    {
        _testId = Guid.NewGuid().ToString("N");
        _testStatementsPath = Path.Combine(TestFolder, $"statements_{_testId}.json");
        _testDbFolder = Path.Combine(TestFolder, _testId);
        Directory.CreateDirectory(_testDbFolder);
    }

    public void Dispose()
    {
        if (_disposed) return;

#if !KEEP_MULTI_DB_TEST_FILES
        try
        {
            if (File.Exists(_testStatementsPath))
                File.Delete(_testStatementsPath);

            var dbFiles = Directory.GetFiles(TestFolder, $"*{_testId}*");
            foreach (var file in dbFiles)
            {
                try { File.Delete(file); } catch { }
            }

            if (Directory.Exists(_testDbFolder))
            {
                try { Directory.Delete(_testDbFolder, true); } catch { }
            }
        }
        catch { }
#else
        Console.WriteLine($"Test files preserved at: {TestFolder}");
        Console.WriteLine($"Test ID: {_testId}");
#endif

        // CRITICAL: Reset SQLiteXM state so subsequent tests work correctly
        try
        {
            SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();

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
    }
  ]
}";
        File.WriteAllText(_testStatementsPath, json);
    }

    #region WHERE Clause Tests

    [Fact]
    public async Task Where_WithSingleCondition_FiltersCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream1 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream1, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order));

        // Insert test data
        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var inStockProducts = context.GetTable<Product>()
                .Where(p => p.InStock)
                .ToList();

            // Assert
            Assert.Equal(2, inStockProducts.Count);
            Assert.All(inStockProducts, p => Assert.True(p.InStock));
        }
    }

    [Fact]
    public async Task Where_WithMultipleConditions_FiltersCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream2 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream2, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync();
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync();
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync();
        await new Product { Name = "Date", Price = 3.99m, InStock = true }.SaveAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var affordableInStock = context.GetTable<Product>()
                .Where(p => p.InStock && p.Price < 2.00m)
                .ToList();

            // Assert
            Assert.Equal(2, affordableInStock.Count);
            Assert.All(affordableInStock, p =>
            {
                Assert.True(p.InStock);
                Assert.True(p.Price < 2.00m);
            });
        }
    }

    [Fact]
    public async Task Where_AcrossMultipleDatabases_WorksIndependently()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream3 = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream3, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product), typeof(Order));

        var prodConnection = new SxmConnection("products", shared: false);
        await using var prodTransaction = await SxmSqlTransaction.CreateAsync(prodConnection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(prodTransaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = false }.SaveAsync(prodTransaction);

        await prodTransaction.CommitTransactionAsync();

        var ordConnection = new SxmConnection("orders", shared: false);
        await using var ordTransaction = await SxmSqlTransaction.CreateAsync(ordConnection);

        await new Order { CustomerName = "John", Total = 100.00m, IsPaid = true }.SaveAsync(ordTransaction);
        await new Order { CustomerName = "Jane", Total = 50.00m, IsPaid = false }.SaveAsync(ordTransaction);

        await ordTransaction.CommitTransactionAsync();

        // Act & Assert - Products database
        using (var context = new SxmLinqDbContext("products"))
        {
            var inStockProducts = context.GetTable<Product>().Where(p => p.InStock).ToList();
            Assert.Single(inStockProducts);
            Assert.Equal("Apple", inStockProducts[0].Name);
        }

        // Act & Assert - Orders database
        using (var context = new SxmLinqDbContext("orders"))
        {
            var paidOrders = context.GetTable<Order>().Where(o => o.IsPaid).ToList();
            Assert.Single(paidOrders);
            Assert.Equal("John", paidOrders[0].CustomerName);
        }
    }

    #endregion

    #region SELECT (Projection) Tests

    [Fact]
    public async Task Select_ProjectToAnonymousType_WorksCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var productSummaries = context.GetTable<Product>()
                .Select(p => new { p.Name, p.Price })
                .ToList();

            // Assert
            Assert.Equal(2, productSummaries.Count);
            Assert.Contains(productSummaries, p => p.Name == "Apple" && p.Price == 1.99m);
            Assert.Contains(productSummaries, p => p.Name == "Banana" && p.Price == 0.99m);
        }
    }

    [Fact]
    public async Task Select_SingleProperty_WorksCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var productNames = context.GetTable<Product>()
                .Select(p => p.Name)
                .ToList();

            // Assert
            Assert.Equal(3, productNames.Count);
            Assert.Contains("Apple", productNames);
            Assert.Contains("Banana", productNames);
            Assert.Contains("Cherry", productNames);
        }
    }

    #endregion

    #region OrderBy Tests

    [Fact]
    public async Task OrderBy_Ascending_SortsCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Cherry", Price = 2.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var sortedProducts = context.GetTable<Product>()
                .OrderBy(p => p.Price)
                .ToList();

            // Assert
            Assert.Equal(3, sortedProducts.Count);
            Assert.Equal("Banana", sortedProducts[0].Name);
            Assert.Equal("Apple", sortedProducts[1].Name);
            Assert.Equal("Cherry", sortedProducts[2].Name);
        }
    }

    [Fact]
    public async Task OrderByDescending_Descending_SortsCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 3.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 0.99m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var sortedProducts = context.GetTable<Product>()
                .OrderByDescending(p => p.Price)
                .ToList();

            // Assert
            Assert.Equal(3, sortedProducts.Count);
            Assert.Equal("Banana", sortedProducts[0].Name);  // 3.99 - highest
            Assert.Equal("Apple", sortedProducts[1].Name);   // 1.99 - middle
            Assert.Equal("Cherry", sortedProducts[2].Name);  // 0.99 - lowest
        }
    }

    [Fact]
    public async Task OrderBy_ThenBy_MultipleColumns_SortsCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 1.99m, InStock = false }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 0.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var sortedProducts = context.GetTable<Product>()
                .OrderBy(p => p.Price)
                .ThenBy(p => p.Name)
                .ToList();

            // Assert
            Assert.Equal(3, sortedProducts.Count);
            Assert.Equal("Cherry", sortedProducts[0].Name);
            Assert.Equal("Apple", sortedProducts[1].Name);
            Assert.Equal("Banana", sortedProducts[2].Name);
        }
    }

    #endregion

    #region Aggregate Function Tests

    [Fact]
    public async Task Count_ReturnsCorrectCount()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync();
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync();
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var totalCount = context.GetTable<Product>().Count();
            var inStockCount = context.GetTable<Product>().Count(p => p.InStock);

            // Assert
            Assert.Equal(3, totalCount);
            Assert.Equal(2, inStockCount);
        }
    }

    [Fact]
    public async Task Any_ReturnsCorrectResult()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var hasInStock = context.GetTable<Product>().Any(p => p.InStock);
            var hasExpensive = context.GetTable<Product>().Any(p => p.Price > 10.00m);

            // Assert
            Assert.True(hasInStock);
            Assert.False(hasExpensive);
        }
    }

    [Fact]
    public async Task Sum_CalculatesCorrectTotal()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Order));

        var connection = new SxmConnection("orders", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Order { CustomerName = "Alice", Total = 100.00m, IsPaid = true }.SaveAsync(transaction);
        await new Order { CustomerName = "Bob", Total = 250.00m, IsPaid = true }.SaveAsync(transaction);
        await new Order { CustomerName = "Charlie", Total = 75.00m, IsPaid = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("orders"))
        {
            var totalRevenue = context.GetTable<Order>().Sum(o => o.Total);
            var paidRevenue = context.GetTable<Order>().Where(o => o.IsPaid).Sum(o => o.Total);

            // Assert
            Assert.Equal(425.00m, totalRevenue);
            Assert.Equal(350.00m, paidRevenue);
        }
    }

    [Fact]
    public async Task Average_CalculatesCorrectAverage()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.00m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 2.00m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 3.00m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var avgPrice = context.GetTable<Product>().Average(p => p.Price);

            // Assert
            Assert.Equal(2.00m, avgPrice);
        }
    }

    [Fact]
    public async Task Min_Max_ReturnCorrectValues()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 2.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var minPrice = context.GetTable<Product>().Min(p => p.Price);
            var maxPrice = context.GetTable<Product>().Max(p => p.Price);

            // Assert
            Assert.Equal(0.99m, minPrice);
            Assert.Equal(2.99m, maxPrice);
        }
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Skip_Take_PaginationWorks()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        for (int i = 1; i <= 10; i++)
        {
            await new Product { Name = $"Product {i}", Price = i * 1.00m, InStock = true }.SaveAsync(transaction);
        }

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var page1 = context.GetTable<Product>()
                .OrderBy(p => p.Name)
                .Take(3)
                .ToList();

            var page2 = context.GetTable<Product>()
                .OrderBy(p => p.Name)
                .Skip(3)
                .Take(3)
                .ToList();

            // Assert
            // String sorting gives: "Product 1", "Product 10", "Product 2", "Product 3", ...
            Assert.Equal(3, page1.Count);
            Assert.Equal(3, page2.Count);
            Assert.Equal("Product 1", page1[0].Name);
            Assert.Equal("Product 10", page1[1].Name);
            Assert.Equal("Product 2", page1[2].Name);
            Assert.Equal("Product 3", page2[0].Name);
            Assert.Equal("Product 4", page2[1].Name);
            Assert.Equal("Product 5", page2[2].Name);
        }
    }

    #endregion

    #region Complex Query Tests

    [Fact]
    public async Task ComplexQuery_WithMultipleOperations_WorksCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync(transaction);
        await new Product { Name = "Date", Price = 3.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Elderberry", Price = 1.49m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act - Complex query: Filter, Sort, Project, Paginate
        using (var context = new SxmLinqDbContext("products"))
        {
            var results = context.GetTable<Product>()
                .Where(p => p.Price < 3.00m)
                .OrderByDescending(p => p.Price)
                .Select(p => new { p.Name, p.Price, p.InStock })
                .Skip(1)
                .Take(2)
                .ToList();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("Apple", results[0].Name);
            Assert.Equal("Elderberry", results[1].Name);
        }
    }

    [Fact]
    public async Task FirstOrDefault_ReturnsCorrectResult()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Banana", Price = 0.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var found = context.GetTable<Product>().FirstOrDefault(p => p.Name == "Apple");
            var notFound = context.GetTable<Product>().FirstOrDefault(p => p.Name == "Zebra");

            // Assert
            Assert.NotNull(found);
            Assert.Equal("Apple", found.Name);
            Assert.Null(notFound);
        }
    }

    [Fact]
    public async Task SingleOrDefault_ReturnsCorrectResult()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "UniqueProduct", Price = 9.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var unique = context.GetTable<Product>().SingleOrDefault(p => p.Name == "UniqueProduct");
            var notFound = context.GetTable<Product>().SingleOrDefault(p => p.Name == "Zebra");

            // Assert
            Assert.NotNull(unique);
            Assert.Equal("UniqueProduct", unique.Name);
            Assert.Null(notFound);
        }
    }

    #endregion

    #region NULL Handling Tests

    [Fact]
    public async Task Where_WithNullCheck_HandlesNullsCorrectly()
    {
        // Arrange
        CreateMultiDatabaseSqlStatements();
        var options = new SxmDatabaseOptions { DatabaseFolderOverride = _testDbFolder };

#if DEBUG
        await SxmDatabase.ResetForTestingAsync();
#endif
        using var stream = File.OpenRead(_testStatementsPath);
        await SxmDatabase.InitializeAsync(stream, options);
        await SxmDatabase.RegisterEntitiesAsync(typeof(Product));

        var connection = new SxmConnection("products", shared: false);
        await using var transaction = await SxmSqlTransaction.CreateAsync(connection);

        await new Product { Name = "Apple", Price = 1.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = null, Price = 0.99m, InStock = true }.SaveAsync(transaction);
        await new Product { Name = "Cherry", Price = 2.99m, InStock = false }.SaveAsync(transaction);

        await transaction.CommitTransactionAsync();

        // Act
        using (var context = new SxmLinqDbContext("products"))
        {
            var withNames = context.GetTable<Product>().Where(p => p.Name != null).ToList();
            var withoutNames = context.GetTable<Product>().Where(p => p.Name == null).ToList();

            // Assert
            Assert.Equal(2, withNames.Count);
            Assert.Single(withoutNames);
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

    #endregion
}
