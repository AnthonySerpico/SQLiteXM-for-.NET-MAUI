namespace SQLiteXM
{
    /// <summary>
    /// Internal entity that backs the SxmStore key/value storage system.
    /// This entity is automatically registered and its table (__sxm_store__) is created
    /// during SxmDatabase initialization.
    /// </summary>
    [Table(IsColumnAttributeRequired = false)]
    internal class __sxm_store__ : SxmEntity
    {
        /// <summary>
        /// The unique key for this key/value pair.
        /// </summary>
        [UniqueIndex]
        public string? Key { get; set; }

        /// <summary>
        /// The value stored as a string representation.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// The CLR type name of the original value before it was converted to string.
        /// Used to restore the proper type when retrieving the value.
        /// </summary>
        public string? CLR_Type { get; set; }
    }
}
