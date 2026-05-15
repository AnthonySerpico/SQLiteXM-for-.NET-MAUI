using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SQLiteXM
{
    internal class SxmSerialization
    {
        // using System.Xml.Serialization;
        // XmlSerializer serializer = new XmlSerializer(typeof(Root));
        // using (StringReader reader = new StringReader(xml))
        // {
        //    var test = (Root)serializer.Deserialize(reader);
        // }

        /// <summary>
        /// Represents a table entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "table")]
        public class Table
        {

            /// <summary>
            /// The name of the table (as found in the source SQL definition).
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL statement text used to create or define the table.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents an alter (column) entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "alter")]
        public class Alter
        {

            /// <summary>
            /// The column name affected by the alter command.
            /// </summary>
            [XmlElement(ElementName = "ColumnName")]
            public string? ColumnName { get; set; }

            /// <summary>
            /// The table name associated with the alter command.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL alter statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents an index definition entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "index")]
        public class Index
        {

            /// <summary>
            /// The index name.
            /// </summary>
            [XmlElement(ElementName = "IndexName")]
            public string? IndexName { get; set; }

            /// <summary>
            /// The table name the index belongs to.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL index statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents an insert statement entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "insert")]
        public class Insert
        {

            /// <summary>
            /// Optional name for the insert statement.
            /// </summary>
            [XmlElement(ElementName = "StatementName")]
            public string? StatementName { get; set; }

            /// <summary>
            /// The table targeted by the insert.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL insert statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents a select statement entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "select")]
        public class Select
        {

            /// <summary>
            /// Optional name for the select statement.
            /// </summary>
            [XmlElement(ElementName = "StatementName")]
            public string? StatementName { get; set; }

            /// <summary>
            /// The table targeted by the select.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL select statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents an update statement entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "update")]
        public class Update
        {

            /// <summary>
            /// Optional name for the update statement.
            /// </summary>
            [XmlElement(ElementName = "StatementName")]
            public string? StatementName { get; set; }

            /// <summary>
            /// The table targeted by the update.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL update statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents a delete statement entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "delete")]
        public class Delete
        {

            /// <summary>
            /// Optional name for the delete statement.
            /// </summary>
            [XmlElement(ElementName = "StatementName")]
            public string? StatementName { get; set; }

            /// <summary>
            /// The table targeted by the delete.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// The SQL delete statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Represents a trigger entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "trigger")]
        public class Trigger
        {

            /// <summary>
            /// The trigger name.
            /// </summary>
            [XmlElement(ElementName = "TriggerName")]
            public string? TriggerName { get; set; }

            /// <summary>
            /// The SQL trigger statement text.
            /// </summary>
            [XmlElement(ElementName = "Statement")]
            public string? Statement { get; set; }
        }

        /// <summary>
        /// Root model for XML formatted SQL statements files.
        /// Maps top-level XML elements to strongly typed collections.
        /// </summary>
        [XmlRoot(ElementName = "rootxml")]
        public class RootXml
        {

            /// <summary>
            /// Database identifier/name included in the SQL statements file.
            /// </summary>
            [XmlElement(ElementName = "database")]
            public string? Database { get; set; }

            /// <summary>
            /// Is default database flag.
            /// </summary>
            [XmlElement(ElementName = "isDefault")]
            public bool IsDefault { get; set; }

            /// <summary>
            /// Version number of the SQL statements file format/content.
            /// </summary>
            [XmlElement(ElementName = "version")]
            public long Version { get; set; }

            /// <summary>
            /// Collection of table definitions.
            /// </summary>
            [XmlElement(ElementName = "table")]
            public List<Table>? Table { get; set; }

            /// <summary>
            /// Collection of alter definitions.
            /// </summary>
            [XmlElement(ElementName = "alter")]
            public List<Alter>? Alter { get; set; }

            /// <summary>
            /// Collection of index definitions.
            /// </summary>
            [XmlElement(ElementName = "index")]
            public List<Index>? Index { get; set; }

            /// <summary>
            /// Collection of insert statements.
            /// </summary>
            [XmlElement(ElementName = "insert")]
            public List<Insert>? Insert { get; set; }

            /// <summary>
            /// Collection of select statements.
            /// </summary>
            [XmlElement(ElementName = "select")]
            public List<Select>? Select { get; set; }

            /// <summary>
            /// Collection of update statements.
            /// </summary>
            [XmlElement(ElementName = "update")]
            public List<Update>? Update { get; set; }

            /// <summary>
            /// Collection of delete statements.
            /// </summary>
            [XmlElement(ElementName = "delete")]
            public List<Delete>? Delete { get; set; }

            /// <summary>
            /// Collection of trigger definitions.
            /// </summary>
            [XmlElement(ElementName = "trigger")]
            public List<Trigger>? Trigger { get; set; }
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        /// <summary>
        /// Root model for JSON formatted SQL statements files.
        /// Uses dictionaries for flexible JSON key names and to match expected payload structure.
        /// </summary>
        public class RootJson
        {
            /// <summary>
            /// Database identifier/name included in the SQL statements file.
            /// </summary>
            public string? database { get; set; }

            /// <summary>
            /// Default database flag.
            /// </summary>
            public bool isDefault { get; set; }

            /// <summary>
            /// Version number of the SQL statements file format/content.
            /// </summary>
            public long version { get; set; }

            /// <summary>
            /// Collections of statement entries represented as dictionaries keyed by column names.
            /// Expected keys differ slightly from XML variant (e.g. "Table Name" vs "TableName").
            /// </summary>
            public List<Dictionary<string, string>>? Table { get; set; }
            public List<Dictionary<string, string>>? Alter { get; set; }
            public List<Dictionary<string, string>>? Index { get; set; }
            public List<Dictionary<string, string>>? Insert { get; set; }
            public List<Dictionary<string, string>>? Select { get; set; }
            public List<Dictionary<string, string>>? Update { get; set; }
            public List<Dictionary<string, string>>? Delete { get; set; }
            public List<Dictionary<string, string>>? Trigger { get; set; }
        }

    }
}
