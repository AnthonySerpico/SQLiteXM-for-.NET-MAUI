namespace SQLiteXM
{
    /// <summary>
    /// Represents the result of an insert operation, including the assigned record identifier
    /// and the synchronization identifier.
    /// </summary>
    public class SxmInsertResponse
    {
        private long _recordId;

        /// <summary>
        /// Gets the database-assigned record identifier.
        /// </summary>
        public long RecordId
        {
            get { return _recordId; }
        }

        private string _synchId;

        /// <summary>
        /// Gets the synchronization identifier associated with the inserted record.
        /// </summary>
        public string SynchId
        {
            get { return _synchId; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmInsertResponse"/> class.
        /// </summary>
        /// <param name="recordID">The database-assigned record identifier.</param>
        /// <param name="synchID">The synchronization identifier for the record.</param>
        internal SxmInsertResponse(long recordID, string synchID)
        {
            this._recordId = recordID;
            this._synchId = synchID;
        }
    }
}