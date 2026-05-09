using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Simple test entity with basic types.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class SimpleEntity : SxmEntity
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Entity testing all supported data types with defaults.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class AllTypesEntity : SxmEntity
{
    // Numeric types
    public sbyte SByteValue { get; set; }
    public byte ByteValue { get; set; }
    public short ShortValue { get; set; }
    public ushort UShortValue { get; set; }
    public int IntValue { get; set; }
    public uint UIntValue { get; set; }
    public long LongValue { get; set; }
    public ulong ULongValue { get; set; }  // Maps to TEXT
    
    // Decimal/Float types
    public decimal DecimalValue { get; set; }  // Maps to TEXT
    public float FloatValue { get; set; }
    public double DoubleValue { get; set; }
    
    // Boolean
    public bool BoolValue { get; set; }
    
    // String
    public string? StringValue { get; set; }
    
    // Guid (default BLOB)
    public Guid GuidValue { get; set; }
    
    // Time types (default INTEGER - Unix milliseconds)
    public DateTime DateTimeValue { get; set; }
    public DateTimeOffset DateTimeOffsetValue { get; set; }
    public TimeSpan TimeSpanValue { get; set; }
    public DateOnly DateOnlyValue { get; set; }
    public TimeOnly TimeOnlyValue { get; set; }
    
    // Byte array
    public byte[]? BlobValue { get; set; }
    
    // Nullable types
    public int? NullableInt { get; set; }
    public DateTime? NullableDateTime { get; set; }
    public Guid? NullableGuid { get; set; }
}

/// <summary>
/// Entity testing time type overrides to TEXT.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class TimeTypeTextEntity : SxmEntity
{
    [Column(DataType = DataType.Text)]
    public DateTime DateTimeAsText { get; set; }
    
    [Column(DataType = DataType.Text)]
    public DateTimeOffset DateTimeOffsetAsText { get; set; }
    
    [Column(DataType = DataType.Text)]
    public TimeSpan TimeSpanAsText { get; set; }
    
    [Column(DataType = DataType.Text)]
    public DateOnly DateOnlyAsText { get; set; }
    
    [Column(DataType = DataType.Text)]
    public TimeOnly TimeOnlyAsText { get; set; }
    
    [Column(DataType = DataType.Text)]
    public Guid GuidAsText { get; set; }
}

/// <summary>
/// Entity testing Column attribute requirements.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = true)]
public class ExplicitColumnEntity : SxmEntity
{
    [Column]
    public string? MappedField { get; set; }
    
    // This should NOT be mapped (no [Column] attribute)
    public string? UnmappedField { get; set; }
    
    [NotColumn]
    public string? ExplicitlyExcluded { get; set; }
}

/// <summary>
/// Entity testing indexes.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
[CreateIndex(nameof(LastName), nameof(FirstName))]  // Composite index
[CreateUniqueIndex(nameof(Email))]  // Unique index
public class IndexedEntity : SxmEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    
    [CreateIndex]  // Single-field index
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Entity testing foreign keys.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class ParentEntity : SxmEntity
{
    public string? ParentName { get; set; }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class ChildEntity : SxmEntity
{
    public string? ChildName { get; set; }
    
    [CreateForeignKey(ForeignTable: nameof(ParentEntity))]
    public long ParentId { get; set; }
    
    [NotColumn]
    public ParentEntity? Parent { get; set; }
}

/// <summary>
/// Entity testing triggers.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
[CreateTrigger("CREATE TRIGGER IF NOT EXISTS UpdateTimestamp AFTER UPDATE ON TriggerEntity BEGIN UPDATE TriggerEntity SET UpdatedDate = (strftime('%s', 'now') * 1000) WHERE id = NEW.id; END;")]
public class TriggerEntity : SxmEntity
{
    public string? Name { get; set; }
    public long UpdatedDate { get; set; }
}

/// <summary>
/// Entity testing RequiredNotNull attribute.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class RequiredFieldEntity : SxmEntity
{
    [RequiredNotNull(DefaultValue: "Default Name")]
    public string? RequiredName { get; set; }
    
    [RequiredNotNull(DefaultValue: 42)]
    public int RequiredAge { get; set; }
    
    public string? OptionalField { get; set; }
}
