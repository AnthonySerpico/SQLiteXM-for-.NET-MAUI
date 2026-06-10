# Transactions

> 📖 **Guide Status**: Coming soon  
> This guide will cover transaction patterns, rollback, error recovery, and best practices.

## Quick Preview

Topics covered in this guide:

### Transaction Basics
- What are transactions?
- ACID properties
- When to use transactions

### Explicit Transactions
- `SxmSqlTransaction.CreateAsync()`
- `SaveAsync(transaction)` pattern
- `CommitTransactionAsync()` and rollback

### Rollback & Error Recovery
- Automatic rollback on exceptions
- Manual rollback with `RollbackTransactionAsync()`
- Handling partial failures

### Best Practices
- Always use transactions for multi-entity operations
- Keep transactions short
- Avoid UI operations inside transactions
- Performance implications

---

**For now**, see the [Getting Started guide](GettingStarted.md#step-6-transactions-optional) for basic transaction examples.

Explore the **[Query Gallery Demo](../Samples/QueryGalleryDemo/)** → **Transactions** category for interactive examples!

Want to contribute? This guide needs expansion! See [CONTRIBUTING.md](../CONTRIBUTING.md).
