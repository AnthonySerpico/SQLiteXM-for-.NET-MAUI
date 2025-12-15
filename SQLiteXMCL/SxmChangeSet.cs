using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLiteXM
{
    public enum ChangeType
    {
        Insert,
        Update,
        Delete
    }

    public class ChangeResult
    {
        public bool Success { get; set; }
        public Exception? Error { get; set; }
        public long? IdAfterOperation { get; set; }
        public Guid? SynchIdAfterOperation { get; set; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }

    public class ChangeAction
    {
        public SxmEntity Entity { get; }
        public ChangeType Type { get; set; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        // Populated by SubmitChanges
        public ChangeResult? Result { get; internal set; }

        public ChangeAction(SxmEntity entity, ChangeType type)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Type = type;
        }
    }

    public class SxmChangeSet
    {
        private readonly List<ChangeAction> _actions = new();

        public IReadOnlyList<ChangeAction> Actions => _actions.AsReadOnly();

        public IEnumerable<SxmEntity> Inserts => _actions.Where(a => a.Type == ChangeType.Insert).Select(a => a.Entity);
        public IEnumerable<SxmEntity> Updates => _actions.Where(a => a.Type == ChangeType.Update).Select(a => a.Entity);
        public IEnumerable<SxmEntity> Deletes => _actions.Where(a => a.Type == ChangeType.Delete).Select(a => a.Entity);

        public bool IsEmpty => _actions.Count == 0;

        public void Clear() => _actions.Clear();

        /// <summary>
        /// Record the action exactly as submitted by the user.
        /// No merging/cancellation of prior actions is performed — operations will be executed
        /// in the exact order they were added when SubmitChanges runs.
        /// </summary>
        public void Add(SxmEntity entity, ChangeType type)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _actions.Add(new ChangeAction(entity, type));
        }

        public IReadOnlyList<ChangeAction> GetOrderedActions() => _actions.AsReadOnly();
    }
}