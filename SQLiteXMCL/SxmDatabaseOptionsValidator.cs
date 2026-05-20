using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Validates SxmDatabaseOptions instances for correctness.
    /// Null options or null properties indicate acceptance of default values and are always valid.
    /// </summary>
    internal static class SxmDatabaseOptionsValidator
    {
        /// <summary>
        /// Validation result containing errors and warnings.
        /// </summary>
        internal sealed class ValidationResult
        {
            /// <summary>
            /// Gets the list of validation errors that prevent initialization.
            /// </summary>
            public IReadOnlyList<string> Errors { get; }

            /// <summary>
            /// Gets the list of validation warnings for unusual but valid configurations.
            /// </summary>
            public IReadOnlyList<string> Warnings { get; }

            /// <summary>
            /// Gets a value indicating whether validation passed (no errors).
            /// </summary>
            public bool IsValid => Errors.Count == 0;

            internal ValidationResult(List<string> errors, List<string> warnings)
            {
                Errors = errors.AsReadOnly();
                Warnings = warnings.AsReadOnly();
            }

            /// <summary>
            /// Throws an exception if validation failed.
            /// </summary>
            /// <exception cref="ArgumentException">Thrown when validation errors exist.</exception>
            internal void ThrowIfValidationErrors()
            {
                if (!IsValid)
                {
                    string message = $"SxmDatabaseOptions validation failed with {Errors.Count} error(s):{Environment.NewLine}" + string.Join(Environment.NewLine, Errors.Select((e, i) => $"  [{i + 1}] {e}"));
                    throw new ArgumentException(message, nameof(SxmDatabaseOptions));
                }
            }

            internal void LogValidationWarnings()  
            {
                if (Warnings.Count > 0)
                {
                    string message = $"SxmDatabaseOptions validation completed with {Warnings.Count} warning(s):{Environment.NewLine}" + string.Join(Environment.NewLine, Warnings.Select((w, i) => $"  [{i + 1}] {w}"));
                    SxmLogging.Log(new SxmWarning(message), "Database Options Warning", $"LogDatabaseWarnings");
                }
            }
        }

        /// <summary>
        /// Validates the SxmDatabaseOptions instance.
        /// Null options or null properties are valid and indicate acceptance of defaults.
        /// Called automatically during SxmDatabase.InitializeAsync.
        /// </summary>
        /// <param name="options">The options to validate. Null is valid (use all defaults).</param>
        /// <returns>Validation result containing errors and warnings.</returns>
        internal static ValidationResult Validate(SxmDatabaseOptions? options)
        {
            // Null options means "use all defaults" - perfectly valid
            if (options == null)
                return new ValidationResult(new List<string>(), new List<string>());

            var errors = new List<string>();
            var warnings = new List<string>();

            // Only validate properties that have been explicitly set (non-null)
            ValidateBusyTimeout(options, errors, warnings);
            ValidateCacheSize(options, errors, warnings);
            ValidateWalAutoCheckpoint(options, errors, warnings);
            ValidateDefaultTimeout(options, errors, warnings);
            ValidateCheckPointWalMaxSize(options, errors, warnings);
            ValidateEnumValues(options, errors);
            ValidateDatabaseFolderOverride(options, errors);
            ValidateConfigurationCombinations(options, warnings);

            return new ValidationResult(errors, warnings);
        }

        #region Individual Validation Methods

        private static void ValidateBusyTimeout(SxmDatabaseOptions options, List<string> errors, List<string> warnings)
        {
            // Null means "use default" - skip validation
            if (!options.BusyTimeout.HasValue)
                return;

            long value = options.BusyTimeout.Value;

            if (value < 0)
            {
                errors.Add($"BusyTimeout must be >= 0 milliseconds. Current: {value} ms");
            }
            else if (value > 2147483647)
            {
                errors.Add($"BusyTimeout exceeds maximum (2,147,483,647 ms). Current: {value} ms");
            }
            else if (value > 60000) // > 1 minute
            {
                warnings.Add($"BusyTimeout is very large ({value} ms = {value / 1000.0:F1} seconds). Long waits may impact user experience.");
            }
        }

        private static void ValidateCacheSize(SxmDatabaseOptions options, List<string> errors, List<string> warnings)
        {
            // Null means "use default" - skip validation
            if (!options.CacheSize.HasValue)
                return;

            const long MinRecommended = 1024;     // 1 MB
            const long MaxRecommended = 1048576;  // 1 GB
            const long TypicalMin = 2048;         // 2 MB
            const long TypicalMax = 102400;       // 100 MB

            long value = options.CacheSize.Value;

            if (value <= 0)
            {
                errors.Add($"CacheSize must be > 0 KB. Current: {value} KB");
            }
            else if (value > MaxRecommended)
            {
                errors.Add($"CacheSize exceeds maximum recommended size (1,048,576 KB = 1 GB). Current: {value} KB ({value / 1024.0:F1} MB)");
            }
            else if (value < MinRecommended)
            {
                warnings.Add($"CacheSize is very small ({value} KB = {value / 1024.0:F2} MB). This may significantly impact performance. Minimum recommended: 1,024 KB (1 MB). Typical range: 2,048-102,400 KB (2-100 MB).");
            }
            else if (value < TypicalMin || value > TypicalMax)
            {
                warnings.Add($"CacheSize ({value} KB = {value / 1024.0:F1} MB) is outside typical range of 2,048-102,400 KB (2-100 MB). Verify this is intentional.");
            }
        }

        private static void ValidateWalAutoCheckpoint(SxmDatabaseOptions options, List<string> errors, List<string> warnings)
        {
            // Null means "use default" - skip validation
            if (!options.WalAutoCheckpoint.HasValue)
                return;

            const long MaxRecommended = 1000000; // 1 million pages (~4 GB with 4KB pages)
            const long TypicalMin = 100;
            const long TypicalMax = 10000;

            long value = options.WalAutoCheckpoint.Value;

            if (value < 0)
            {
                errors.Add($"WalAutoCheckpoint must be >= 0 (0 disables auto-checkpoint). Current: {value} pages");
            }
            else if (value > MaxRecommended)
            {
                errors.Add($"WalAutoCheckpoint exceeds maximum recommended (1,000,000 pages). Current: {value} pages");
            }
            else if (value > 0 && value < TypicalMin)
            {
                warnings.Add($"WalAutoCheckpoint is very small ({value} pages ≈ {value * 4 / 1024.0:F1} MB with 4KB pages). This may cause frequent checkpoints and reduce performance. Typical range: 1,000-10,000 pages (4-40 MB).");
            }
            else if (value > TypicalMax)
            {
                warnings.Add($"WalAutoCheckpoint is large ({value} pages ≈ {value * 4 / 1024.0:F1} MB with 4KB pages). WAL file may grow significantly before checkpoint. Typical range: 1,000-10,000 pages (4-40 MB).");
            }
        }

        private static void ValidateDefaultTimeout(SxmDatabaseOptions options, List<string> errors, List<string> warnings)
        {
            // Null means "use default" - skip validation
            if (!options.DefaultTimeout.HasValue)
                return;

            int value = options.DefaultTimeout.Value;

            if (value < 0)
            {
                errors.Add($"DefaultTimeout must be >= 0 seconds. Current: {value} seconds");
            }
            else if (value > 300) // > 5 minutes
            {
                warnings.Add($"DefaultTimeout is very large ({value} seconds = {value / 60.0:F1} minutes). Long-running operations may block user interface or cause timeouts elsewhere.");
            }
            else if (value == 0)
            {
                warnings.Add("DefaultTimeout is 0 seconds. Operations will fail immediately if resources are busy. Consider a small timeout (5-30 seconds) for better reliability.");
            }
        }

        private static void ValidateCheckPointWalMaxSize(SxmDatabaseOptions options, List<string> errors, List<string> warnings)
        {
            // Null means "use default" - skip validation
            if (!options.CheckPointWalMaxSize.HasValue)
                return;

            const int MinRecommended = 1024;     // 1 MB
            const int MaxRecommended = 1048576;  // 1 GB
            const int TypicalMin = 10240;        // 10 MB
            const int TypicalMax = 102400;       // 100 MB

            int value = options.CheckPointWalMaxSize.Value;

            if (value <= 0)
            {
                errors.Add($"CheckPointWalMaxSize must be > 0 KB. Current: {value} KB");
            }
            else if (value > MaxRecommended)
            {
                errors.Add($"CheckPointWalMaxSize exceeds maximum recommended (1,048,576 KB = 1 GB). Current: {value} KB ({value / 1024.0:F1} MB)");
            }
            else if (value < MinRecommended)
            {
                warnings.Add($"CheckPointWalMaxSize is very small ({value} KB = {value / 1024.0:F2} MB). This may cause frequent checkpoints. Minimum recommended: 1,024 KB (1 MB). Typical range: 10,240-102,400 KB (10-100 MB).");
            }
            else if (value < TypicalMin || value > TypicalMax)
            {
                warnings.Add($"CheckPointWalMaxSize ({value} KB = {value / 1024.0:F1} MB) is outside typical range of 10,240-102,400 KB (10-100 MB). Verify this is intentional.");
            }
        }

        private static void ValidateEnumValues(SxmDatabaseOptions options, List<string> errors)
        {
            // Only validate enums that have been explicitly set (non-null)
            if (options.JournalModeOption.HasValue &&
                !Enum.IsDefined(typeof(SxmJournalMode), options.JournalModeOption.Value))
            {
                errors.Add($"Invalid JournalModeOption: {options.JournalModeOption.Value} ({(int)options.JournalModeOption.Value})");
            }

            if (options.SynchronousModeOption.HasValue &&
                !Enum.IsDefined(typeof(SxmSynchronousMode), options.SynchronousModeOption.Value))
            {
                errors.Add($"Invalid SynchronousModeOption: {options.SynchronousModeOption.Value} ({(int)options.SynchronousModeOption.Value})");
            }

            if (options.TempStore.HasValue &&
                !Enum.IsDefined(typeof(SxmTempStore), options.TempStore.Value))
            {
                errors.Add($"Invalid TempStore: {options.TempStore.Value} ({(int)options.TempStore.Value})");
            }

            if (options.CheckPointConnection.HasValue &&
                !Enum.IsDefined(typeof(CheckPointConnection), options.CheckPointConnection.Value))
            {
                errors.Add($"Invalid CheckPointConnection: {options.CheckPointConnection.Value} ({(int)options.CheckPointConnection.Value})");
            }
        }

        private static void ValidateDatabaseFolderOverride(SxmDatabaseOptions options, List<string> errors)
        {
            // Null or whitespace means "use default location" - skip validation
            if (string.IsNullOrWhiteSpace(options.DatabaseFolderOverride))
                return;

            try
            {
                string path = options.DatabaseFolderOverride;

                // Check if path is rooted (absolute)
                if (!Path.IsPathRooted(path))
                {
                    errors.Add($"DatabaseFolderOverride must be an absolute path. Current: '{path}'");
                    return; // Don't continue validation if path format is wrong
                }

                // Check for invalid path characters
                char[] invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    errors.Add($"DatabaseFolderOverride contains invalid path characters: '{path}'");
                    return;
                }

                // Verify path can be used (checks for invalid format without requiring directory to exist)
                string _ = Path.GetFullPath(path);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"DatabaseFolderOverride path is invalid: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                errors.Add($"DatabaseFolderOverride path format not supported: {ex.Message}");
            }
            catch (System.Security.SecurityException ex)
            {
                errors.Add($"DatabaseFolderOverride access denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"DatabaseFolderOverride validation failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ValidateConfigurationCombinations(SxmDatabaseOptions options, List<string> warnings)
        {
            // WAL mode validation - only if JournalModeOption is explicitly set
            if (options.JournalModeOption.HasValue)
            {
                SxmJournalMode journalMode = options.JournalModeOption.Value;

                if (journalMode == SxmJournalMode.Wal)
                {
                    ValidateWalModeConfiguration(options, warnings);
                }
                else
                {
                    ValidateNonWalModeConfiguration(options, journalMode, warnings);
                }

                // Journal mode warnings
                if (journalMode == SxmJournalMode.Off)
                {
                    warnings.Add("CRITICAL: Journal mode is OFF. Transactions are not protected by rollback journal. Database corruption may occur if a crash happens during a write operation. Use only for temporary databases or import operations.");
                }
                else if (journalMode == SxmJournalMode.Memory)
                {
                    warnings.Add("Journal mode is MEMORY. Rollback journal is stored in memory. This improves performance but reduces durability - a crash during a transaction will likely corrupt the database.");
                }
            }

            // Synchronous mode warnings - only if explicitly set
            if (options.SynchronousModeOption.HasValue)
            {
                if (options.SynchronousModeOption.Value == SxmSynchronousMode.Off)
                {
                    warnings.Add("CRITICAL: Synchronous mode is OFF. Database writes are NOT synchronized to disk. Data corruption or loss may occur on system crash, power loss, or application termination. Use only for temporary data or if another mechanism ensures durability.");
                }
                else if (options.SynchronousModeOption.Value == SxmSynchronousMode.Extra)
                {
                    warnings.Add("Synchronous mode is EXTRA. This provides maximum durability but significantly reduces write performance. Consider FULL mode for a better balance unless absolute durability is required.");
                }
            }

            // Connection pooling with busy timeout - only warn if both are explicitly set
            if (options.EnableConnectionPooling.HasValue &&
                options.EnableConnectionPooling.Value == false &&
                options.BusyTimeout.HasValue &&
                options.BusyTimeout.Value > 0)
            {
                warnings.Add("Connection pooling is disabled but BusyTimeout is configured. Without pooling, connection contention is more likely. Consider enabling pooling for better concurrency.");
            }
        }

        private static void ValidateWalModeConfiguration(SxmDatabaseOptions options, List<string> warnings)
        {
            if (options.WalAutoCheckpoint.HasValue && options.WalAutoCheckpoint.Value == 0)
            {
                warnings.Add("WAL mode with WalAutoCheckpoint=0 disables automatic checkpointing. You must manually checkpoint or the WAL file will grow indefinitely. Consider enabling CheckPointConnection or setting WalAutoCheckpoint > 0.");
            }

            if (!options.CheckPointConnection.HasValue && !options.WalAutoCheckpoint.HasValue)
            {
                warnings.Add("WAL mode enabled without explicit checkpoint strategy. Defaults will be used. Consider configuring CheckPointConnection or WalAutoCheckpoint to control checkpoint behavior.");
            }
        }

        private static void ValidateNonWalModeConfiguration(SxmDatabaseOptions options, SxmJournalMode journalMode, List<string> warnings)
        {
            // Not using WAL mode - warn about WAL-specific settings that are set but will be ignored
            if (options.WalAutoCheckpoint.HasValue)
            {
                warnings.Add($"WalAutoCheckpoint is configured but JournalModeOption is {journalMode}. WalAutoCheckpoint only applies to WAL mode and will be ignored.");
            }

            if (options.CheckPointWalMaxSize.HasValue)
            {
                warnings.Add($"CheckPointWalMaxSize is configured but JournalModeOption is {journalMode}. CheckPointWalMaxSize only applies to WAL mode and will be ignored.");
            }

            if (options.CheckPointConnection.HasValue &&
                options.CheckPointConnection.Value != CheckPointConnection.Off)
            {
                warnings.Add($"CheckPointConnection is configured but JournalModeOption is {journalMode}. CheckPointConnection primarily applies to WAL mode.");
            }
        }

        #endregion
    }
}