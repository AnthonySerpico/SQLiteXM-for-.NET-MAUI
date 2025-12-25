using System;
using System.Collections.Generic;
using System.Threading;

namespace SQLiteXM
{
    /// <summary>
    /// Ambient transaction holder that flows with <see cref="ExecutionContext"/> (async/await).
    /// Enforces strict LIFO semantics.
    /// </summary>
    public static class SxmAmbientTransaction
    {
        /// <summary>
        /// Per-execution-context storage for the ambient transaction stack.
        /// Uses <see cref="AsyncLocal{T}"/> so the stack flows with async/await and logical execution context.
        /// Null when there are no ambient transactions.
        /// </summary>
        static readonly AsyncLocal<Stack<SxmTransaction>?> slot = new();

        /// <summary>
        /// Gets the current (top-most) ambient <see cref="SxmTransaction"/> or null when none exists.
        /// </summary>
        public static SxmTransaction? Current => slot.Value != null && slot.Value.Count > 0 ? slot.Value.Peek() : null;

        /// <summary>
        /// Pushes the supplied transaction onto the ambient transaction stack.
        /// </summary>
        /// <param name="tx">The transaction to push onto the ambient stack. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tx"/> is null.</exception>
        internal static void Push(SxmTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            var stack = slot.Value;
            if (stack == null)
            {
                stack = new Stack<SxmTransaction>();
                slot.Value = stack;
            }
            stack.Push(tx);
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
        internal static void Pop(SxmTransaction tx)
        {
            var stack = slot.Value;
            if (stack == null || stack.Count == 0)
                throw new InvalidOperationException("No ambient transaction to pop.");

            if (!ReferenceEquals(stack.Peek(), tx))
                throw new InvalidOperationException("Ambient transaction disposed out of order.");

            stack.Pop();
            if (stack.Count == 0)
                slot.Value = null;
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
        internal static bool TryRemove(SxmTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            var stack = slot.Value;
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
                var temp = new Stack<SxmTransaction>();
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

                var rebuilt = new Stack<SxmTransaction>();
                while (temp.Count > 0)
                    rebuilt.Push(temp.Pop());

                slot.Value = rebuilt.Count > 0 ? rebuilt : null;
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