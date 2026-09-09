# QueryGalleryDemo - Pre-Built Windows Application

**A comprehensive, interactive demonstration of SQLiteXM's query capabilities**

**[📥 Download QueryGalleryDemo_Windows.zip](https://querygallerydemo.s3.us-east-1.amazonaws.com/QueryGalleryDemo_Windows.zip)**

---

## 📦 What's Included

This is a **ready-to-run** MAUI Windows application that showcases SQLiteXM through 100+ working query examples organized into 10 categories. No build tools or development environment required!

## 🎯 What You'll See

**QueryGalleryDemo demonstrates:**

| Operation | Type | Details |
|-----------|------|---------|
| **Basic Queries** | LINQ | SELECT, WHERE, ORDER BY, LIKE patterns
| **Joins & Navigation** | LINQ | INNER JOIN, LEFT JOIN, multi-table queries
| **Aggregations** | LINQ | COUNT, SUM, AVG, GROUP BY operations
| **Advanced LINQ** | LINQ | Pagination, complex sorting, compound filters
| **Raw SQL** | SQL | Custom SQL statements loaded from configuration
| **Performance** | LINQ | Large datasets and benchmarks
| **Many-to-Many** | LINQ | Junction table queries and bidirectional navigation
| **Transactions** | LINQ | Atomic operations with commit/rollback patterns
| **Data Modification** | LINQ | INSERT, UPDATE, DELETE with bulk operations
| **Mixed Transactions** | LINQ + SQL + Entity DML | LINQ + Entity DML + Raw SQL in a single transaction

Result set, execution time, and record counts for every query.

**Realistic Data Set** - ~25,000 records across 11 related tables (Chinook-style music database)

## 🚀 How to Run

1. **Extract the ZIP file** to any folder on your computer
2. **Navigate to the extracted folder**
3. **Double-click `QueryGalleryDemo.exe`** to launch the application

## 💻 System Requirements

- **Operating System**: Windows 10+
- **No .NET installation required** - .NET 9 Runtime is included!

## 🔧 Troubleshooting

**"Windows protected your PC" warning?**  
Click "More info" → "Run anyway" (normal for apps without paid code signing)

**App won't start or crashes?**  
Make sure you extracted all files from the ZIP (don't run directly from ZIP)

## 📚 Want to Build It Yourself?

The full source code is available in the SQLiteXM repository:

**Repository:** [SQLiteXM for .NET MAUI](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI)

Navigate to: `Samples/QueryGalleryDemo/`

Build instructions and architecture details are in the project's README.

## 💡 About SQLiteXM

SQLiteXM is a high-performance, entity-first ORM for SQLite designed specifically for .NET MAUI applications.

**Key Features:**
- ✅ LINQ query support with full IntelliSense
- ✅ Entity-first architecture with built-in persistence
- ✅ AOT & IL Trimming Safe
- ✅ Automatic schema evolution
- ✅ Full transaction support
- ✅ Built-in INotifyPropertyChanged for MAUI binding
- ✅ Minimal configuration required

**NuGet Package:** [SQLiteXM](https://www.nuget.org/packages/SQLiteXM/)

**Documentation:** [SQLiteXM Docs](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)

## 📄 License

This demo application and SQLiteXM are both open source. See the repository for license details.

---

**Questions or Issues?** Visit the [GitHub Repository](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI) to file issues or start discussions.
