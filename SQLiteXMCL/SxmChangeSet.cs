using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Enumerates the types of change operations that can be recorded in a change set.
    /// </summary>
    public enum ChangeType
    {
        /// <summary>
        /// A new entity will be inserted.
        /// </summary>
        Insert,

        /// <summary>
        /// An existing entity will be updated.
        /// </summary>
        Update,

        /// <summary>
        /// An existing entity will be deleted.
        /// </summary>
        Delete
    }

    /// <summary>
    /// Result information produced after attempting to apply a change action.
    /// </summary>
    public class ChangeResult
    {
        /// <summary>
        /// True when the operation completed successfully; false when it failed.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// When <see cref="Success"/> is false this may hold the exception that occurred.
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// For insert/update operations this may contain the database-assigned long id after the operation.
        /// </summary>
        public long? IdAfterOperation { get; set; }

        /// <summary>
        /// For entities that use GUID synchronization ids this may contain the GUID assigned or returned
        /// by the store after the operation.
        /// </summary>
        public Guid? SynchIdAfterOperation { get; set; }

        /// <summary>
        /// UTC timestamp when the result instance was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a single change (insert/update/delete) that has been recorded against an entity.
    /// </summary>
    public class ChangeAction
    {
        /// <summary>
        /// The entity the action targets. Never null.
        /// </summary>
        public SxmEntity Entity { get; }

        /// <summary>
        /// The type of change to apply to <see cref="Entity"/>.
        /// </summary>
        public ChangeType Type { get; set; }

        /// <summary>
        /// UTC timestamp when this action was created/recorded.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>
        /// Populated by <c>SubmitChanges</c> (or similar execution flow) with the result of applying the action.
        /// </summary>
        public ChangeResult? Result { get; internal set; }

        /// <summary>
        /// Create a new <see cref="ChangeAction"/> for the specified entity and change type.
        /// </summary>
        /// <param name="entity">The entity to change. Must not be null.</param>
        /// <param name="type">The type of change.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is null.</exception>
        public ChangeAction(SxmEntity entity, ChangeType type)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Type = type;
        }
    }

    /// <summary>
    /// An ordered collection of change actions recorded by the caller.
    /// </summary>
    /// <remarks>
    /// Actions are recorded exactly in the order they are added. No implicit merging or cancellation
    /// of previously recorded actions is performed here — ordering and semantics are preserved and
    /// should be honored by the executor (for example, a SubmitChanges implementation).
    /// </remarks>
    public class SxmChangeSet
    {
        private readonly List<ChangeAction> _actions = new();

        /// <summary>
        /// All recorded actions in the order they were added. Read-only wrapper of the internal list.
        /// </summary>
        public IReadOnlyList<ChangeAction> Actions => _actions.AsReadOnly();

        /// <summary>
        /// Enumerates entities scheduled for insert operations in insertion order.
        /// </summary>
        public IEnumerable<SxmEntity> Inserts => _actions.Where(a => a.Type == ChangeType.Insert).Select(a => a.Entity);

        /// <summary>
        /// Enumerates entities scheduled for update operations in the order their actions were recorded.
        /// </summary>
        public IEnumerable<SxmEntity> Updates => _actions.Where(a => a.Type == ChangeType.Update).Select(a => a.Entity);

        /// <summary>
        /// Enumerates entities scheduled for delete operations in the order their actions were recorded.
        /// </summary>
        public IEnumerable<SxmEntity> Deletes => _actions.Where(a => a.Type == ChangeType.Delete).Select(a => a.Entity);

        /// <summary>
        /// True when no actions have been recorded.
        /// </summary>
        public bool IsEmpty => _actions.Count == 0;

        /// <summary>
        /// Remove all recorded actions from the change set.
        /// </summary>
        public void Clear() => _actions.Clear();

        /// <summary>
        /// Record the action exactly as submitted by the user.
        /// </summary>
        /// <remarks>
        /// No merging/cancellation of prior actions is performed — operations will be executed
        /// in the exact order they were added when SubmitChanges runs.
        /// </remarks>
        /// <param name="entity">The entity that the change targets. Must not be null.</param>
        /// <param name="type">The type of change to record.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is null.</exception>
        public void Add(SxmEntity entity, ChangeType type)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _actions.Add(new ChangeAction(entity, type));
        }

        /// <summary>
        /// Returns a read-only snapshot of the internal ordered actions list.
        /// </summary>
        /// <returns>A read-only list that reflects the current ordering of recorded actions.</returns>
        public IReadOnlyList<ChangeAction> GetOrderedActions() => _actions.AsReadOnly();
    }
}