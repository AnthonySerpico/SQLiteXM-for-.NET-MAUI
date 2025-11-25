using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    interface IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
    }

    public class IndexPropertyAttributes : IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }

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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateIndex : Attribute, IIndexVars
    {
        // Implementing the interface
        public string[] indexFields { get; set; } = Array.Empty<string>();
        public string indexName { get; set; } = string.Empty;

        // Default ctor (no args => attribute on property/field; class-level use should provide field names)
        public CreateIndex()
        {
        }

        // Single-field index
        public CreateIndex(string indexField)
        {
            indexFields = new[] { indexField };
        }

        // Multi-field index
        public CreateIndex(params string[] indexFields)
        {
            this.indexFields = indexFields ?? Array.Empty<string>();
        }

        // Optional helper to set index name fluently via named argument:
        // [CreateIndex("col1", IndexName = "IX_Name")]
        public string IndexName
        {
            get => indexName;
            set => indexName = value ?? string.Empty;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateUniqueIndex : Attribute, IIndexVars
    {
        // Implementing the interface
        public string[] indexFields { get; set; } = Array.Empty<string>();
        public string indexName { get; set; } = string.Empty;

        // Default ctor (no args => attribute on property/field; class-level use should provide field names)
        public CreateUniqueIndex()
        {
        }

        // Single-field index
        public CreateUniqueIndex(string indexField)
        {
            indexFields = new[] { indexField };
        }

        // Multi-field index
        public CreateUniqueIndex(params string[] indexFields)
        {
            this.indexFields = indexFields ?? Array.Empty<string>();
        }

        // Optional helper to set index name fluently via named argument:
        // [CreateUniqueIndex("col1", IndexName = "IX_Name")]
        public string IndexName
        {
            get => indexName;
            set => indexName = value ?? string.Empty;
        }
    }


    // For testing only.
    /*[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ObservablePropertyAttribute : Attribute
    {
    }*/

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class IsAColumnAttribute : Attribute
    {
        public ColumnType ColumnType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class NotAColumnAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class IsTableAttribute : Attribute
    {
        public bool IsColumnAttributeRequired { get; set; } = false;

        public IsTableAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CreateTrigger : Attribute
    {
        public string triggerSql { get; set; }

        public CreateTrigger(string triggerSql)
        {
            this.triggerSql = triggerSql;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class RequiredNotNull : Attribute
    {
        public object defaultValue { get; set; }

        public RequiredNotNull(object DefaultValue)
        {
            this.defaultValue = DefaultValue;
            if (DefaultValue == null)
                throw new ArgumentNullException("RequiredNotNull", "For fields with the attribute 'RequiredNotNull', the default value for the field cannot be null.");
        }
    }

    internal class ForeignKeyAttributes
    {
        public string fieldName { get; set; }
        public string foreignTable { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class CreateForeignKey : Attribute
    {
        public string foreignTable { get; set; }

        public CreateForeignKey(string ForeignTable)
        {
            this.foreignTable = ForeignTable;
        }
    }

    internal class SxmAttributes
    {
    }

    internal class MemberInfoWithAlias
    {
        public MemberInfo memberInfo {  get; set; }
        public string alias { get; set; }

        internal MemberInfoWithAlias(MemberInfo propertyInfo, string alias)
        {
            this.memberInfo = propertyInfo;
            this.alias = alias; 
        }
    }
}
