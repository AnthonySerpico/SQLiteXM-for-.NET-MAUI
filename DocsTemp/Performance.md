# Performance Guide

> 📖 **Guide Status**: Coming soon  
> This guide will cover optimization techniques, benchmarks, and best practices.

## Quick Preview

Topics covered in this guide:

### Benchmarks vs. Other ORMs
- SQLiteXM vs. Entity Framework Core
- SQLiteXM vs. SQLite-net
- SQLiteXM vs. Dapper
- Real-world performance tests

### Transaction Optimization
- Why transactions matter for bulk operations
- Single transaction vs. multiple transactions
- Performance impact (60x faster with transactions!)

### Bulk Operations
- Batch inserts with transactions
- Bulk LINQ operations
- Connection pooling benefits

### Indexing Strategies
- When to create indexes
- Compound indexes for multi-column queries
- Index overhead considerations

### Connection Management
- Connection pooling in SQLiteXM
- Shared vs. non-shared connections
- When to use `SxmConnection`

---

**Performance Highlights** (from our test suite):

| Operation | Time | Details |
|-----------|------|---------|
| 10,000 row insert (transacted) | ~0.45s | Using explicit transaction |
| 10,000 row insert (individual) | ~27s | 60x slower without transaction |
| 50,000 row query | ~14ms | With index |
| Complex LINQ query (20K rows) | ~12ms | Joins + filters |
| 100 concurrent writes | ~1.2s | Thread-safe operations |

Want to contribute? This guide needs expansion! See [CONTRIBUTING.md](../CONTRIBUTING.md).
