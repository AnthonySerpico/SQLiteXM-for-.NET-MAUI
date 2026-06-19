using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Class-level table attribute (alias to <c>LinqToDB.Mapping.TableAttribute</c>).
    /// Apply to a class to specify the mapped database table name.
    /// </summary>
    /// <remarks>
    /// The <see cref="LinqToDB.Mapping.TableAttribute.Database"/> property specifies which database 
    /// this entity belongs to in a multi-database configuration.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : LinqToDB.Mapping.TableAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TableAttribute"/> class.
        /// Uses the default table name resolution from LinqToDB.
        /// </summary>
        public TableAttribute() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableAttribute"/> class
        /// and specifies the mapped table name.
        /// </summary>
        /// <param name="tableName">The database table name to map the class to.</param>
        public TableAttribute(string tableName) : base(tableName) { }
    }

    /// <summary>
    /// Property/field column attribute (alias to <c>LinqToDB.Mapping.ColumnAttribute</c>).
    /// Use to control column mapping, including an optionally consumer-facing <see cref="DataType"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ColumnAttribute : LinqToDB.Mapping.ColumnAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnAttribute"/> class.
        /// </summary>
        public ColumnAttribute() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnAttribute"/> class
        /// and specifies the column name.
        /// </summary>
        /// <param name="name">The name of the column in the database.</param>
        public ColumnAttribute(string name) : base(name) { }

        /// <summary>
        /// Consumer-facing DataType that maps to <c>LinqToDB.DataType</c> by name.
        /// Example usage in user code:
        /// <code>[Column(DataType = SQLiteXM.DataType.Long)]</code>
        /// </summary>
        /// <remarks>
        /// This property reads/writes the underlying LinqToDB.DataType via name-based mapping.
        /// If mapping fails, the property falls back to <see cref="DataType.Default"/>.
        /// </remarks>
        public new DataType DataType
        {
            get
            {
                try
                {
                    string baseName = base.DataType.ToString();
                    if (Enum.TryParse<DataType>(baseName, out SQLiteXM.DataType dt))
                        return dt;
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    SxmLogging.Log(ex, $"Data type mapping failure. DataType '{base.DataType.ToString()}'.");
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    SxmLogging.Log(ex);
                    throw ExceptionHelper.Wrap(ex, $"Data type mapping failure. DataType '{base.DataType.ToString()}'.");
                }
                return DataType.Default;
            }
            set
            {
                try
                {
                    // fallback: if name mismatches, default to base default.
                    base.DataType = default;

                    // map by name to LinqToDB.DataType
                    if (Enum.TryParse(typeof(LinqToDB.DataType), value.ToString(), ignoreCase: false, out var baseDt))
                    {
                        base.DataType = (LinqToDB.DataType)baseDt!;
                        return;
                    }
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    SxmLogging.Log(ex, $"Data type mapping failed for datatype '{typeof(LinqToDB.DataType)}'.");
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    SxmLogging.Log(ex);
                    throw ExceptionHelper.Wrap(ex, $"Data type mapping failed for datatype '{typeof(LinqToDB.DataType)}'.");
                }
            }
        }
    }

    /// <summary>
    /// Property/field NotColumn attribute (alias to <c>LinqToDB.Mapping.NotColumnAttribute</c>).
    /// Apply to members that should not be mapped to a database column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class NotColumnAttribute : LinqToDB.Mapping.NotColumnAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotColumnAttribute"/> class.
        /// </summary>
        public NotColumnAttribute() : base() { }
    }

    /// <summary>
    /// Indicates that this property was previously named differently in the database.
    /// The column will be renamed during schema migration to preserve existing data.
    /// </summary>
    /// <remarks>
    /// <para><strong>Migration Behavior:</strong></para>
    /// <list type="bullet">
    ///   <item><description>If any old column name exists in the database, it will be renamed (data preserved).</description></item>
    ///   <item><description>If no old column exists, a new column is created (fresh install scenario).</description></item>
    ///   <item><description>The old property must be removed from the entity class (validation enforced at registration).</description></item>
    ///   <item><description>Multiple renames are tried in reverse order (newest to oldest).</description></item>
    /// </list>
    /// 
    /// <para><strong>Single-Step Rename Example:</strong></para>
    /// <code>
    /// // Version 1
    /// public string Title { get; set; }
    /// 
    /// // Version 2 - Rename "Title" to "Name"
    /// [Rename("Title")]
    /// public string Name { get; set; }  // ← Remove "Title" property!
    /// </code>
    /// 
    /// <para><strong>Multi-Step Rename Example:</strong></para>
    /// <code>
    /// // Version 1
    /// public string Title { get; set; }
    /// 
    /// // Version 2
    /// [Rename("Title")]
    /// public string Name { get; set; }
    /// 
    /// // Version 3 - Track full history
    /// [Rename("Title", "Name")]
    /// public string ProductName { get; set; }  // ← Remove "Name" property!
    /// </code>
    /// 
    /// <para><strong>Fresh Install:</strong> If a user installs Version 3 directly (never had Version 1 or 2),
    /// the column is created as "ProductName" with no rename needed.</para>
    /// 
    /// <para><strong>Skipped Versions:</strong> If a user updates from Version 1 directly to Version 3,
    /// the migration finds "Title" column and renames it directly to "ProductName".</para>
    /// 
    /// <para><strong>Important:</strong> The old property must be completely removed from the entity class.
    /// If both old and new properties exist, schema registration will fail with a clear validation error.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RenameAttribute : Attribute
    {
        /// <summary>
        /// The previous name(s) of this column, in chronological order (oldest first).
        /// </summary>
        /// <remarks>
        /// When multiple names are provided, SQLiteXM will search for them in reverse order (newest to oldest)
        /// during migration. The first match found will be renamed to the current property name.
        /// </remarks>
        public string[] OldNames { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenameAttribute"/> class for a single-step rename.
        /// </summary>
        /// <param name="oldName">The previous name of this column in the database.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="oldName"/> is null or whitespace.</exception>
        /// <example>
        /// <code>
        /// [Rename("OldColumnName")]
        /// public string NewColumnName { get; set; }
        /// </code>
        /// </example>
        public RenameAttribute(string oldName)
        {
            if (string.IsNullOrWhiteSpace(oldName))
                throw new ArgumentException("Old column name cannot be null or empty.", nameof(oldName));

            OldNames = new[] { oldName };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenameAttribute"/> class for multi-step renames.
        /// </summary>
        /// <param name="oldNames">
        /// The previous names of this column in chronological order (oldest to newest).
        /// Example: If column was "A" → "B" → "C", pass ("A", "B") for property named "C".
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="oldNames"/> is null, empty, or contains null/whitespace entries.
        /// </exception>
        /// <example>
        /// <code>
        /// // Column history: "OriginalName" → "MiddleName" → "FinalName"
        /// [Rename("OriginalName", "MiddleName")]
        /// public string FinalName { get; set; }
        /// </code>
        /// </example>
        public RenameAttribute(params string[] oldNames)
        {
            if (oldNames == null || oldNames.Length == 0)
                throw new ArgumentException("At least one old name must be provided.", nameof(oldNames));

            if (oldNames.Any(name => string.IsNullOrWhiteSpace(name)))
                throw new ArgumentException("Old column names cannot be null or empty.", nameof(oldNames));

            OldNames = oldNames;
        }
    }

    /// <summary>
    /// Interface describing index-related attribute data.
    /// </summary>
    interface IIndexProperties
    {
        /// <summary>
        /// The fields that participate in the index.
        /// </summary>
        public string[] IndexFields { get; set; }

        /// <summary>
        /// The name of the index.
        /// </summary>
        public string IndexName { get; set; }
    }

    /// <summary>
    /// Helper class used to construct index attributes programmatically.
    /// Not an attribute itself; used to build index names and field lists.
    /// </summary>
    public class IndexProperties : IIndexProperties
    {
        /// <inheritdoc/>
        public string[] IndexFields { get; set; }

        /// <inheritdoc/>
        public string IndexName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexProperties"/> class
        /// for a single-field index and builds a conventional index name using the table name.
        /// </summary>
        /// <param name="indexField">The indexed field name.</param>
        /// <param name="tableName">The table name used to derive the index name.</param>
        public IndexProperties(string indexField, string tableName)
        {
            this.IndexFields = new string[] { indexField };

            this.IndexName = "IDX_" + tableName;
            foreach (string field in this.IndexFields)
            {
                this.IndexName += "_" + field;
            }
        }
    }

    /// <summary>
    /// Attribute used to request creation of a non-unique database index.
    /// Can be applied at class-level (specify fields) or member-level (no args).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class IndexAttribute : Attribute, IIndexProperties
    {
        /// <inheritdoc/>
        public string[] IndexFields { get; set; } = Array.Empty<string>();

        /// <inheritdoc/>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexAttribute"/> attribute.
        /// </summary>
        public IndexAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexAttribute"/> attribute for a single-field index.
        /// </summary>
        /// <param name="indexField">The single field to include in the index.</param>
        public IndexAttribute(string indexField)
        {
            IndexFields = new[] { indexField };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexAttribute"/> attribute for a multi-field index.
        /// </summary>
        /// <param name="indexFields">The fields to include in the index.</param>
        public IndexAttribute(params string[] indexFields)
        {
            this.IndexFields = indexFields ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Attribute used to request creation of a unique database index.
    /// Can be applied at class-level (specify fields) or member-level (no args).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class UniqueIndexAttribute : Attribute, IIndexProperties
    {
        /// <inheritdoc/>
        public string[] IndexFields { get; set; } = Array.Empty<string>();

        /// <inheritdoc/>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueIndexAttribute"/> attribute.
        /// </summary>
        public UniqueIndexAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueIndexAttribute"/> attribute for a single-field unique index.
        /// </summary>
        /// <param name="indexField">The single field to include in the unique index.</param>
        public UniqueIndexAttribute(string indexField)
        {
            IndexFields = new[] { indexField };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueIndexAttribute"/> attribute for a multi-field unique index.
        /// </summary>
        /// <param name="indexFields">The fields to include in the unique index.</param>
        public UniqueIndexAttribute(params string[] indexFields)
        {
            this.IndexFields = indexFields ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Attribute that contains SQL for creating a trigger.
    /// Apply to a class to include trigger creation SQL during initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class TriggerAttribute : Attribute
    {
        /// <summary>
        /// The SQL text used to create the trigger.
        /// </summary>
        public string TriggerSql { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerAttribute"/> attribute.
        /// </summary>
        /// <param name="triggerSql">The CREATE TRIGGER SQL script to associate with the class.</param>
        public TriggerAttribute(string triggerSql)
        {
            this.TriggerSql = triggerSql;
        }
    }

    /// <summary>
    /// Attribute indicating that a member must not be null and provides a default value.
    /// Used to enforce non-null defaults at schema generation or validation time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class RequiredNotNullAttribute : Attribute
    {
        /// <summary>
        /// The default value to use when the member is required and cannot be null.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequiredNotNullAttribute"/> attribute.
        /// Throws <see cref="ArgumentNullException"/> if provided default value is null.
        /// </summary>
        /// <param name="defaultValue">The non-null default value to assign to the member.</param>
        public RequiredNotNullAttribute(object defaultValue)
        {
            if (defaultValue == null)
                throw new ArgumentNullException("RequiredNotNull", "For fields with the attribute 'RequiredNotNull', the default value for the field cannot be null.");

            this.DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// Defines the action to take when a referenced row in the parent table is deleted.
    /// </summary>
    public enum ForeignKeyDeleteAction
    {
        /// <summary>
        /// No explicit action specified. SQLite will use RESTRICT behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Automatically delete child records when parent is deleted.
        /// </summary>
        Cascade = 1,

        /// <summary>
        /// Set the foreign key column to NULL when parent is deleted.
        /// </summary>
        SetNull = 2,

        /// <summary>
        /// Set the foreign key column to its default value when parent is deleted.
        /// </summary>
        SetDefault = 3,

        /// <summary>
        /// Prevent deletion of parent if child records exist (SQLite default).
        /// </summary>
        Restrict = 4,

        /// <summary>
        /// No action taken (deferred constraint check).
        /// </summary>
        NoAction = 5
    }

    /// <summary>
    /// Internal representation of a foreign-key relationship for attribute processing.
    /// </summary>
    internal class ForeignKeyFields
    {
        /// <summary>
        /// The local field name participating in the foreign key.
        /// </summary>
        public string? fieldName { get; set; }

        /// <summary>
        /// The referenced foreign table name.
        /// </summary>
        public string? ForeignTable { get; set; }

        /// <summary>
        /// The action to take when the referenced row is deleted.
        /// </summary>
        public ForeignKeyDeleteAction OnDelete { get; set; } = ForeignKeyDeleteAction.None;
    }

    /// <summary>
    /// Attribute to request creation of a foreign key constraint referencing another table.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// The referenced foreign table name.
        /// </summary>
        public string ForeignTable { get; set; }

        /// <summary>
        /// The action to take when the referenced row is deleted.
        /// </summary>
        public ForeignKeyDeleteAction OnDelete { get; set; } = ForeignKeyDeleteAction.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForeignKeyAttribute"/> attribute.
        /// </summary>
        /// <param name="foreignTable">The foreign table name to reference in the constraint.</param>
        public ForeignKeyAttribute(string foreignTable)
        {
            this.ForeignTable = foreignTable;
        }
    }

    /// <summary>
    /// Internal container type for grouping SXM attribute helpers (reserved for future use).
    /// </summary>
    internal class SxmAttributes
    {
    }

    /// <summary>
    /// Internal helper that pairs a <see cref="MemberInfo"/> with an alias string.
    /// Used when mapping or projecting members with user-defined aliases.
    /// </summary>
    internal class MemberInfoWithAlias
    {
        /// <summary>
        /// The reflected member info (property/field/method).
        /// </summary>
        public MemberInfo MemberInfo { get; set; }

        /// <summary>
        /// The alias associated with the member.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberInfoWithAlias"/> class.
        /// </summary>
        /// <param name="propertyInfo">The reflected member info.</param>
        /// <param name="alias">The alias to associate with the member.</param>
        internal MemberInfoWithAlias(MemberInfo propertyInfo, string alias)
        {
            this.MemberInfo = propertyInfo;
            this.Alias = alias;
        }
    }
}