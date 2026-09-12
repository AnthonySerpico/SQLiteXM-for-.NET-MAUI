using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SQLiteXM
{
    internal partial class SxmSerialization
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

            /// <summary>
            /// Reads a <see cref="RootXml"/> from an <see cref="XmlReader"/> using LINQ to XML.
            /// This mirrors the element mapping declared by the <see cref="XmlElementAttribute"/>s above
            /// without using <see cref="XmlSerializer"/>, which is not trimming/AOT safe.
            /// </summary>
            /// <param name="reader">Reader positioned at the start of the document.</param>
            /// <returns>The populated <see cref="RootXml"/>.</returns>
            /// <exception cref="InvalidOperationException">The document root element is not <c>rootxml</c>.</exception>
            internal static RootXml ReadFrom(XmlReader reader)
            {
                XDocument doc = XDocument.Load(reader);
                XElement root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");

                if (!string.Equals(root.Name.LocalName, "rootxml", StringComparison.Ordinal))
                    throw new InvalidOperationException($"<{root.Name.LocalName} xmlns=''> was not expected. Expected root element 'rootxml'.");

                RootXml result = new RootXml();

                XElement? versionElement = root.Element("version");
                if (versionElement != null)
                    result.Version = XmlConvert.ToInt64(versionElement.Value.Trim());

                result.Databases = ReadList(root, "Database", e => new Database
                {
                    database = e.Element("database")?.Value,
                    isDefault = ReadBool(e.Element("isDefault"))
                });

                result.Insert = ReadList(root, "insert", e => new Insert
                {
                    StatementName = e.Element("StatementName")?.Value,
                    TableName = e.Element("TableName")?.Value,
                    Statement = e.Element("Statement")?.Value
                });

                result.Select = ReadList(root, "select", e => new Select
                {
                    StatementName = e.Element("StatementName")?.Value,
                    TableName = e.Element("TableName")?.Value,
                    Statement = e.Element("Statement")?.Value
                });

                result.Update = ReadList(root, "update", e => new Update
                {
                    StatementName = e.Element("StatementName")?.Value,
                    TableName = e.Element("TableName")?.Value,
                    Statement = e.Element("Statement")?.Value
                });

                result.Delete = ReadList(root, "delete", e => new Delete
                {
                    StatementName = e.Element("StatementName")?.Value,
                    TableName = e.Element("TableName")?.Value,
                    Statement = e.Element("Statement")?.Value
                });

                result.Trigger = ReadList(root, "trigger", e => new Trigger
                {
                    Database = e.Element("Database")?.Value,
                    TableName = e.Element("TableName")?.Value,
                    Statement = e.Element("Statement")?.Value
                });

                return result;
            }

            private static List<T>? ReadList<T>(XElement root, string elementName, Func<XElement, T> map)
            {
                List<T> items = root.Elements(elementName).Select(map).ToList();
                return items.Count == 0 ? null : items;
            }

            private static bool ReadBool(XElement? element)
            {
                if (element == null)
                    return false;
                return XmlConvert.ToBoolean(element.Value.Trim());
            }
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

        /// <summary>
        /// System.Text.Json source-generated context for the SQL statements file model.
        /// Using a generated context avoids reflection-based serialization, which is not trimming/AOT safe.
        /// </summary>
        [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
        [JsonSerializable(typeof(RootJson))]
        internal partial class SxmSqlStatementsJsonContext : JsonSerializerContext
        {
        }

    }
}
