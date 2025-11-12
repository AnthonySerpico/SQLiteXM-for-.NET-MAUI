using System.Runtime.CompilerServices;

namespace SQLiteXM.Internal
{
    internal static class AwaitExt
    {
        // Extension method for Task
        public static ConfiguredTaskAwaitable CAF(this Task task)
            => task.ConfigureAwait(false);

        // Extension method for Task<T>
        public static ConfiguredTaskAwaitable<T> CAF<T>(this Task<T> task)
            => task.ConfigureAwait(false);

        // Extension method for ValueTask
        public static ConfiguredValueTaskAwaitable CAF(this ValueTask task)
            => task.ConfigureAwait(false);

        // Extension method for ValueTask<T>
        public static ConfiguredValueTaskAwaitable<T> CAF<T>(this ValueTask<T> task)
            => task.ConfigureAwait(false);
    }
}
