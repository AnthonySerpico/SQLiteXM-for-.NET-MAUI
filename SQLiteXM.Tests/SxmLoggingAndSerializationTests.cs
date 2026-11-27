using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for the file logger (<see cref="SxmLogging"/>) and the XML statements-file reader
/// (<see cref="SxmSerialization.RootXml.ReadFrom"/>).
///
/// Logging is disabled by default for the test run, so the logging tests switch the shared
/// EnableLogging setting on through reflection and always restore it afterwards.
/// Individual logger instances are created through the private constructor so each test writes
/// to its own temporary folder and can be disposed without affecting the shared loggers.
/// </summary>
[Collection("Sequential")]
public class SxmLoggingAndSerializationTests : TestBase
{
    private static readonly FieldInfo EnableLoggingField =
        typeof(SxmDatabaseOptions).GetField("_enableLogging", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SxmDatabaseOptions._enableLogging was not found.");

    // ---------- Helpers ----------

    private static string NewTempFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SxmLoggingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static SxmLogging CreateLogger(string fileName, string folder, long maxLogSize)
    {
        ConstructorInfo ctor = typeof(SxmLogging).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(string), typeof(string), typeof(long) },
            null)
            ?? throw new InvalidOperationException("SxmLogging private constructor was not found.");

        return (SxmLogging)ctor.Invoke(new object[] { fileName, folder, maxLogSize });
    }

    private static void WriteLog(SxmLogging logger, Exception? ex, string? method, string context)
    {
        MethodInfo write = typeof(SxmLogging).GetMethod("WriteLog", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SxmLogging.WriteLog was not found.");
        write.Invoke(logger, new object?[] { ex, method, context });
    }

    private static async Task<string> WaitForFileContentAsync(string path, string expected, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                try
                {
                    // Share read/write/delete so polling never blocks the background writer or rotation.
                    string text = ReadShared(path);
                    if (text.Contains(expected, StringComparison.Ordinal))
                        return text;
                }
                catch (IOException)
                {
                    // The background writer may still hold the file; retry.
                }
            }
            await Task.Delay(25);
        }
        return File.Exists(path) ? ReadShared(path) : string.Empty;
    }

    private static string ReadShared(string path)
    {
        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader sr = new StreamReader(fs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static void TryDeleteFolder(string folder)
    {
        try { Directory.Delete(folder, true); } catch { }
    }

    private static SxmSerialization.RootXml ReadXml(string xml)
    {
        XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
        using StringReader sr = new StringReader(xml);
        using XmlReader xr = XmlReader.Create(sr, settings);
        return SxmSerialization.RootXml.ReadFrom(xr);
    }

    // ---------- Static Log entry point ----------

    [Fact]
    public async Task Log_WhenLoggingDisabled_WritesNothing()
    {
        await InitializeSqliteXMAsync();
        object? original = EnableLoggingField.GetValue(null);
        string marker = $"DisabledMarker_{Guid.NewGuid():N}";
        string logPath = Path.Combine(SxmDatabaseDescriptor.DatabaseFolder!, $"{SxmDatabaseDescriptor.DefaultDatabase}.log");

        try
        {
            EnableLoggingField.SetValue(null, false);
            SxmLogging.Log(new InvalidOperationException(marker), "disabled context");

            await Task.Delay(300);
            string text = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
            text.Should().NotContain(marker);
        }
        finally
        {
            EnableLoggingField.SetValue(null, original);
        }
    }

    [Fact]
    public async Task Log_WhenLoggingEnabled_WritesEntryToDefaultDatabaseLog()
    {
        await InitializeSqliteXMAsync();
        object? original = EnableLoggingField.GetValue(null);
        string marker = $"EnabledMarker_{Guid.NewGuid():N}";
        string logPath = Path.Combine(SxmDatabaseDescriptor.DatabaseFolder!, $"{SxmDatabaseDescriptor.DefaultDatabase}.log");

        try
        {
            EnableLoggingField.SetValue(null, true);
            SxmLogging.Log(new InvalidOperationException(marker), "enabled context", "MyMethod");

            string text = await WaitForFileContentAsync(logPath, marker);
            text.Should().Contain(marker);
            text.Should().Contain("enabled context");
            text.Should().Contain("Method: MyMethod");
        }
        finally
        {
            EnableLoggingField.SetValue(null, original);
        }
    }

    // ---------- Factory ----------

    [Fact]
    public void LoggingFactory_EmptyDatabaseName_StillCreatesDefaultLogger()
    {
        string folder = NewTempFolder();
        try
        {
            // ".log" has no name part, so only the shared default logger is ensured. Must not throw.
            Action act = () => SxmLogging.SxmLoggingFactory(".log", folder, 1024 * 1024);
            act.Should().NotThrow();
        }
        finally
        {
            TryDeleteFolder(folder);
        }
    }

    // ---------- Instance writing ----------

    [Fact]
    public async Task WriteLog_ErrorEntry_ContainsHeaderMethodSourceAndErrorLabel()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("entry.log", folder, 10 * 1024 * 1024);
        string path = Path.Combine(folder, "entry.log");
        string marker = $"ErrMarker_{Guid.NewGuid():N}";

        try
        {
            InvalidOperationException ex;
            try { throw new InvalidOperationException(marker); }
            catch (InvalidOperationException thrown) { ex = thrown; }

            WriteLog(logger, ex, "SomeMethod", "Some context");

            string text = await WaitForFileContentAsync(path, marker);
            text.Should().Contain("******************************************************* Some context");
            text.Should().Contain("Method: SomeMethod");
            text.Should().Contain("Error System.InvalidOperationException");
            text.Should().Contain("Source:");
            text.Should().Contain("(UTC)");
            text.Should().Contain("(Local Time)");
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_WarningAndInformational_HaveNoErrorLabel()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("levels.log", folder, 10 * 1024 * 1024);
        string path = Path.Combine(folder, "levels.log");
        string warnMarker = $"Warn_{Guid.NewGuid():N}";
        string infoMarker = $"Info_{Guid.NewGuid():N}";

        try
        {
            WriteLog(logger, new SxmWarning(warnMarker), "W", "ctx");
            WriteLog(logger, new SxmInformational(infoMarker), "I", "ctx");

            string text = await WaitForFileContentAsync(path, infoMarker);
            text.Should().Contain(warnMarker);
            text.Should().Contain(infoMarker);
            text.Should().NotContain("Error SQLiteXM.SxmWarning");
            text.Should().NotContain("Error SQLiteXM.SxmInformational");
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_NullExceptionAndMethod_UsesPlaceholders()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("nulls.log", folder, 10 * 1024 * 1024);
        string path = Path.Combine(folder, "nulls.log");

        try
        {
            WriteLog(logger, null, null, "null context");

            string text = await WaitForFileContentAsync(path, "null context");
            text.Should().Contain("<unknown exception>");
            text.Should().Contain("Method: <unknown>");
            text.Should().NotContain("Source:");
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_VeryLongException_IsTruncated()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("long.log", folder, 10 * 1024 * 1024);
        string path = Path.Combine(folder, "long.log");
        string longMessage = new string('x', 10_000);

        try
        {
            WriteLog(logger, new InvalidOperationException(longMessage), "Long", "long context");

            string text = await WaitForFileContentAsync(path, "[truncated]");
            text.Should().Contain("... [truncated]");
            text.Should().NotContain(longMessage, "the 10,000-character message must be cut to the 2,048-character limit");
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_FolderDeletedAfterConstruction_IsRecreated()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("recreate.log", folder, 10 * 1024 * 1024);
        string path = Path.Combine(folder, "recreate.log");
        string marker = $"Recreate_{Guid.NewGuid():N}";

        try
        {
            Directory.Delete(folder, true);

            WriteLog(logger, new InvalidOperationException(marker), "M", "ctx");

            string text = await WaitForFileContentAsync(path, marker);
            text.Should().Contain(marker);
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_ExceedsMaxSize_RotatesToOldFile_AndReplacesExistingOld()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("rotate.log", folder, 100);
        string path = Path.Combine(folder, "rotate.log");
        string oldPath = Path.Combine(folder, "rotate.old.log");
        string first = $"First_{Guid.NewGuid():N}";
        string second = $"Second_{Guid.NewGuid():N}";

        try
        {
            WriteLog(logger, new InvalidOperationException(first), "M", "ctx");
            string oldText = await WaitForFileContentAsync(oldPath, first);
            oldText.Should().Contain(first, "an entry larger than the limit rotates the file to .old (File.Move path)");

            WriteLog(logger, new InvalidOperationException(second), "M", "ctx");
            oldText = await WaitForFileContentAsync(oldPath, second);
            oldText.Should().Contain(second, "a second rotation replaces the existing .old file (File.Replace path)");
            oldText.Should().NotContain(first);
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public async Task WriteLog_ManyEntriesQuickly_AllOrSummaryAreWritten()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("burst.log", folder, 50 * 1024 * 1024);
        string path = Path.Combine(folder, "burst.log");
        string lastMarker = $"Last_{Guid.NewGuid():N}";

        try
        {
            for (int i = 0; i < 600; i++)
                WriteLog(logger, new InvalidOperationException($"Burst {i}"), "M", "ctx");
            WriteLog(logger, new InvalidOperationException(lastMarker), "M", "ctx");

            string text = await WaitForFileContentAsync(path, lastMarker, 15000);
            text.Should().Contain(lastMarker, "the bounded queue keeps the newest entries when it overflows");
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("dispose.log", folder, 1024);

        try
        {
            logger.Dispose();
            Action again = () => logger.Dispose();
            again.Should().NotThrow();

            // Writing after dispose is swallowed rather than thrown.
            Action write = () => WriteLog(logger, new InvalidOperationException("after"), "M", "ctx");
            write.Should().NotThrow();
        }
        finally
        {
            TryDeleteFolder(folder);
        }
    }

    [Fact]
    public void GetExceptionDetails_IncludesInnerExceptionsAndTruncates()
    {
        string folder = NewTempFolder();
        SxmLogging logger = CreateLogger("details.log", folder, 1024);
        MethodInfo details = typeof(SxmLogging).GetMethod("GetExceptionDetails", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SxmLogging.GetExceptionDetails was not found.");

        try
        {
            Exception thrown;
            try { throw new InvalidOperationException("outer", new ArgumentException("inner")); }
            catch (Exception ex) { thrown = ex; }

            string text = (string)details.Invoke(logger, new object[] { thrown, 4096 })!;
            text.Should().Contain("System.InvalidOperationException: outer");
            text.Should().Contain("--- Inner Exception ---");
            text.Should().Contain("System.ArgumentException: inner");

            string shortText = (string)details.Invoke(logger, new object[] { thrown, 10 })!;
            shortText.Should().EndWith("... [truncated]");

            string empty = (string)details.Invoke(logger, new object?[] { null, 100 })!;
            empty.Should().BeEmpty();
        }
        finally
        {
            logger.Dispose();
            TryDeleteFolder(folder);
        }
    }

    // ---------- XML statements reader ----------

    [Fact]
    public void ReadFrom_FullDocument_MapsEverySection()
    {
        string xml = """
            <rootxml>
              <version> 7 </version>
              <Database><database>db1</database><isDefault> true </isDefault></Database>
              <Database><database>db2</database></Database>
              <insert><StatementName>ins</StatementName><TableName>T</TableName><Statement>INSERT 1</Statement></insert>
              <select><StatementName>sel</StatementName><TableName>T</TableName><Statement>SELECT 1</Statement></select>
              <update><StatementName>upd</StatementName><TableName>T</TableName><Statement>UPDATE 1</Statement></update>
              <delete><StatementName>del</StatementName><TableName>T</TableName><Statement>DELETE 1</Statement></delete>
              <trigger><Database>db1</Database><TableName>trg</TableName><Statement>CREATE TRIGGER</Statement></trigger>
            </rootxml>
            """;

        SxmSerialization.RootXml root = ReadXml(xml);

        root.Version.Should().Be(7);
        root.Databases.Should().HaveCount(2);
        root.Databases![0].database.Should().Be("db1");
        root.Databases[0].isDefault.Should().BeTrue();
        root.Databases[1].isDefault.Should().BeFalse("a missing isDefault element means false");

        root.Insert.Should().ContainSingle();
        root.Insert![0].StatementName.Should().Be("ins");
        root.Insert[0].TableName.Should().Be("T");
        root.Insert[0].Statement.Should().Be("INSERT 1");

        root.Select![0].StatementName.Should().Be("sel");
        root.Select[0].Statement.Should().Be("SELECT 1");
        root.Update![0].StatementName.Should().Be("upd");
        root.Update[0].Statement.Should().Be("UPDATE 1");
        root.Delete![0].StatementName.Should().Be("del");
        root.Delete[0].Statement.Should().Be("DELETE 1");

        root.Trigger.Should().ContainSingle();
        root.Trigger![0].Database.Should().Be("db1");
        root.Trigger[0].TableName.Should().Be("trg");
        root.Trigger[0].Statement.Should().Be("CREATE TRIGGER");
    }

    [Fact]
    public void ReadFrom_EmptyRoot_LeavesListsNullAndVersionZero()
    {
        SxmSerialization.RootXml root = ReadXml("<rootxml></rootxml>");

        root.Version.Should().Be(0);
        root.Databases.Should().BeNull();
        root.Insert.Should().BeNull();
        root.Select.Should().BeNull();
        root.Update.Should().BeNull();
        root.Delete.Should().BeNull();
        root.Trigger.Should().BeNull();
    }

    [Fact]
    public void ReadFrom_MissingChildElements_MapToNull()
    {
        SxmSerialization.RootXml root = ReadXml("<rootxml><insert></insert><trigger></trigger><Database></Database></rootxml>");

        root.Insert![0].StatementName.Should().BeNull();
        root.Insert[0].TableName.Should().BeNull();
        root.Insert[0].Statement.Should().BeNull();
        root.Trigger![0].Database.Should().BeNull();
        root.Databases![0].database.Should().BeNull();
        root.Databases[0].isDefault.Should().BeFalse();
    }

    [Fact]
    public void ReadFrom_WrongRootElement_Throws()
    {
        Action act = () => ReadXml("<notroot></notroot>");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Expected root element 'rootxml'*");
    }

    [Fact]
    public void ReadFrom_InvalidVersion_Throws()
    {
        Action act = () => ReadXml("<rootxml><version>abc</version></rootxml>");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ReadFrom_InvalidIsDefault_Throws()
    {
        Action act = () => ReadXml("<rootxml><Database><database>x</database><isDefault>maybe</isDefault></Database></rootxml>");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ReadFrom_MalformedXml_Throws()
    {
        Action act = () => ReadXml("<rootxml><insert>");

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void XmlModelClasses_PropertiesRoundTrip()
    {
        SxmSerialization.RootXml root = new SxmSerialization.RootXml
        {
            Version = 3,
            Databases = new List<SxmSerialization.Database> { new SxmSerialization.Database { database = "d", isDefault = true } },
            Insert = new List<SxmSerialization.Insert> { new SxmSerialization.Insert { StatementName = "a", TableName = "b", Statement = "c" } },
            Select = new List<SxmSerialization.Select> { new SxmSerialization.Select { StatementName = "a", TableName = "b", Statement = "c" } },
            Update = new List<SxmSerialization.Update> { new SxmSerialization.Update { StatementName = "a", TableName = "b", Statement = "c" } },
            Delete = new List<SxmSerialization.Delete> { new SxmSerialization.Delete { StatementName = "a", TableName = "b", Statement = "c" } },
            Trigger = new List<SxmSerialization.Trigger> { new SxmSerialization.Trigger { Database = "d", TableName = "t", Statement = "s" } }
        };

        root.Version.Should().Be(3);
        root.Databases![0].isDefault.Should().BeTrue();
        root.Insert![0].Statement.Should().Be("c");
        root.Select![0].TableName.Should().Be("b");
        root.Update![0].StatementName.Should().Be("a");
        root.Delete![0].Statement.Should().Be("c");
        root.Trigger![0].Database.Should().Be("d");
    }
}
