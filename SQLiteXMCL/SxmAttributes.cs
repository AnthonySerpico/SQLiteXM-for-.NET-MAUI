using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    interface IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public static string? tableName { get; set; }
    }

    public class IndexPropertyAttributes : IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public string? tableName { get; set; } // set by the consumer

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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class CreateIndex : Attribute, IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public static string? tableName { get; set; } // set by the consumer

        public CreateIndex(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDX_" + tableName;
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateIndex(string indexField)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX_" + tableName;
            foreach (string field in this.indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateIndex()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class CreateUniqueIndex : Attribute, IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public static string? tableName { get; set; } // set by the consumer   

        public CreateUniqueIndex(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDX_" + tableName;
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateUniqueIndex()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ObservablePropertyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class IsAColumnAttribute : Attribute
    {
        public ColumnType ColumnType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class NotAColumnAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public bool ColumnAttributeRequired { get; set; } = false;

        public TableAttribute()
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

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
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
}
