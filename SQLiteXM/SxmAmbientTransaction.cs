using System;
using System.Collections.Generic;
using System.Threading;

namespace SQLiteXM
{
    /// <summary>
    /// Ambient transaction holder that flows with <see cref="ExecutionContext"/> (async/await).
    /// Enforces strict LIFO semantics.
    /// </summary>
    internal static class SxmAmbientTransaction
    {
        /// <summary>
        /// Per-execution-context storage for the ambient transaction stack.
        /// Uses <see cref="AsyncLocal{T}"/> so the stack flows with async/await and logical execution context.
        /// Null when there are no ambient transactions.
        /// </summary>
        /// <remarks>
        /// This field is private to the type and is not synchronized. It is intended to be accessed
        /// only from code that observes logical execution context flow. Concurrent mutations from
        /// multiple threads that intentionally share the same logical context are not protected.
        /// </remarks>
        static private readonly AsyncLocal<Stack<SxmSqlTransaction>?> _slot = new();

        /// <summary>
        /// Gets the current (top-most) ambient <see cref="SxmSqlTransaction"/> or null when none exists.
        /// </summary>
        /// <value>The top-most ambient transaction, or <c>null</c> if no ambient transaction is present.</value>
        /// <remarks>
        /// The returned instance is the object reference stored on the top of the ambient stack.
        /// Callers must not assume thread-safety of the returned instance; this property simply
        /// exposes the current ambient token for the logical execution context.
        /// </remarks>
        internal static SxmSqlTransaction? Current => _slot.Value != null && _slot.Value.Count > 0 ? _slot.Value.Peek() : null;

        /// <summary>
        /// Pushes the supplied transaction onto the ambient transaction stack.
        /// </summary>
        /// <param name="transaction">The transaction to push onto the ambient stack. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="transaction"/> is null.</exception>
        /// <remarks>
        /// If there is no ambient stack for the current logical execution context, a new stack
        /// is created and assigned to the underlying <see cref="_slot"/>. This method preserves
        /// strict LIFO semantics — callers should ensure they call <see cref="Pop(SxmSqlTransaction)"/>
        /// or <see cref="TryRemove(SxmSqlTransaction)"/> to remove the pushed transaction when finished.
        /// </remarks>
        internal static void Push(SxmSqlTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var stack = _slot.Value;

            // Fail-fast: disallow nested ambient transactions
            if (stack != null && stack.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot create a nested ambient transaction. An ambient transaction is already active on this execution context. " +
                    "Complete or dispose the outer transaction first, or use explicit transaction parameters instead of ambient transactions.");
            }

            if (stack == null)
            {
                stack = new Stack<SxmSqlTransaction>();
                _slot.Value = stack;
            }
            stack.Push(transaction);
        }

        /// <summary>
        /// Pops the specified transaction from the ambient transaction stack.
        /// Enforces strict LIFO: the specified <paramref name="tx"/> must be the current (top) transaction.
        /// If the stack becomes empty after popping, the underlying AsyncLocal value is set to null.
        /// </summary>
        /// <param name="tx">The transaction expected to be at the top of the stack.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no ambient transaction to pop, or when the supplied transaction
        /// is not the top-most ambient transaction (disposed out of order).
        /// </exception>
        /// <remarks>
        /// This method enforces strict stack discipline. If callers dispose transactions out of order,
        /// prefer <see cref="TryRemove(SxmSqlTransaction)"/> which attempts a best-effort removal without throwing.
        /// </remarks>
        internal static void Pop(SxmSqlTransaction tx)
        {
            var stack = _slot.Value;
            if (stack == null || stack.Count == 0)
                throw new InvalidOperationException("No ambient transaction to pop.");

            if (!ReferenceEquals(stack.Peek(), tx))
                throw new InvalidOperationException("Ambient transaction disposed out of order.");

            stack.Pop();
            if (stack.Count == 0)
                _slot.Value = null;
        }


        /// <summary>
        /// Best-effort removal of a transaction from the ambient stack.
        /// Returns <c>true</c> if the transaction was found and removed; otherwise <c>false</c>.
        /// This method avoids throwing for common out-of-order disposal scenarios and attempts
        /// to repair the stack by rebuilding it without the target transaction.
        /// </summary>
        /// <param name="tx">The transaction to remove from the ambient stack. Must not be null.</param>
        /// <returns><c>true</c> if the transaction was removed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tx"/> is null.</exception>
        /// <remarks>
        /// The method first tries the fast path of popping the top item if it matches the target.
        /// If that fails or the target is not top-most, it performs a safe rebuild of the stack by
        /// popping all items into a temporary stack and reconstructing a new stack without the target.
        /// Any unexpected exception during rebuild is swallowed and <c>false</c> is returned to keep
        /// behavior conservative. This method mutates the ambient stack for the current logical context.
        /// </remarks>
        internal static bool TryRemove(SxmSqlTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            var stack = _slot.Value;
            if (stack == null || stack.Count == 0)
                return false;

            // If top matches, just pop using Pop (let it throw if something is really wrong).
            if (ReferenceEquals(stack.Peek(), tx))
            {
                try
                {
                    Pop(tx);
                    return true;
                }
                catch
                {
                    // fall through to best-effort path
                }
            }

            // Best-effort remove: rebuild stack without the target.
            try
            {
                var temp = new Stack<SxmSqlTransaction>();
                bool removed = false;
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    if (!removed && ReferenceEquals(item, tx))
                    {
                        removed = true;
                        continue; // drop this item
                    }
                    temp.Push(item);
                }

                var rebuilt = new Stack<SxmSqlTransaction>();
                while (temp.Count > 0)
                    rebuilt.Push(temp.Pop());

                _slot.Value = rebuilt.Count > 0 ? rebuilt : null;
                return removed;
            }
            catch
            {
                // Conservative: if anything goes wrong, do not propagate.
                return false;
            }
        }
    }
}