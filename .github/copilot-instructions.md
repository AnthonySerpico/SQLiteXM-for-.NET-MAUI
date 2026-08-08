# Copilot Instructions

## Project Guidelines
- In the SQLiteXM library, entity persistence should use the parameterless ambient-transaction pattern: entity.SaveAsync() / entity.DeleteAsync() (which pick up SxmAmbientTransaction.Current). The explicit-transaction overloads SaveAsync(SxmSqlTransaction) / DeleteAsync(SxmSqlTransaction) are considered unnecessary and can be removed. Do not use ctx.InsertAsync(entity) where entity.SaveAsync() suffices.