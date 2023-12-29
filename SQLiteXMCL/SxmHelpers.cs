using System.Reflection;



//using static CoreFoundation.DispatchSource;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    // A Transaction object represents a series of SQL statements that will be executes as a single transaction.
    public class SxmHelpers
    {
        private SxmHelpers() { }

        internal static string GetDatabaseStatementTypeName(SqlStatementType statementType)
        {

            if (statementType == SqlStatementType.select || statementType == SqlStatementType.selectDirect)
                return "SELECT";
            if (statementType == SqlStatementType.insert)
                return "INSERT";
            if (statementType == SqlStatementType.delete || statementType == SqlStatementType.deleteDirect)
                return "DELETE";
            if (statementType == SqlStatementType.update || statementType == SqlStatementType.updateDirect)
                return "UPDATE";

            throw new ArgumentException("The sql statement type could not be found.");
        }

        internal static SqlStatementType GetDatabaseStatementType(string? sqlStatementName)
        {
            if (sqlStatementName == null)
                throw new ArgumentException("A sql statement name cannot be null.");

            if (SqlStatements.selectStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.select;

            if (SqlStatements.insertStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.insert;

            if (SqlStatements.updateStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.update;

            if (SqlStatements.deleteStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.delete;

            // Direct SQL statement queries are processed here.
            if (sqlStatementName.StartsWith("SELECT ", true, null))
                return SqlStatementType.selectDirect;

            if (sqlStatementName.StartsWith("DELETE ", true, null))
                return SqlStatementType.deleteDirect;

            if (sqlStatementName.StartsWith("UPDATE ", true, null))
                return SqlStatementType.updateDirect;

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName.Length > 30 ? (sqlStatementName.Substring(0, 29) + "...") : sqlStatementName));
        }

        internal static List<T> populateUserRecord<T>(List<Dictionary<string, object?>> databaseRowsList) where T : class, new()
        {
            List<T> userObjectList = new List<T>();

            foreach (Dictionary<string, object?> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                T userObject = new T();
                loadDbValues(databaseRecord, ref userObject);
                userObjectList.Add(userObject);
            }

            return userObjectList;
        }

        // Data being loaded into a user object; usualy after a select.
        internal static void loadDbValues<T>(Dictionary<string, object?> databaseRecord, ref T userObject) where T : class
        {
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = userObject.GetType().GetProperty(kvp.Key);
                    if (pi != null)
                    {
                        if (kvp.Value != DBNull.Value && kvp.Value != null)
                        {
                            string piType = pi.PropertyType.Name;
                            Type? underlyingType = Nullable.GetUnderlyingType(pi.PropertyType);
                            if (underlyingType != null)
                            {
                                piType = underlyingType.Name;
                            }

                            if (piType == typeof(int).Name)
                                pi.SetValue(userObject, (int)(long)kvp.Value);

                            else if (piType == typeof(long).Name)
                                pi.SetValue(userObject, (long)kvp.Value);

                            else if (piType == typeof(ulong).Name)    // Large values will overflow.
                                pi.SetValue(userObject, (ulong)ulong.Parse((string)kvp.Value));

                            else if (piType == typeof(float).Name)
                                pi.SetValue(userObject, (float)(double)kvp.Value);

                            else if (piType == typeof(short).Name)
                                pi.SetValue(userObject, (short)(long)kvp.Value);

                            else if (piType == typeof(ushort).Name)
                                pi.SetValue(userObject, (ushort)(long)kvp.Value);

                            else if (piType == typeof(uint).Name)
                                pi.SetValue(userObject, (uint)(long)kvp.Value);

                            else if (piType == typeof(sbyte).Name)
                                pi.SetValue(userObject, (sbyte)(long)kvp.Value);

                            else if (piType == typeof(byte).Name)
                                pi.SetValue(userObject, (byte)(long)kvp.Value);

                            else if (piType == typeof(double).Name)
                                pi.SetValue(userObject, (double)kvp.Value);

                            else if (piType == typeof(Guid).Name)
                                pi.SetValue(userObject, Guid.Parse((string)kvp.Value));

                            else if (piType == typeof(string).Name)
                                pi.SetValue(userObject, kvp.Value.ToString());

                            else if (piType == typeof(decimal).Name)    // Can be either text or double. Double will lose precision
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, Decimal.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, (decimal)(long)kvp.Value);   // Will lose precision.

                                else if (typeName == typeof(double).Name)
                                    pi.SetValue(userObject, (decimal)(double)kvp.Value);   // Will lose precision.
                            }

                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(userObject, true);
                                else
                                    pi.SetValue(userObject, false);
                            }

                            else if (piType == typeof(DateTime).Name)  // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, new DateTime((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, DateTime.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateTimeOffset).Name)  // Can be either text or DATETIMEOFFSET.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, DateTimeOffset.FromUnixTimeMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, DateTimeOffset.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(TimeSpan).Name)  // Can be either text or TIMESPAN.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, TimeSpan.FromTicks((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, TimeSpan.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateOnly).Name)  // Must be text.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, DateOnly.FromDayNumber((int)(long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, DateOnly.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(int).Name)
                                    pi.SetValue(userObject, DateOnly.FromDayNumber((int)kvp.Value));
                            }

                            else if (piType == typeof(TimeOnly).Name)    // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, new TimeOnly((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, TimeOnly.Parse(kvp.Value.ToString()!));
                            }

                            else
                            {
                                pi.SetValue(userObject, kvp.Value);
                            }

                        }
                        else
                            pi.SetValue(userObject, default);
                    }
                }
                catch (System.ArgumentException)
                {
                    string? userPropertyType = userObject.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString();
                    string? databasePropertyType = kvp.Value?.GetType().ToString();
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", kvp.Key, databasePropertyType, userObject.GetType().ToString() + "." + kvp.Key, userPropertyType));
                }
            }
        }

        // Data from the user supplied object loaded into a dictionary that is then to be written to the database.
        internal static Dictionary<string, object?> loadParamaterValues<T>(Dictionary<string, string> dbColumnNameType, T userObject) where T : class, new()
        {
            Dictionary<string, object?> returnDictionary = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, string> kvp in dbColumnNameType)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    string columnName = kvp.Key;
                    PropertyInfo? userObjectPI = userObject.GetType().GetProperty(columnName);  // Get the property from the user supplied object that matches the column anme in the database.

                    if (userObjectPI != default)  // If the column is in the user supplied object.
                    {
                        object? userSuppliedObjectData = userObjectPI.GetValue(userObject);
                        if (userSuppliedObjectData != null)  // If the value of the data for the column in the user supplied object is not null;
                        {
                            string userObjectType = userObjectPI.PropertyType.Name;  // Get the data type of the column in the user supplied object.
                            Type? underlyingType = Nullable.GetUnderlyingType(userObjectPI.PropertyType);
                            if (underlyingType != null)
                            {
                                userObjectType = underlyingType.Name;
                            }

                            if (userObjectType == typeof(DateTime).Name)  // Is the data type for the column in the user object a DateTime?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).ToString("o"));

                                else if (kvp.Value.ToLower().Equals("datetime"))
                                    returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).Ticks);
                            }

                            else if (userObjectType == typeof(DateOnly).Name)  // Is the data type for the column in the user object a DateOnly?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((DateOnly)userSuppliedObjectData).ToString("o"));

                                else if (kvp.Value.ToLower().Equals("dateonly"))
                                    returnDictionary.Add(columnName, ((DateOnly)userSuppliedObjectData).DayNumber);
                            }

                            else if (userObjectType == typeof(decimal).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToLower().Equals("decimal"))
                                    returnDictionary.Add(columnName, ((decimal)userSuppliedObjectData));  // Will lose precision. Converts to a numeric which is either a double or a long.

                                else if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((decimal)userSuppliedObjectData).ToString());
                            }

                            else if (userObjectType == typeof(ulong).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToLower().Equals("ulong"))
                                    returnDictionary.Add(columnName, ((ulong)userSuppliedObjectData));  // Will lose precision. Converts to a numeric which is either a double or a long.

                                else if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((ulong)userSuppliedObjectData).ToString());
                            }

                            else if (userObjectType == typeof(DateTimeOffset).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((DateTimeOffset)userSuppliedObjectData).ToString("o"));

                                else if (kvp.Value.ToLower().Equals("datetimeoffset"))
                                    returnDictionary.Add(columnName, ((DateTimeOffset)userSuppliedObjectData).ToUnixTimeMilliseconds());
                            }

                            else if (userObjectType == typeof(TimeSpan).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((TimeSpan)userSuppliedObjectData).ToString());

                                else if (kvp.Value.ToLower().Equals("timespan"))
                                    returnDictionary.Add(columnName, ((TimeSpan)userSuppliedObjectData).Ticks);
                            }

                            else if (userObjectType == typeof(TimeOnly).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((TimeOnly)userSuppliedObjectData).ToString("o"));

                                else if (kvp.Value.ToLower().Equals("timeonly"))
                                    returnDictionary.Add(columnName, ((TimeOnly)userSuppliedObjectData).Ticks);
                            }

                            else
                            {
                                returnDictionary.Add(columnName, userObjectPI.GetValue(userObject));
                            }
                        }
                        else
                            returnDictionary.Add(columnName, DBNull.Value);
                    }
                }
                catch (System.ArgumentException)
                {
                    throw;
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
            return returnDictionary;
        }
    }
}
