using System;
using System.IO;
using System.Reflection;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Unit tests for <see cref="SxmDatabaseOptionsValidator"/>.
///
/// These are pure in-memory tests - no database file, no connection, no initialization.
/// They exercise the rules that decide whether a <see cref="SxmDatabaseOptions"/> instance
/// is accepted, rejected with errors, or accepted with warnings.
///
/// Terminology used throughout:
///  - "error"   = validation failure. Initialization throws.
///  - "warning" = unusual but legal configuration. Logged, initialization continues.
///
/// A key rule under test: a null option property means "use the library default" and is
/// always valid. Only explicitly set (non-null) properties are validated.
/// </summary>
[Collection("Sequential")]
public class DatabaseOptionsValidatorTests : IDisposable
{
    /// <summary>
    /// By design, SQLiteXM keeps a single shared configuration for every database declared in
    /// statements.json. EnableConnectionPooling, EnableLogging and DefaultTimeout are therefore
    /// deliberately stored in static fields on <see cref="SxmDatabaseOptions"/>, so assigning any
    /// of them sets the value for the whole process.
    ///
    /// That is intended library behavior, but it means a validator test that sets one of those
    /// properties would change the configuration seen by every other test in the suite. This helper
    /// restores them to their initial null ("use default") state before and after each test in this
    /// class so the tests stay isolated without altering the library's shared-configuration design.
    /// </summary>
    private static void ResetSharedOptionState()
    {
        string[] sharedFieldNames = { "_enableConnectionPooling", "_enableLogging", "_defaultTimeout" };

        foreach (string fieldName in sharedFieldNames)
        {
            FieldInfo field = typeof(SxmDatabaseOptions)
                .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    $"Expected to find the shared static field '{fieldName}' on {nameof(SxmDatabaseOptions)}. " +
                    "If it was renamed or removed, update ResetSharedOptionState so these tests keep " +
                    "restoring the shared configuration instead of leaking it into the rest of the suite.");

            field.SetValue(null, null);
        }
    }

    public DatabaseOptionsValidatorTests() => ResetSharedOptionState();

    public void Dispose() => ResetSharedOptionState();

    #region Defaults - null options and unset properties

    [Fact]
    public void Validate_NullOptions_ShouldBeValidWithNoErrorsOrWarnings()
    {
        var result = SxmDatabaseOptionsValidator.Validate(null);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyOptions_ShouldBeValidWithNoErrorsOrWarnings()
    {
        // Every property left null means "accept all defaults", which must validate cleanly.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region BusyTimeout

    [Theory]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Validate_NegativeBusyTimeout_ShouldProduceError(long busyTimeout)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { BusyTimeout = busyTimeout });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("BusyTimeout must be >= 0");
    }

    [Fact]
    public void Validate_BusyTimeoutAboveInt32Max_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { BusyTimeout = 2147483648L });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("BusyTimeout exceeds maximum");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(60000)]
    public void Validate_BusyTimeoutWithinNormalRange_ShouldBeValidWithNoWarnings(long busyTimeout)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { BusyTimeout = busyTimeout });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_BusyTimeoutOverOneMinute_ShouldBeValidWithWarning()
    {
        // Legal, but long blocking waits hurt responsiveness - warn rather than reject.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { BusyTimeout = 60001 });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("BusyTimeout is very large");
    }

    #endregion

    #region CacheSize

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveCacheSize_ShouldProduceError(long cacheSize)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CacheSize = cacheSize });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("CacheSize must be > 0");
    }

    [Fact]
    public void Validate_CacheSizeAboveOneGigabyte_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CacheSize = 1048577 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("CacheSize exceeds maximum recommended size");
    }

    [Fact]
    public void Validate_CacheSizeBelowOneMegabyte_ShouldBeValidWithVerySmallWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CacheSize = 512 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CacheSize is very small");
    }

    [Theory]
    [InlineData(1500)]    // >= 1 MB but below the typical 2 MB floor
    [InlineData(102401)]  // just above the typical 100 MB ceiling
    [InlineData(1048576)] // exactly 1 GB - allowed, but far outside typical
    public void Validate_CacheSizeOutsideTypicalRange_ShouldBeValidWithWarning(long cacheSize)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CacheSize = cacheSize });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("outside typical range");
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(32768)]
    [InlineData(102400)]
    public void Validate_CacheSizeWithinTypicalRange_ShouldBeValidWithNoWarnings(long cacheSize)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CacheSize = cacheSize });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region WalAutoCheckpoint

    [Fact]
    public void Validate_NegativeWalAutoCheckpoint_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = -1 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("WalAutoCheckpoint must be >= 0");
    }

    [Fact]
    public void Validate_WalAutoCheckpointAboveMaximum_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = 1000001 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("WalAutoCheckpoint exceeds maximum recommended");
    }

    [Fact]
    public void Validate_WalAutoCheckpointZero_ShouldBeValidWithNoWarnings()
    {
        // 0 explicitly means "disable automatic checkpointing". On its own that is legal and
        // unremarkable. It only earns a warning when combined with WAL journal mode, which is
        // covered separately in the configuration-combination tests.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = 0 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WalAutoCheckpointBelowTypicalMinimum_ShouldBeValidWithWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = 50 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("WalAutoCheckpoint is very small");
    }

    [Fact]
    public void Validate_WalAutoCheckpointAboveTypicalMaximum_ShouldBeValidWithWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = 10001 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("WalAutoCheckpoint is large");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Validate_WalAutoCheckpointWithinTypicalRange_ShouldBeValidWithNoWarnings(long pages)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { WalAutoCheckpoint = pages });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region DefaultTimeout

    [Fact]
    public void Validate_NegativeDefaultTimeout_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { DefaultTimeout = -1 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("DefaultTimeout must be >= 0");
    }

    [Fact]
    public void Validate_ZeroDefaultTimeout_ShouldBeValidWithWarning()
    {
        // Zero is legal but means "fail immediately if the database is busy".
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { DefaultTimeout = 0 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("DefaultTimeout is 0 seconds");
    }

    [Fact]
    public void Validate_DefaultTimeoutOverFiveMinutes_ShouldBeValidWithWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { DefaultTimeout = 301 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("DefaultTimeout is very large");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(300)]
    public void Validate_DefaultTimeoutWithinNormalRange_ShouldBeValidWithNoWarnings(int seconds)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { DefaultTimeout = seconds });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region CheckPointWalMaxSize

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveCheckPointWalMaxSize_ShouldProduceError(int size)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CheckPointWalMaxSize = size });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("CheckPointWalMaxSize must be > 0");
    }

    [Fact]
    public void Validate_CheckPointWalMaxSizeAboveOneGigabyte_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CheckPointWalMaxSize = 1048577 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("CheckPointWalMaxSize exceeds maximum recommended");
    }

    [Fact]
    public void Validate_CheckPointWalMaxSizeBelowOneMegabyte_ShouldBeValidWithVerySmallWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CheckPointWalMaxSize = 512 });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CheckPointWalMaxSize is very small");
    }

    [Theory]
    [InlineData(2048)]    // >= 1 MB but below the typical 10 MB floor
    [InlineData(102401)]  // just above the typical 100 MB ceiling
    public void Validate_CheckPointWalMaxSizeOutsideTypicalRange_ShouldBeValidWithWarning(int size)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CheckPointWalMaxSize = size });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("outside typical range");
    }

    [Theory]
    [InlineData(10240)]
    [InlineData(51200)]
    [InlineData(102400)]
    public void Validate_CheckPointWalMaxSizeWithinTypicalRange_ShouldBeValidWithNoWarnings(int size)
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { CheckPointWalMaxSize = size });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region Enum validation

    [Fact]
    public void Validate_UndefinedJournalMode_ShouldProduceError()
    {
        // A value cast from an int that is not a declared enum member must be rejected.
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = (SxmJournalMode)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JournalModeOption"));
    }

    [Fact]
    public void Validate_UndefinedSynchronousMode_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { SynchronousModeOption = (SxmSynchronousMode)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Invalid SynchronousModeOption");
    }

    [Fact]
    public void Validate_UndefinedTempStore_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { TempStore = (SxmTempStore)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Invalid TempStore");
    }

    [Fact]
    public void Validate_UndefinedCheckPointConnection_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { CheckPointConnection = (CheckPointConnection)99 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid CheckPointConnection"));
    }

    [Fact]
    public void Validate_AllEnumsUndefined_ShouldReportEveryInvalidEnum()
    {
        // Validation must not stop at the first bad enum - callers should see all problems at once.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = (SxmJournalMode)99,
            SynchronousModeOption = (SxmSynchronousMode)99,
            TempStore = (SxmTempStore)99,
            CheckPointConnection = (CheckPointConnection)99
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(4);
        result.Errors.Should().Contain(e => e.Contains("Invalid JournalModeOption"));
        result.Errors.Should().Contain(e => e.Contains("Invalid SynchronousModeOption"));
        result.Errors.Should().Contain(e => e.Contains("Invalid TempStore"));
        result.Errors.Should().Contain(e => e.Contains("Invalid CheckPointConnection"));
    }

    [Theory]
    [InlineData(SxmJournalMode.Delete)]
    [InlineData(SxmJournalMode.Truncate)]
    [InlineData(SxmJournalMode.Persist)]
    [InlineData(SxmJournalMode.Wal)]
    public void Validate_DefinedJournalModes_ShouldNotProduceEnumErrors(SxmJournalMode journalMode)
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = journalMode });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region DatabaseFolderOverride

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankDatabaseFolderOverride_ShouldBeValid(string? folder)
    {
        // Null or whitespace means "use the default database location".
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { DatabaseFolderOverride = folder });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RelativeDatabaseFolderOverride_ShouldProduceError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { DatabaseFolderOverride = Path.Combine("relative", "folder") });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("must be an absolute path");
    }

    [Fact]
    public void Validate_DatabaseFolderOverrideWithInvalidCharacters_ShouldProduceError()
    {
        // The null character is an invalid path character on every supported platform.
        string rootedPathWithNullChar = Path.Combine(Path.GetTempPath(), "bad\0folder");

        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { DatabaseFolderOverride = rootedPathWithNullChar });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("invalid path characters");
    }

    [Fact]
    public void Validate_AbsoluteDatabaseFolderOverride_ShouldBeValid()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { DatabaseFolderOverride = Path.GetTempPath() });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AbsoluteDatabaseFolderOverrideThatDoesNotExist_ShouldBeValid()
    {
        // The folder is created during initialization, so a not-yet-existing path is acceptable.
        string notYetCreated = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", Guid.NewGuid().ToString("N"));

        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { DatabaseFolderOverride = notYetCreated });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Configuration combinations - WAL mode

    [Fact]
    public void Validate_WalModeWithoutCheckpointStrategy_ShouldWarnAboutDefaults()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = SxmJournalMode.Wal });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("WAL mode enabled without explicit checkpoint strategy");
    }

    [Fact]
    public void Validate_WalModeWithAutoCheckpointDisabled_ShouldWarnAboutUnboundedWalGrowth()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Wal,
            WalAutoCheckpoint = 0
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("disables automatic checkpointing");
    }

    [Fact]
    public void Validate_WalModeWithExplicitCheckpointConnection_ShouldBeValidWithNoWarnings()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Wal,
            CheckPointConnection = CheckPointConnection.OnConnectionClose
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region Configuration combinations - non-WAL mode

    [Fact]
    public void Validate_NonWalModeWithWalAutoCheckpoint_ShouldWarnSettingIsIgnored()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Delete,
            WalAutoCheckpoint = 1000
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("WalAutoCheckpoint only applies to WAL mode and will be ignored");
    }

    [Fact]
    public void Validate_NonWalModeWithCheckPointWalMaxSize_ShouldWarnSettingIsIgnored()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Delete,
            CheckPointWalMaxSize = 20480
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CheckPointWalMaxSize only applies to WAL mode and will be ignored");
    }

    [Fact]
    public void Validate_NonWalModeWithActiveCheckPointConnection_ShouldWarn()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Truncate,
            CheckPointConnection = CheckPointConnection.OnConnectionClose
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CheckPointConnection primarily applies to WAL mode");
    }

    [Fact]
    public void Validate_NonWalModeWithCheckPointConnectionOff_ShouldBeValidWithNoWarnings()
    {
        // Explicitly turning checkpointing off in a non-WAL mode is consistent, so no warning.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            JournalModeOption = SxmJournalMode.Truncate,
            CheckPointConnection = CheckPointConnection.Off
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region Configuration combinations - durability warnings

    [Fact]
    public void Validate_JournalModeOff_ShouldRaiseCriticalDurabilityWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = SxmJournalMode.Off });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CRITICAL: Journal mode is OFF");
    }

    [Fact]
    public void Validate_JournalModeMemory_ShouldWarnAboutReducedDurability()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = SxmJournalMode.Memory });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("Journal mode is MEMORY");
    }

    [Fact]
    public void Validate_SynchronousModeOff_ShouldRaiseCriticalDurabilityWarning()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { SynchronousModeOption = SxmSynchronousMode.Off });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("CRITICAL: Synchronous mode is OFF");
    }

    [Fact]
    public void Validate_SynchronousModeExtra_ShouldWarnAboutWritePerformance()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { SynchronousModeOption = SxmSynchronousMode.Extra });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("Synchronous mode is EXTRA");
    }

    [Theory]
    [InlineData(SxmSynchronousMode.Normal)]
    [InlineData(SxmSynchronousMode.Full)]
    public void Validate_BalancedSynchronousModes_ShouldBeValidWithNoWarnings(SxmSynchronousMode mode)
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { SynchronousModeOption = mode });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region Configuration combinations - connection pooling

    [Fact]
    public void Validate_PoolingDisabledWithBusyTimeout_ShouldWarnAboutContention()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            EnableConnectionPooling = false,
            BusyTimeout = 5000
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("Connection pooling is disabled but BusyTimeout is configured");
    }

    [Fact]
    public void Validate_PoolingEnabledWithBusyTimeout_ShouldBeValidWithNoWarnings()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            EnableConnectionPooling = true,
            BusyTimeout = 5000
        });

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region ValidationResult contract

    [Fact]
    public void ThrowIfValidationErrors_WhenValid_ShouldNotThrow()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions { BusyTimeout = 5000 });

        Action act = () => result.ThrowIfValidationErrors();

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfValidationErrors_WhenInvalid_ShouldThrowArgumentExceptionListingEveryError()
    {
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            BusyTimeout = -1,
            CacheSize = 0
        });

        result.Errors.Should().HaveCount(2);

        Action act = () => result.ThrowIfValidationErrors();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*validation failed with 2 error(s)*")
            .And.ParamName.Should().Be("SxmDatabaseOptions");
    }

    [Fact]
    public void ThrowIfValidationErrors_WhenOnlyWarnings_ShouldNotThrow()
    {
        // Warnings must never block initialization.
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { SynchronousModeOption = SxmSynchronousMode.Off });

        result.Warnings.Should().NotBeEmpty();
        result.IsValid.Should().BeTrue();

        Action act = () => result.ThrowIfValidationErrors();

        act.Should().NotThrow();
    }

    [Fact]
    public void LogValidationWarnings_WithWarnings_ShouldNotThrow()
    {
        var result = SxmDatabaseOptionsValidator.Validate(
            new SxmDatabaseOptions { JournalModeOption = SxmJournalMode.Off });

        result.Warnings.Should().NotBeEmpty();

        Action act = () => result.LogValidationWarnings();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MultipleIndependentErrors_ShouldAllBeReportedTogether()
    {
        // Callers should be able to fix every problem in one pass rather than one per run.
        var result = SxmDatabaseOptionsValidator.Validate(new SxmDatabaseOptions
        {
            BusyTimeout = -1,
            CacheSize = -1,
            WalAutoCheckpoint = -1,
            DefaultTimeout = -1,
            CheckPointWalMaxSize = -1
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(5);
    }

    #endregion
}
