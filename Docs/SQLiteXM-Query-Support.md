# Working with Data in SQLiteXM

SQLiteXM provides two complementary query APIs and a built-in entity persistence API:

- **LINQ** for strongly typed, composable queries.
- **Direct SQL** for complete control, SQLite-specific features, and hand-written statements.
- **Entity DML** for letting SxmEntity instances save, update, and delete themselves with a single method call.

Use LINQ when you want a queryable, C#-friendly approach. Use direct SQL when you want to write the SQL yourself or work at a lower level. Use entity DML when you're working with `SxmEntity`-derived classes and want the simplest possible persistence — `SaveAsync` / `DeleteAsync` on the instance itself, with automatic participation in an ambient `SxmSqlTransaction`.

## Related documents

- [SQLiteXM LINQ Support](SQLiteXM-LINQ-Support.md)
- [SQLiteXM Embedded SQL Support](SQLiteXM-SQL-Support.md)
- [SQLiteXM Named SQL Statement Support](SQLiteXM-Named-Statements.md)
- [SQLiteXM Entity DML — Save, Update, and Delete](SQLiteXM-Entity-DML.md)
