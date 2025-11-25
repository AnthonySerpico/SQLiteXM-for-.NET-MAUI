using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using System;

//using static CoreFoundation.DispatchSource;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    // A Transaction object represents a series of SQL statements that will be executes as a single transaction.
    public class SxmHelpers
    {
        private static ISet<string> _registeredAssociations = new HashSet<string>();
        private SxmHelpers() { }

        internal static List<string> getAllUserTableNames(SxmConnection? sxmConnection)
        {
            List<string> tableNames = new List<string>();

            if (sxmConnection != null)
            {
                sxmConnection.executeQuery("SELECT tableName FROM _systemCloudSynchDescriptor", null as List<object>);

                if (sxmConnection.hasRows() == true)
                {
                    string[] fieldNames = sxmConnection.getFieldNames();
                    while (sxmConnection.nextRow() == true)
                    {
                        foreach (string fieldName in fieldNames)
                            tableNames.Add(sxmConnection.getValue(fieldName).ToString());
                    }
                }
            }

            return tableNames;
        }

        internal static void CreateAssociation(Type sourceType, string sourceKey, string targetTableName)
        {

            // Attempt to wire an association if a navigation property exists.
            // Conditions:
            // 1. Navigation property must have PropertyType.Name == fk.foreignTable
            // 2. It must be excluded from column mapping (NotColumn) so schema builder ignores it.
            // 3. Avoid duplicate registration per (SourceType.PropertyName)
            try
            {
                // Find a single navigation property whose CLR type name matches the foreign table name.
                var navProp = sourceType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.PropertyType.Name.Equals(targetTableName, StringComparison.Ordinal) && p.IsDefined(typeof(LinqToDB.Mapping.NotColumnAttribute), false));

                if (navProp != null &&
                    typeof(SxmEntity).IsAssignableFrom(navProp.PropertyType))
                {
                    string assocKey = $"{sourceType.FullName}.{navProp.Name}";
                    if (_registeredAssociations.Add(assocKey))
                    {
                        // Register runtime association: Source.FK -> Target.id
                        SxmMapping.ConfigureAssociation(
                            sourceType: sourceType,
                            navigationPropertyName: navProp.Name,
                            thisKey: sourceKey,
                            canBeNull: true);
                    }
                }
                else
                {
                    // OPTIONAL: You can log or ignore if no navigation property is found.
                    // This means only the physical FK will exist; navigation-based LINQ won't.
                }
            }
            catch
            {
                // Swallow or log; association registration failure should not break table creation.
            }

        }

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

        // Data being loaded into a user entity; usualy after a select.
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
                                pi.SetValue(userObject, (Guid)Guid.Parse((string)kvp.Value));

                            else if (piType == typeof(string).Name)
                                pi.SetValue(userObject, kvp.Value.ToString());

                            else if (piType == typeof(decimal).Name)    // Large vlues will overflow if not text in DB.
                                pi.SetValue(userObject, Decimal.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture));

                            else if (piType == typeof(ulong).Name)    // Large values will overflow if not text in DB.
                                pi.SetValue(userObject, ulong.Parse((string)kvp.Value, CultureInfo.InvariantCulture));

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
                                    pi.SetValue(userObject, DateTime.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
                            }

                            else if (piType == typeof(DateTimeOffset).Name)  // Can be either text or DATETIMEOFFSET.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, DateTimeOffset.FromUnixTimeMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, DateTimeOffset.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
                            }

                            else if (piType == typeof(TimeSpan).Name)  // Can be either text or TIMESPAN.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, TimeSpan.FromTicks((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, TimeSpan.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture));
                            }

                            else if (piType == typeof(DateOnly).Name)  // Must be text.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, DateOnly.FromDayNumber((int)(long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, DateOnly.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture));
                            }

                            else if (piType == typeof(TimeOnly).Name)    // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, new TimeOnly((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, TimeOnly.Parse(kvp.Value.ToString()!, CultureInfo.InvariantCulture));
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

        // Data from the user entity loaded into a dictionary that is then to be written to the database.
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

                            if (userObjectType == typeof(decimal).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                returnDictionary.Add(columnName, ((decimal)userSuppliedObjectData).ToString(CultureInfo.InvariantCulture));
                            }

                            else if (userObjectType == typeof(ulong).Name)  // Is the data type for the column in the user object a ulong?
                            {
                                returnDictionary.Add(columnName, ((ulong)userSuppliedObjectData).ToString("D20", CultureInfo.InvariantCulture));
                            }

                            else if (userObjectType == typeof(DateTime).Name)  // Is the data type for the column in the user object a DateTime?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).ToString("o", CultureInfo.InvariantCulture));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, ((DateTime)userSuppliedObjectData).Ticks);
                            }

                            else if (userObjectType == typeof(DateOnly).Name)  // Is the data type for the column in the user object a DateOnly?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, ((DateOnly)userSuppliedObjectData).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, ((DateOnly)userSuppliedObjectData).DayNumber);
                            }

                            else if (userObjectType == typeof(DateTimeOffset).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, ((DateTimeOffset)userSuppliedObjectData).ToString("o", CultureInfo.InvariantCulture));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, ((DateTimeOffset)userSuppliedObjectData).ToUnixTimeMilliseconds());
                            }

                            else if (userObjectType == typeof(TimeSpan).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, ((TimeSpan)userSuppliedObjectData).ToString("c", CultureInfo.InvariantCulture));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, ((TimeSpan)userSuppliedObjectData).Ticks);
                            }

                            else if (userObjectType == typeof(TimeOnly).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, ((TimeOnly)userSuppliedObjectData).ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
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

    public static class MemberInfoExtensions
    {
        /// <summary>
        /// Gets the underlying Type of the member (e.g., the property type, field type, etc.).
        /// </summary>
        /// <param name="member">The MemberInfo instance.</param>
        /// <returns>The underlying Type of the member, or null if the type cannot be determined.</returns>
        public static Type GetMemberType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                default:
                    // Other member types like Constructor, TypeInfo, etc., don't have a single "type" property in this context.
                    return null;
            }
        }
    }
}
