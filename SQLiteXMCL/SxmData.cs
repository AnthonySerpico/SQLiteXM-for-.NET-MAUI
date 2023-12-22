using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    public class SxmData
    {
        private string? databaseName = default;

        public SxmData(string? dbName) { this.databaseName = dbName; }
        public SxmData() { }

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
