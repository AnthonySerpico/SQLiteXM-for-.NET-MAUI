# Saving Data

> 📖 **Guide Status**: Coming soon  
> This guide will cover insert, update, delete, batch operations, and error handling.

## Quick Preview

Topics covered in this guide:

### Insert, Update, Delete
- `SaveAsync()` - auto-detects insert vs. update
- `InsertOrUpdateAsync()` - explicit upsert
- `DeleteAsync()` - remove entities

### Batch Operations
- Saving multiple entities efficiently
- Using transactions for batches
- Performance optimization

### Bulk LINQ Operations
- `UpdateAsync()` for bulk updates
- `DeleteAsync()` for bulk deletes
- Set-based operations vs. entity-based

### SubmitChanges Pattern
- `InsertOnSubmit`, `UpdateOnSubmit`, `DeleteOnSubmit`
- `SubmitChangesAsync()` for batching
- Error handling with `throwIfFailed`

### Error Handling
- Handling constraint violations
- Dealing with concurrency conflicts
- Transaction rollback on error

---

**For now**, see the [Getting Started guide](GettingStarted.md) for basic save/update/delete examples.

Want to contribute? This guide needs expansion! See [CONTRIBUTING.md](../CONTRIBUTING.md).
