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
                            else if (piType == typeof(float).Name)
                                pi.SetValue(this, (float)(double)kvp.Value);
                            else if (piType == typeof(double).Name)
                                pi.SetValue(this, (double)kvp.Value);
                            else if (piType == typeof(decimal).Name)
                                pi.SetValue(this, (decimal)(double)kvp.Value);
                            else if (piType == typeof(string).Name)
                                pi.SetValue(this, kvp.Value.ToString());
                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(this, true);
                                else
                                    pi.SetValue(this, false);
                            }
                            else if (piType == typeof(DateTime).Name)
                            {
                                if (kvp.Value.GetType().Name == typeof(string).Name)
                                    pi.SetValue(this, DateTime.Parse(kvp.Value.ToString()!));
                                if (kvp.Value.GetType().Name == typeof(double).Name)
                                    pi.SetValue(this, new DateTime((long)(double)kvp.Value));
                            }
                            else
                                pi.SetValue(this, kvp.Value);

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
