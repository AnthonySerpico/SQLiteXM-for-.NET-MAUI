using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SQLiteXM
{
    public class SxmData
    {
        private bool mustReconcile = true;
        private static object lockObject = new object();
        private string? databaseName = SxmConnection.ImplicitDatabaseName;
        private static Dictionary<string, string> columnNameAndType = new Dictionary<string, string>();

        public virtual long id { get; set; }

        public SxmData(string? databaseName)
        {
            this.databaseName = databaseName;
            initialize();
        }
        public SxmData()
        {
            initialize();
        }

        private void initialize()
        {
            lock (lockObject)
            {
                ensureDatabaseName();

                if (columnNameAndType.Count == 0)
                {
                    createTable();
                    reconcile();
                }
            }
        }

        public virtual async Task PerformInsert(string sqlStatementName)
        {
            Dictionary<string, object?> result = await SxmStatement.PerformInsert<SxmData>(sqlStatementName, this, databaseName);
            loadDbValues(result);
        }

        public virtual async Task PerformUpdate(string sqlStatementName)
        {
            await SxmStatement.PerformUpdate<SxmData>(sqlStatementName, this, databaseName);
        }

        public virtual async Task PerformDelete(string sqlStatementName)
        {
            await SxmStatement.PerformDelete<SxmData>(sqlStatementName, this, databaseName);
        }

        private void reconcile()
        {
            Dictionary<string, string> dbTableColumnNameAndType = SxmInit.getTableColumnNames(databaseName, this.GetType().Name);

            SxmConnection sxmConnection = new SxmConnection(databaseName);  // Creates an implicit database name.
            try
            {
                foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} ADD {1} {2}", this.GetType().Name, kvp.Key, kvp.Value);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                        {
                            sxmTransaction1.executeAlterTable(alterDefinition);
                            sxmTransaction1.commitTransaction();
                        }
                    }
                }

                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!columnNameAndType.ContainsKey(kvp.Key) && !kvp.Key.Equals("id") && !kvp.Key.Equals("systemSynchID"))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} DROP {1}", this.GetType().Name, kvp.Key);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                        {
                            sxmTransaction1.executeAlterTable(alterDefinition);
                            sxmTransaction1.commitTransaction();
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (sxmConnection != null)
                    sxmConnection.destroyConnection();
            }
        }

        private void ensureDatabaseName()
        {
            if (this.databaseName == null)
            {
                SxmConnection? sxmConnection = default(SxmConnection);

                try
                {
                    sxmConnection = new SxmConnection(this.databaseName);  // Creates an implicit database name.
                }
                catch (Exception)
                {
                }
                finally
                {
                    if (sxmConnection != default(SxmConnection))
                        sxmConnection.destroyConnection();

                    this.databaseName = SxmConnection.ImplicitDatabaseName;
                    if (this.databaseName == null)
                    {
                        throw new InvalidDataException("The database name cannot be null.");
                    }
                }
            }
        }

        private void createTable()
        {
            getColumnNamesAndDataTypes();
            string tableStatement = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT", this.GetType().Name);

            foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                tableStatement += string.Format(", {0} {1}", kvp.Key, kvp.Value);

            tableStatement += ")";

            SqlStatements.addTableDefinition(string.Format("{0}.{1}", this.databaseName, this.GetType().Name), tableStatement);
            SxmInit.createTable(this.databaseName, this.GetType().Name);
        }

        private void getColumnNamesAndDataTypes()
        {
            PropertyInfo[]? thisPropertyInfo = this.GetType().GetProperties();

            foreach (PropertyInfo pi in thisPropertyInfo)
            {
                string piType = pi.PropertyType.Name;
                string piName = pi.Name;

                if (!piName.Equals("id"))
                {
                    if (piType == typeof(int).Name)
                        columnNameAndType.Add(piName, "int");

                    else if (piType == typeof(string).Name)    // Can be either text or double for saving ticks.
                        columnNameAndType.Add(piName, "text");

                    else if (piType == typeof(long).Name)
                        columnNameAndType.Add(piName, "long");

                    else if (piType == typeof(ulong).Name)    // Large values will overflow.
                        columnNameAndType.Add(piName, "ulong");

                    else if (piType == typeof(float).Name)
                        columnNameAndType.Add(piName, "float");

                    else if (piType == typeof(short).Name)
                        columnNameAndType.Add(piName, "short");

                    else if (piType == typeof(ushort).Name)
                        columnNameAndType.Add(piName, "ushort");

                    else if (piType == typeof(uint).Name)
                        columnNameAndType.Add(piName, "uint");

                    else if (piType == typeof(sbyte).Name)
                        columnNameAndType.Add(piName, "sbyte");

                    else if (piType == typeof(byte).Name)
                        columnNameAndType.Add(piName, "byte");

                    else if (piType == typeof(double).Name)
                        columnNameAndType.Add(piName, "double");

                    else if (piType == typeof(string).Name)
                        columnNameAndType.Add(piName, "string");

                    else if (piType == typeof(decimal).Name)
                        columnNameAndType.Add(piName, "text");

                    else if (piType == typeof(bool).Name)
                        columnNameAndType.Add(piName, "bool");

                    else if (piType == typeof(DateTime).Name)
                        columnNameAndType.Add(piName, "DateTime");

                    else if (piType == typeof(DateTimeOffset).Name)
                        columnNameAndType.Add(piName, "DateTimeOffset");

                    else if (piType == typeof(TimeSpan).Name)
                        columnNameAndType.Add(piName, "TimeSpan");

                    else if (piType == typeof(DateOnly).Name)
                        columnNameAndType.Add(piName, "DateOnly");

                    else if (piType == typeof(TimeOnly).Name)
                        columnNameAndType.Add(piName, "TimeOnly");
                }
            }
        }

        private void loadDbValues(Dictionary<string, object?> databaseRecord)
        {
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = this.GetType().GetProperty(kvp.Key);
                    if (pi != null)
                    {
                        if (kvp.Value != DBNull.Value && kvp.Value != null)
                        {
                            string piType = pi.PropertyType.Name;

                            if (piType == typeof(int).Name)
                                pi.SetValue(this, (int)(long)kvp.Value);

                            else if (piType == typeof(long).Name)
                                pi.SetValue(this, (long)kvp.Value);

                            else if (piType == typeof(ulong).Name)    // Large values will overflow.
                                pi.SetValue(this, (ulong)(long)kvp.Value);

                            else if (piType == typeof(float).Name)
                                pi.SetValue(this, (float)(double)kvp.Value);

                            else if (piType == typeof(short).Name)
                                pi.SetValue(this, (short)(long)kvp.Value);

                            else if (piType == typeof(ushort).Name)
                                pi.SetValue(this, (ushort)(long)kvp.Value);

                            else if (piType == typeof(uint).Name)
                                pi.SetValue(this, (uint)(long)kvp.Value);

                            else if (piType == typeof(sbyte).Name)
                                pi.SetValue(this, (sbyte)(long)kvp.Value);

                            else if (piType == typeof(byte).Name)
                                pi.SetValue(this, (byte)(long)kvp.Value);

                            else if (piType == typeof(double).Name)
                                pi.SetValue(this, (double)kvp.Value);

                            else if (piType == typeof(string).Name)
                                pi.SetValue(this, kvp.Value.ToString());

                            else if (piType == typeof(decimal).Name)    // Can be either text or double. Double will lose precision
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(string).Name)
                                    pi.SetValue(this, Decimal.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(long).Name)
                                    pi.SetValue(this, (decimal)(long)kvp.Value);   // Will lose precision.

                                else if (typeName == typeof(double).Name)
                                    pi.SetValue(this, (decimal)(double)kvp.Value);   // Will lose precision.
                            }

                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(this, true);
                                else
                                    pi.SetValue(this, false);
                            }

                            else if (piType == typeof(DateTime).Name)  // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, new DateTime((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateTime.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateTimeOffset).Name)  // Can be either text or DATETIMEOFFSET.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, DateTimeOffset.FromUnixTimeSeconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateTimeOffset.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(TimeSpan).Name)  // Can be either text or TIMESPAN.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, TimeSpan.FromTicks((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, TimeSpan.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateOnly).Name)  // Must be text.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, DateOnly.FromDayNumber((int)(long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateOnly.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(int).Name)
                                    pi.SetValue(this, DateOnly.FromDayNumber((int)kvp.Value));
                            }

                            else if (piType == typeof(TimeOnly).Name)    // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, new TimeOnly((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, TimeOnly.Parse(kvp.Value.ToString()!));
                            }

                            else
                            {
                                pi.SetValue(this, kvp.Value);
                            }

                        }
                        else
                            pi.SetValue(this, default);
                    }
                }
                catch (System.ArgumentException)
                {
                    string? userPropertyType = this.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString();
                    string? databasePropertyType = kvp.Value?.GetType().ToString();
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", kvp.Key, databasePropertyType, this.GetType().ToString() + "." + kvp.Key, userPropertyType));
                }
            }
        }
    }
}
