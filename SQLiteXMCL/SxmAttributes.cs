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
                    var baseName = base.DataType.ToString();
                    if (Enum.TryParse<DataType>(baseName, out var dt))
                        return dt;
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    SxmLogging.Log(ex);
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    SxmLogging.Log(ex);
                    throw ExceptionHelper.Wrap(ex, $"Data type mapping failed for datatype '{base.DataType.ToString()}'.");
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
                    SxmLogging.Log(ex);
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
    /// Interface describing index-related attribute data.
    /// </summary>
    interface IIndexVars
    {
        /// <summary>
        /// The fields that participate in the index.
        /// </summary>
        public string[] indexFields { get; set; }

        /// <summary>
        /// The name of the index.
        /// </summary>
        public string indexName { get; set; }
    }

    /// <summary>
    /// Helper class used to construct index attributes programmatically.
    /// Not an attribute itself; used to build index names and field lists.
    /// </summary>
    public class IndexPropertyAttributes : IIndexVars
    {
        /// <inheritdoc/>
        public string[] indexFields { get; set; }

        /// <inheritdoc/>
        public string indexName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexPropertyAttributes"/> class
        /// for a single-field index and builds a conventional index name using the table name.
        /// </summary>
        /// <param name="indexField">The indexed field name.</param>
        /// <param name="tableName">The table name used to derive the index name.</param>
        public IndexPropertyAttributes(string indexField, string tableName)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX_" + tableName;
            foreach (string field in this.indexFields)
            {
                this.indexName += "_" + field;
            }
        }
    }

    /// <summary>
    /// Attribute used to request creation of a non-unique database index.
    /// Can be applied at class-level (specify fields) or member-level (no args).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateIndex : Attribute, IIndexVars
    {
        /// <inheritdoc/>
        public string[] indexFields { get; set; } = Array.Empty<string>();

        /// <inheritdoc/>
        public string indexName { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateIndex"/> attribute.
        /// Use the parameterless ctor for member-level usage or named arguments to set <see cref="IndexName"/>.
        /// </summary>
        public CreateIndex()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateIndex"/> attribute for a single-field index.
        /// </summary>
        /// <param name="indexField">The single field to include in the index.</param>
        public CreateIndex(string indexField)
        {
            indexFields = new[] { indexField };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateIndex"/> attribute for a multi-field index.
        /// </summary>
        /// <param name="indexFields">The fields to include in the index.</param>
        public CreateIndex(params string[] indexFields)
        {
            this.indexFields = indexFields ?? Array.Empty<string>();
        }

        /// <summary>
        /// Optional helper to set index name fluently via named argument:
        /// Example: <c>[CreateIndex("col1", IndexName = "IX_Name")]</c>
        /// </summary>
        public string IndexName
        {
            get => indexName;
            set => indexName = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Attribute used to request creation of a unique database index.
    /// Can be applied at class-level (specify fields) or member-level (no args).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateUniqueIndex : Attribute, IIndexVars
    {
        /// <inheritdoc/>
        public string[] indexFields { get; set; } = Array.Empty<string>();

        /// <inheritdoc/>
        public string indexName { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUniqueIndex"/> attribute.
        /// </summary>
        public CreateUniqueIndex()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUniqueIndex"/> attribute for a single-field unique index.
        /// </summary>
        /// <param name="indexField">The single field to include in the unique index.</param>
        public CreateUniqueIndex(string indexField)
        {
            indexFields = new[] { indexField };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUniqueIndex"/> attribute for a multi-field unique index.
        /// </summary>
        /// <param name="indexFields">The fields to include in the unique index.</param>
        public CreateUniqueIndex(params string[] indexFields)
        {
            this.indexFields = indexFields ?? Array.Empty<string>();
        }

        /// <summary>
        /// Optional helper to set index name fluently via named argument:
        /// Example: <c>[CreateUniqueIndex("col1", IndexName = "IX_Name")]</c>
        /// </summary>
        public string IndexName
        {
            get => indexName;
            set => indexName = value ?? string.Empty;
        }
    }

    /// <summary>
    /// Attribute used to mark a member as a column (explicit intent).
    /// Provides an optional <see cref="ColumnType"/> to describe the column semantics.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class IsAColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the column type classification for this column.
        /// </summary>
        public ColumnType ColumnType { get; set; }
    }

    /// <summary>
    /// Attribute indicating a member should not be treated as a column by SQLiteXM logic.
    /// This is similar in intent to <see cref="NotColumnAttribute"/> but kept for explicit project-level tagging.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class NotAColumnAttribute : Attribute
    {
    }

    /// <summary>
    /// Class-level attribute denoting a type represents a mapped table in the system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IsTableAttribute : Attribute
    {
        /// <summary>
        /// When true, members require a column attribute to be considered for mapping.
        /// Default is false (members can be inferred).
        /// </summary>
        public bool IsColumnAttributeRequired { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="IsTableAttribute"/> class.
        /// </summary>
        public IsTableAttribute()
        {
        }
    }

    /// <summary>
    /// Attribute that contains SQL for creating a trigger.
    /// Apply to a class to include trigger creation SQL during initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CreateTrigger : Attribute
    {
        /// <summary>
        /// The SQL text used to create the trigger.
        /// </summary>
        public string triggerSql { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateTrigger"/> attribute.
        /// </summary>
        /// <param name="triggerSql">The CREATE TRIGGER SQL script to associate with the class.</param>
        public CreateTrigger(string triggerSql)
        {
            this.triggerSql = triggerSql;
        }
    }

    /// <summary>
    /// Attribute indicating that a member must not be null and provides a default value.
    /// Used to enforce non-null defaults at schema generation or validation time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class RequiredNotNull : Attribute
    {
        /// <summary>
        /// The default value to use when the member is required and cannot be null.
        /// </summary>
        public object defaultValue { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequiredNotNull"/> attribute.
        /// Throws <see cref="ArgumentNullException"/> if provided default value is null.
        /// </summary>
        /// <param name="DefaultValue">The non-null default value to assign to the member.</param>
        public RequiredNotNull(object DefaultValue)
        {
            this.defaultValue = DefaultValue;
            if (DefaultValue == null)
                throw new ArgumentNullException("RequiredNotNull", "For fields with the attribute 'RequiredNotNull', the default value for the field cannot be null.");
        }
    }

    /// <summary>
    /// Internal representation of a foreign-key relationship for attribute processing.
    /// </summary>
    internal class ForeignKeyAttributes
    {
        /// <summary>
        /// The local field name participating in the foreign key.
        /// </summary>
        public string fieldName { get; set; }

        /// <summary>
        /// The referenced foreign table name.
        /// </summary>
        public string foreignTable { get; set; }
    }

    /// <summary>
    /// Attribute to request creation of a foreign key constraint referencing another table.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateForeignKey : Attribute
    {
        /// <summary>
        /// The referenced foreign table name.
        /// </summary>
        public string foreignTable { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateForeignKey"/> attribute.
        /// </summary>
        /// <param name="ForeignTable">The foreign table name to reference in the constraint.</param>
        public CreateForeignKey(string ForeignTable)
        {
            this.foreignTable = ForeignTable;
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
        public MemberInfo memberInfo { get; set; }

        /// <summary>
        /// The alias associated with the member.
        /// </summary>
        public string alias { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberInfoWithAlias"/> class.
        /// </summary>
        /// <param name="propertyInfo">The reflected member info.</param>
        /// <param name="alias">The alias to associate with the member.</param>
        internal MemberInfoWithAlias(MemberInfo propertyInfo, string alias)
        {
            this.memberInfo = propertyInfo;
            this.alias = alias;
        }
    }
}