namespace SQLiteXM
{
    public class SxmChangeSet
    {
        public List<SxmEntity> Inserts { get; } = new();
        public List<SxmEntity> Updates { get; } = new();
        public List<SxmEntity> Deletes { get; } = new();

        public bool IsEmpty =>
            Inserts.Count == 0 &&
            Updates.Count == 0 &&
            Deletes.Count == 0;

        public void Clear()
        {
            Inserts.Clear();
            Updates.Clear();
            Deletes.Clear();
        }
    }
}
