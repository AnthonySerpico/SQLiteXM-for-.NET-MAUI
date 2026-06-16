using System.Runtime.CompilerServices;

namespace SQLiteXM
{
    /// <summary>
    /// Provides small extension helpers for quickly calling <see cref="Task.ConfigureAwait(bool)"/> and <see cref="ValueTask.ConfigureAwait(bool)"/>.
    /// </summary>
    internal static class SxmAwaitPolicyExtensions
    {
        /// <summary>
        /// Configures an await on the supplied <see cref="Task"/> to not capture the current synchronization context.
        /// </summary>
        /// <param name="task">The task to configure.</param>
        /// <returns>A <see cref="ConfiguredTaskAwaitable"/> that will not capture the current synchronization context when awaited.</returns>
        /// <remarks>
        /// This is a convenience wrapper for <see cref="Task.ConfigureAwait(bool)"/> with <c>false</c>.
        /// Use to avoid deadlocks and unnecessary context captures in library code.
        /// </remarks>
        internal static ConfiguredTaskAwaitable ConfigureFalse(this Task task) => task.ConfigureAwait(false);

        /// <summary>
        /// Configures an await on the supplied <see cref="Task{TResult}"/> to not capture the current synchronization context.
        /// </summary>
        /// <typeparam name="T">The type of the task result.</typeparam>
        /// <param name="task">The task to configure.</param>
        /// <returns>A <see cref="ConfiguredTaskAwaitable{TResult}"/> that will not capture the current synchronization context when awaited.</returns>
        /// <remarks>
        /// This is a convenience wrapper for <see cref="Task{TResult}.ConfigureAwait(bool)"/> with <c>false</c>.
        /// Use to avoid deadlocks and unnecessary context captures in library code.
        /// </remarks>
        internal static ConfiguredTaskAwaitable<T> ConfigureFalse<T>(this Task<T> task) => task.ConfigureAwait(false);

        /// <summary>
        /// Configures an await on the supplied <see cref="ValueTask"/> to not capture the current synchronization context.
        /// </summary>
        /// <param name="task">The value task to configure.</param>
        /// <returns>A <see cref="ConfiguredValueTaskAwaitable"/> that will not capture the current synchronization context when awaited.</returns>
        /// <remarks>
        /// This is a convenience wrapper for <see cref="ValueTask.ConfigureAwait(bool)"/> with <c>false</c>.
        /// Prefer <see cref="ValueTask"/> overloads when dealing with value-task-returning APIs.
        /// </remarks>
        internal static ConfiguredValueTaskAwaitable ConfigureFalse(this ValueTask task) => task.ConfigureAwait(false);

        /// <summary>
        /// Configures an await on the supplied <see cref="ValueTask{TResult}"/> to not capture the current synchronization context.
        /// </summary>
        /// <typeparam name="T">The type of the value task result.</typeparam>
        /// <param name="task">The value task to configure.</param>
        /// <returns>A <see cref="ConfiguredValueTaskAwaitable{TResult}"/> that will not capture the current synchronization context when awaited.</returns>
        /// <remarks>
        /// This is a convenience wrapper for <see cref="ValueTask{TResult}.ConfigureAwait(bool)"/> with <c>false</c>.
        /// Use to avoid deadlocks and unnecessary context captures in library code.
        /// </remarks>
        internal static ConfiguredValueTaskAwaitable<T> ConfigureFalse<T>(this ValueTask<T> task) => task.ConfigureAwait(false);
    }
}