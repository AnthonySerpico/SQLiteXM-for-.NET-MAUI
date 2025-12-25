namespace SQLiteXM
{
    /// <summary>
    /// Represents the result of an insert operation, including the assigned record identifier
    /// and the synchronization identifier.
    /// </summary>
    public class SxmInsertResponse
    {
        private long recordID;

        /// <summary>
        /// Gets the database-assigned record identifier.
        /// </summary>
        public long RecordID
        {
            get { return recordID; }
        }

        private string synchID;

        /// <summary>
        /// Gets the synchronization identifier associated with the inserted record.
        /// </summary>
        public string SynchID
        {
            get { return synchID; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmInsertResponse"/> class.
        /// </summary>
        /// <param name="recordID">The database-assigned record identifier.</param>
        /// <param name="synchID">The synchronization identifier for the record.</param>
        internal SxmInsertResponse(long recordID, string synchID)
        {
            this.recordID = recordID;
            this.synchID = synchID;
        }
    }
}