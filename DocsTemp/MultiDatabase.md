# Multi-Database Support

> 📖 **Guide Status**: Coming soon  
> This guide will cover working with multiple SQLite databases in a single application.

## Quick Preview

Topics covered in this guide:

### Why Multiple Databases?
- Tenant isolation
- Separating concerns (user data vs. app data)
- Performance benefits
- Backup/restore strategies

### Configuration
- Defining multiple databases in `statements.json`
- Setting default databases
- Database-specific entity configuration

### Cross-Database Queries
- Querying across databases
- Limitations and workarounds
- Performance considerations

### Performance Considerations
- When to use multiple databases
- Connection management
- Transaction boundaries

---

**For now**, see the [test suite](../SQLiteXM.Tests/MultiDatabaseTests.cs) for multi-database examples.

Want to contribute? This guide needs expansion! See [CONTRIBUTING.md](../CONTRIBUTING.md).
