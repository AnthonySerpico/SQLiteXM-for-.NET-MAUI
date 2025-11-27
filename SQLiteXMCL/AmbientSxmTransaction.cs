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

        public static void Push(SxmTransaction tx)
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

        public static void Pop(SxmTransaction tx)
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
    }
}
