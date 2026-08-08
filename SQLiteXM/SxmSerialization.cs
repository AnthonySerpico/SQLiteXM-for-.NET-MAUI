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
        /// Represents an insert statement entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "insert")]
        internal class Insert
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
        internal class Select
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
        internal class Update
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
        internal class Delete
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
        /// Represents a database definition entry in the SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "Database")]
        internal class Database
        {
            /// <summary>
            /// The database name.
            /// </summary>
            [XmlElement(ElementName = "database")]
            public string? database { get; set; }

            /// <summary>
            /// Flag indicating if this is the default database.
            /// </summary>
            [XmlElement(ElementName = "isDefault")]
            public bool isDefault { get; set; }
        }

        /// <summary>
        /// Represents a trigger entry in an XML SQL statements file.
        /// </summary>
        [XmlRoot(ElementName = "trigger")]
        internal class Trigger
        {

            /// <summary>
            /// The database name where the trigger will be created.
            /// This field is required for all trigger definitions.
            /// </summary>
            [XmlElement(ElementName = "Database")]
            public string? Database { get; set; }

            /// <summary>
            /// The trigger name.
            /// </summary>
            [XmlElement(ElementName = "TableName")]
            public string? TableName { get; set; }

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
        internal class RootXml
        {

            /// <summary>
            /// Version number of the SQL statements file format/content.
            /// </summary>
            [XmlElement(ElementName = "version")]
            public long Version { get; set; }

            /// <summary>
            /// Collection of database definitions.
            /// </summary>
            [XmlElement(ElementName = "Database")]
            public List<Database>? Databases { get; set; }

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
        internal class RootJson
        {
            /// <summary>
            /// Version number of the SQL statements file format/content.
            /// </summary>
            public long version { get; set; }

            /// <summary>
            /// Collection of database definitions.
            /// </summary>
            public List<Dictionary<string, object>>? databases { get; set; }

            /// <summary>
            /// Collections of statement entries represented as dictionaries keyed by column names.
            /// Expected keys differ slightly from XML variant (e.g. "Table Name" vs "TableName").
            /// </summary>
            public List<Dictionary<string, string>>? Insert { get; set; }
            public List<Dictionary<string, string>>? Select { get; set; }
            public List<Dictionary<string, string>>? Update { get; set; }
            public List<Dictionary<string, string>>? Delete { get; set; }
            public List<Dictionary<string, string>>? Trigger { get; set; }
        }

    }
}
