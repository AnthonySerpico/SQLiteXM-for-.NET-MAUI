using System;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to decide whether to wrap or rethrow exceptions according to project policy.
    /// </summary>
    internal static class ExceptionHelper
    {
        /// <summary>
        /// Returns true for exceptions that should NOT be wrapped by SxmException.
        /// This includes cancellation, fatal runtime-host exceptions and CLR API-usage exceptions.
        /// </summary>
        internal static bool IsNonWrappable(Exception ex)
        {
            return ex is SxmException
                   || ex is OperationCanceledException
                   || ex is TaskCanceledException
                   || ex is OutOfMemoryException
                   || ex is StackOverflowException
                   || ex is AccessViolationException
                   || ex is AppDomainUnloadedException
                   || ex is ThreadAbortException
                   || ex is ThreadInterruptedException
                   || ex is ArgumentException
                   || ex is InvalidOperationException
                   || ex is NotSupportedException
                   || ex is NotImplementedException;
        }

        /// <summary>
        /// Wraps the supplied exception in an <see cref="SxmException"/>, preserving the original as InnerException.
        /// </summary>
        internal static SxmException Wrap(Exception ex, string? message = null)
        {
            if (string.IsNullOrEmpty(message))
                return new SxmException(ex);

            return new SxmException(message, ex);
        }
    }
}