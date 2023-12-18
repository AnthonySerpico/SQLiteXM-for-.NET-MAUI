using System.Reflection;



//using static CoreFoundation.DispatchSource;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    // A Transaction object represents a series of SQL statements that will be executes as a single transaction.
    public class SxmHelpers
    {
        private SxmHelpers() { }

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

            if(sqlStatementName.StartsWith("SELECT ", true, null))
                return SqlStatementType.selectDirect;

            if (sqlStatementName.StartsWith("DELETE ", true, null))
                return SqlStatementType.deleteDirect;

            if (sqlStatementName.StartsWith("UPDATE ", true, null))
                return SqlStatementType.updateDirect;

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName));
        }

        public static List<T> populateUserRecord<T>(List<Dictionary<string, object?>> databaseRowsList) where T : class, new()
        {
            List<T> userObjectList = new List<T>();

            foreach (Dictionary<string, object?> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                T userObject = loadDbValues<T>(databaseRecord);
                userObjectList.Add(userObject);
            }

            return userObjectList;
        }

        // Data being loaded into a user object; usualy after a select.
        public static T loadDbValues<T>(Dictionary<string, object?> databaseRecord) where T : class, new()
        {
            T userObject = new T();
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

                            if (piType == typeof(int).Name)
                                pi.SetValue(userObject, (int)(long)kvp.Value);
                            else if (piType == typeof(long).Name)
                                pi.SetValue(userObject, (long)kvp.Value);
                            else if (piType == typeof(float).Name)
                                pi.SetValue(userObject, (float)(double)kvp.Value);
                            else if (piType == typeof(double).Name)
                                pi.SetValue(userObject, (double)kvp.Value);
                            else if (piType == typeof(decimal).Name)
                                pi.SetValue(userObject, (decimal)(double)kvp.Value);
                            else if (piType == typeof(string).Name)
                                pi.SetValue(userObject, kvp.Value.ToString());
                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(userObject, true);
                                else
                                    pi.SetValue(userObject, false);
                            }
                            else if (piType == typeof(DateTime).Name)
                            {
                                if (kvp.Value.GetType().Name == typeof(string).Name)
                                    pi.SetValue(userObject, DateTime.Parse(kvp.Value.ToString()!));
                                if (kvp.Value.GetType().Name == typeof(double).Name)
                                    pi.SetValue(userObject, new DateTime((long)(double)kvp.Value));
                            }
                            else
                                pi.SetValue(userObject, kvp.Value);

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
            return userObject;
        }

        // Data from the user supplied object loaded into a dictionary that is then to be written to the database.
        public static Dictionary<string, object?> loadParamaterValues<T>(Dictionary<string, string> dbColumnNameType, T userObject) where T : class, new()
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

                            if (userObjectType == typeof(DateTime).Name)  // Is the data type for the column in the user object a DateTime?
                            {
                                if (kvp.Value.ToLower().Equals("text"))
                                    returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).ToString("o"));
                                else
                                {
                                    if (kvp.Value.ToLower().Equals("double"))
                                        returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).Ticks);
                                }
                            }
                            else
                                returnDictionary.Add(columnName, userObjectPI.GetValue(userObject));
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
