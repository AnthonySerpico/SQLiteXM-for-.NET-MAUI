using System;
using System.Collections.Generic;
using System.Threading;

namespace SQLiteXM
{
    /// <summary>
    /// Ambient transaction holder that flows with ExecutionContext (async/await).
    /// Enforces strict LIFO semantics.
    /// </summary>
    public static class AmbientSxmTransaction
    {
        static readonly AsyncLocal<Stack<SxmTransaction>?> slot = new();

        public static SxmTransaction? Current => slot.Value != null && slot.Value.Count > 0 ? slot.Value.Peek() : null;

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
        /// Returns true if removed; false otherwise.
        /// Does not throw for common out-of-order situations.
        /// </summary>
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
