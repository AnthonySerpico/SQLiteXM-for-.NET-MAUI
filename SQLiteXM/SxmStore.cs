using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Provides a simple key/value store for primitive CLR types, backed by SQLite.
    /// The store table is automatically created during database initialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Supported Types:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>string</description></item>
    ///   <item><description>bool</description></item>
    ///   <item><description>byte</description></item>
    ///   <item><description>short</description></item>
    ///   <item><description>int</description></item>
    ///   <item><description>long</description></item>
    ///   <item><description>float</description></item>
    ///   <item><description>double</description></item>
    ///   <item><description>decimal</description></item>
    ///   <item><description>char</description></item>
    /// </list>
    /// <para>
    /// <strong>Usage Examples:</strong>
    /// </para>
    /// <code>
    /// // Store values
    /// await SxmStore.PutAsync("theme", "dark");
    /// await SxmStore.PutAsync("fontSize", 14);
    /// await SxmStore.PutAsync("showTips", true);
    /// 
    /// // Retrieve values
    /// string theme = await SxmStore.GetAsync&lt;string&gt;("theme");
    /// int fontSize = await SxmStore.GetAsync&lt;int&gt;("fontSize");
    /// bool showTips = await SxmStore.GetAsync&lt;bool&gt;("showTips");
    /// 
    /// // Check type
    /// Type? storedType = await SxmStore.GetTypeAsync("fontSize"); // returns typeof(int)
    /// 
    /// // Remove value
    /// bool removed = await SxmStore.RemoveAsync("theme");
    /// </code>
    /// <para>
    /// <strong>Transaction Restrictions:</strong>
    /// SxmStore operations are only allowed outside of transactions or within the default database transaction.
    /// Attempting to use SxmStore inside a transaction for a named database (other than the default) will throw
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <strong>Complex Types:</strong>
    /// For complex types like DateTime, DateTimeOffset, etc., the application is responsible for converting
    /// to/from one of the supported primitive types. For example, convert DateTime to ticks (long) or ISO-8601 string.
    /// </para>
    /// </remarks>
    public static class SxmStore
    {
        /// <summary>
        /// Set of supported primitive CLR types for the key/value store.
        /// </summary>
        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(char)
        };

        /// <summary>
        /// Validates that the current execution context is allowed to use SxmStore.
        /// Throws if inside a transaction for a non-default database.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        private static void ValidateTransactionContext()
        {
            var ambient = SxmAmbientTransaction.Current;
            if (ambient?.Connection?.DatabaseName != null)
            {
                string? currentDbName = ambient.Connection.DatabaseName;
                string? defaultDbName = SxmDatabaseDescriptor.DefaultDatabase;

                // Block if this is a named database transaction AND it's not the default database
                if (!string.Equals(currentDbName, defaultDbName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"SxmStore cannot be used inside a transaction for database '{currentDbName}'. " +
                        $"SxmStore operations are only allowed outside transactions or within the default database transaction.");
                }
            }
        }

        /// <summary>
        /// Validates that the specified type is one of the supported primitive types.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <param name="parameterName">The parameter name for exception messages.</param>
        /// <exception cref="ArgumentException">Thrown when the type is not supported.</exception>
        private static void ValidateSupportedType(Type type, string parameterName)
        {
            if (!SupportedTypes.Contains(type))
            {
                throw new ArgumentException(
                    $"Type '{type.Name}' is not supported by SxmStore. " +
                    $"Supported types are: {string.Join(", ", SupportedTypes.Select(t => t.Name))}.",
                    parameterName);
            }
        }

        /// <summary>
        /// Converts a primitive value to its string representation for storage.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>String representation of the value.</returns>
        private static string? ConvertToString(object? value)
        {
            if (value == null)
                return null;

            return value switch
            {
                string s => s,
                bool b => b ? "1" : "0",
                byte bt => bt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                short sh => sh.ToString(System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                decimal dec => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
                char c => c.ToString(),
                _ => throw new InvalidOperationException($"Unsupported type: {value.GetType().Name}")
            };
        }

        /// <summary>
        /// Converts a stored string value back to its original CLR type.
        /// </summary>
        /// <param name="storedValue">The stored string value.</param>
        /// <param name="clrTypeName">The CLR type name.</param>
        /// <returns>The converted value.</returns>
        private static object? ConvertFromString(string? storedValue, string clrTypeName)
        {
            if (storedValue == null)
                return null;

            return clrTypeName switch
            {
                "String" => storedValue,
                "Boolean" => storedValue == "1",
                "Byte" => byte.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Int16" => short.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Int32" => int.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Int64" => long.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Single" => float.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Double" => double.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Decimal" => decimal.Parse(storedValue, System.Globalization.CultureInfo.InvariantCulture),
                "Char" => storedValue.Length > 0 ? storedValue[0] : '\0',
                _ => throw new InvalidOperationException($"Unsupported CLR type: {clrTypeName}")
            };
        }

        /// <summary>
        /// Stores or updates a key/value pair in the store.
        /// </summary>
        /// <typeparam name="T">The type of the value. Must be one of the supported primitive types.</typeparam>
        /// <param name="key">The unique key for this value.</param>
        /// <param name="value">The value to store.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when the value type is not supported.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// If the key already exists, its value will be updated (upsert behavior).
        /// This operation participates in the ambient transaction if one exists for the default database.
        /// </remarks>
        public static async Task PutAsync<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");

            ValidateTransactionContext();

            Type valueType = typeof(T);
            ValidateSupportedType(valueType, nameof(value));

            // Convert value to string for storage
            string? storedValue = ConvertToString(value);
            string clrTypeName = valueType.Name;

            // Create or update the entity
            var entity = new __sxm_store__
            {
                Key = key,
                Value = storedValue,
                CLR_Type = clrTypeName
            };

            await entity.SaveAsync().ConfigureFalse();
        }

        /// <summary>
        /// Retrieves a value from the store by its key.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to retrieve.</param>
        /// <returns>
        /// The value associated with the key, or default(T) if the key does not exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the stored type does not match the requested type T, or when called
        /// inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// If the key exists but the stored type doesn't match T, an exception is thrown.
        /// This helps catch type mismatches early rather than causing silent errors.
        /// </remarks>
        public static async Task<T?> GetAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");

            ValidateTransactionContext();

            __sxm_store__? entity;

            // Check if we're in a transaction context
            var ambient = SxmAmbientTransaction.Current;
            if (ambient != null)
            {
                // Use existing transaction
                using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }
            else
            {
                // Create a temporary transaction for the query
                await using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }

            if (entity == null)
                return default(T);

            // Validate that the stored type matches the requested type
            Type requestedType = typeof(T);
            string requestedTypeName = requestedType.Name;

            if (!string.Equals(entity.CLR_Type, requestedTypeName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Type mismatch for key '{key}': stored as '{entity.CLR_Type}' but requested as '{requestedTypeName}'.");
            }

            // Convert from string back to the original type
            object? converted = ConvertFromString(entity.Value, entity.CLR_Type);
            return (T?)converted;
        }

        /// <summary>
        /// Gets the CLR type of the value stored under the specified key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>
        /// The CLR Type of the stored value, or null if the key does not exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// This method is useful for inspecting stored values or handling dynamic scenarios
        /// where the type isn't known at compile time.
        /// </remarks>
        public static async Task<Type?> GetTypeAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");

            ValidateTransactionContext();

            __sxm_store__? entity;

            // Check if we're in a transaction context
            var ambient = SxmAmbientTransaction.Current;
            if (ambient != null)
            {
                // Use existing transaction
                using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }
            else
            {
                // Create a temporary transaction for the query
                await using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }

            if (entity == null)
                return null;

            // Map CLR type name back to Type
            return entity.CLR_Type switch
            {
                "String" => typeof(string),
                "Boolean" => typeof(bool),
                "Byte" => typeof(byte),
                "Int16" => typeof(short),
                "Int32" => typeof(int),
                "Int64" => typeof(long),
                "Single" => typeof(float),
                "Double" => typeof(double),
                "Decimal" => typeof(decimal),
                "Char" => typeof(char),
                _ => null
            };
        }

        /// <summary>
        /// Removes a key/value pair from the store.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>
        /// True if the key existed and was removed; false if the key did not exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// This operation participates in the ambient transaction if one exists for the default database.
        /// </remarks>
        public static async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");

            ValidateTransactionContext();

            __sxm_store__? entity;

            // Check if we're in a transaction context
            var ambient = SxmAmbientTransaction.Current;
            if (ambient != null)
            {
                // Use existing transaction
                using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }
            else
            {
                // Create a temporary transaction for the query
                await using var ctx = new SxmTransaction();
                entity = await ctx.GetTable<__sxm_store__>()
                    .FirstOrDefaultAsync(e => e.Key == key)
                    .ConfigureFalse();
            }

            if (entity == null)
                return false;

            // Delete the entity
            await entity.DeleteAsync().ConfigureFalse();
            return true;
        }

        /// <summary>
        /// Checks whether a key exists in the store.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>
        /// True if the key exists; false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// This is more efficient than calling GetTypeAsync if you only need to know whether a key exists.
        /// </remarks>
        public static async Task<bool> ContainsKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Key cannot be null or empty.");

            ValidateTransactionContext();

            bool exists;

            // Check if we're in a transaction context
            var ambient = SxmAmbientTransaction.Current;
            if (ambient != null)
            {
                // Use existing transaction
                using var ctx = new SxmTransaction();
                exists = await ctx.GetTable<__sxm_store__>()
                    .AnyAsync(e => e.Key == key)
                    .ConfigureFalse();
            }
            else
            {
                // Create a temporary transaction for the query
                await using var ctx = new SxmTransaction();
                exists = await ctx.GetTable<__sxm_store__>()
                    .AnyAsync(e => e.Key == key)
                    .ConfigureFalse();
            }

            return exists;
        }

        /// <summary>
        /// Removes all key/value pairs from the store.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called inside a transaction for a non-default database.
        /// </exception>
        /// <remarks>
        /// This operation is useful for testing or when you need to reset all settings.
        /// This operation participates in the ambient transaction if one exists for the default database.
        /// </remarks>
        public static async Task ClearAsync()
        {
            ValidateTransactionContext();

            // Check if we're in a transaction context
            var ambient = SxmAmbientTransaction.Current;
            if (ambient != null)
            {
                // Use existing transaction
                using var ctx = new SxmTransaction();
                await ctx.GetTable<__sxm_store__>()
                    .DeleteAsync()
                    .ConfigureFalse();
            }
            else
            {
                // Create a temporary transaction for the delete
                await using var ctx = new SxmTransaction();
                await ctx.GetTable<__sxm_store__>()
                    .DeleteAsync()
                    .ConfigureFalse();
            }
        }
    }
}
