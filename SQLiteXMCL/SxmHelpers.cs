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

        public static T loadDbValues<T>(Dictionary<string, object?> databaseRecord) where T : class, new()
        {
            T userObject = new T();
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    userObject.GetType().GetProperty(kvp.Key)?.SetValue(userObject, kvp.Value);
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

        public static Dictionary<string, object?> loadParamaterValues<T>(List<string> dbColumnNames, T userObject) where T : class, new()
        {
            Dictionary<string, object?> returnDictionary = new Dictionary<string, object?> ();
            foreach (string columnName in dbColumnNames)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = userObject.GetType().GetProperty(columnName);  // If the column is in the user supplied object.
                    if (pi != default)
                        returnDictionary.Add(columnName, pi.GetValue(userObject));
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
