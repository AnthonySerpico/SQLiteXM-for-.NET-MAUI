using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Locks the publicly documented <see cref="SxmException"/> metadata contract.
/// The guidance in Docs/application-lifecycle.md tells users to branch on
/// <see cref="SxmException.ErrorCode"/>, so these tests guard that surface.
/// </summary>
public class SxmExceptionContractTests : TestBase
{
    [Fact]
    public void SxmException_ErrorCode_ShouldMatchDataEntry()
    {
        // Both representations are documented; they must never diverge.
        var ex = CaptureDependentsException();

        ex.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.TableHasDependents);
        ex.Data["sxmErrorCode"].Should().Be(SxmDefines.SxmErrorCode.TableHasDependents);
    }

    [Fact]
    public void SxmException_ErrorCode_ShouldSupportExceptionFilter()
    {
        // Mirrors the `catch (SxmException ex) when (ex.ErrorCode == ...)` form in the docs.
        var ex = CaptureDependentsException();

        bool matched = false;
        try
        {
            throw ex;
        }
        catch (SxmException caught) when (caught.ErrorCode == SxmDefines.SxmErrorCode.TableHasDependents)
        {
            matched = true;
        }

        matched.Should().BeTrue();
    }

    [Fact]
    public void SxmException_Context_ShouldBeEmpty_ForDirectValidationThrow()
    {
        // TableHasDependents is thrown directly as a pre-flight check in SxmSql.DropTableAsync and
        // never passes through a wrap, so no operation context is attached. Absence is reported as
        // an empty string, never null, so callers never need a null check.
        var ex = CaptureDependentsException();

        ex.Context.Should().BeEmpty();
        ex.GetSxmContext().Should().BeEmpty();
    }

    [Fact]
    public void SxmException_Logging_ShouldNotAttachContext()
    {
        // Logging is observational only. It must never mutate the exception, otherwise context
        // attachment would silently depend on whether a call site happened to log.
        var ex = CaptureDependentsException();

        SxmLogging.Log(ex, "Dropping table 'Parent'.");

        ex.Context.Should().BeEmpty();
    }

    [Fact]
    public void GetSxmContext_ShouldAgreeWithContextProperty()
    {
        // The extension is the supported way to read context off a pass-through exception, which has
        // no SxmException properties. It must return the same value on an SxmException.
        var ex = CaptureConversionException();

        ex.GetSxmContext().Should().Be(ex.Context);
        ex.GetSxmContext().Should().NotBeEmpty();
    }

    [Fact]
    public void GetSxmContext_ShouldReturnEmpty_ForUnrelatedException()
    {
        // An exception that never passed through SQLiteXM carries no context, and the helper must
        // report that as empty rather than throwing or returning null.
        new InvalidOperationException("unrelated").GetSxmContext().Should().BeEmpty();
    }

    [Fact]
    public void SxmException_WrappedFailure_ShouldSeparateMessageFromContext()
    {
        // The wrapper's message is hoisted from the inner exception so the root cause is readable
        // without unwrapping, while Context describes the SQLiteXM operation that was in flight.
        // The two must never hold the same string.
        var ex = CaptureConversionException();

        ex.Context.Should().NotBeNullOrWhiteSpace();
        ex.Message.Should().NotBe(ex.Context);
        ex.Message.Should().Be(ex.InnerException!.Message);
        ex.GetSxmContext().Should().Be(ex.Context);
    }

    [Fact]
    public void SxmException_WrappedFailure_ShouldReportCategoryAndKeepInnerException()
    {
        // Wrapped failures are classified by the operation that failed. The category is the
        // branching surface; InnerException remains the authoritative root cause.
        var ex = CaptureConversionException();

        ex.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.DataConversionFailure);
        ex.Data["sxmErrorCode"].Should().Be(SxmDefines.SxmErrorCode.DataConversionFailure);
        ex.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void SxmException_WrappedFailure_ShouldNotUseSpecificConditionCode()
    {
        // Guards the two-tier model: a wrapped operational failure must never report one of the
        // specific-condition codes, which are reserved for direct validation throws.
        var ex = CaptureConversionException();

        ex.ErrorCode.Should().NotBe(SxmDefines.SxmErrorCode.TableHasDependents);
        ex.ErrorCode.Should().NotBe(SxmDefines.SxmErrorCode.UnknownSqlStatement);
    }

    /// <summary>
    /// Produces a wrapped <see cref="SxmException"/> by forcing a column value that cannot be
    /// converted to the target property type.
    /// </summary>
    private SxmException CaptureConversionException()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();

        return Assert.Throws<SxmException>(() =>
            SxmSql.RunStatementAsync<SimpleEntity>(
                "SELECT 1 AS id, 'not-a-number' AS Age").GetAwaiter().GetResult());
    }

    /// <summary>
    /// Produces a real <see cref="SxmException"/> through the public API rather than
    /// constructing one directly, since all constructors are internal.
    /// </summary>
    private SxmException CaptureDependentsException()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();

        var parent = new ExceptionContractParentEntity { Name = "Parent" };
        parent.SaveAsync().GetAwaiter().GetResult();

        var child = new ExceptionContractChildEntity { Name = "Child", ParentId = parent.id };
        child.SaveAsync().GetAwaiter().GetResult();

        return Assert.Throws<SxmException>(() =>
            SxmSql.DropTableAsync(nameof(ExceptionContractParentEntity)).GetAwaiter().GetResult());
    }

    #region Test Entities

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class ExceptionContractParentEntity : SxmEntity { public string? Name { get; set; } }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class ExceptionContractChildEntity : SxmEntity
    {
        public string? Name { get; set; }
        [ForeignKey(foreignTable: nameof(ExceptionContractParentEntity))]
        public long ParentId { get; set; }
    }

    #endregion
}
