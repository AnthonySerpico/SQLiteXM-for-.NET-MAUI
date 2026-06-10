# iOS Deployment Guide

## Overview

SQLiteXM is fully compatible with iOS AOT (Ahead-of-Time) compilation and app trimming. This guide explains the recommended configuration for deploying SQLiteXM-based applications to iOS.

## Quick Start

Add the following configuration to your MAUI app's `.csproj` file to ensure reliable iOS deployment:

```xml
<!-- iOS AOT/Trimming Configuration -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0-ios' or '$(TargetFramework)' == 'net9.0-ios'">
  <PublishAot>true</PublishAot>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-ios' or '$(TargetFramework)' == 'net9.0-ios'">
  <TrimmerRootAssembly Include="linq2db" />
</ItemGroup>
```

## Why This Configuration?

### 1. Trimmer-Safe Design
SQLiteXM is designed with iOS trimming in mind:
- Entity registration methods use `[DynamicallyAccessedMembers]` attributes
- Metadata preservation is automatic when you call `RegisterEntitiesAsync`
- All reflection-based operations are properly annotated

### 2. LinqToDB Rooting
The `<TrimmerRootAssembly Include="linq2db" />` setting keeps the entire LinqToDB assembly intact. This is the **recommended approach** because:
- **Reliability**: Eliminates any risk of trimming breaking LINQ queries at runtime
- **Minimal Impact**: Adds approximately 1.6 MB to your app size
- **Zero Maintenance**: No need to track or update trimming annotations as LinqToDB evolves

### 3. Partial Trim Mode
`TrimMode=partial` provides the best balance:
- Trims unused code from most assemblies
- Uses assembly-level trimming (safer than member-level)
- Works well with rooted assemblies like LinqToDB

## Alternative Configurations

### Option: Full Trimming (Not Recommended)
If app size is critical and you're willing to accept higher risk:

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0-ios' or '$(TargetFramework)' == 'net9.0-ios'">
  <PublishAot>true</PublishAot>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
</PropertyGroup>

<!-- No TrimmerRootAssembly - LinqToDB will be trimmed -->
```

⚠️ **Warning**: This configuration may cause runtime failures with complex LINQ queries. Full trimming of LinqToDB internals is not guaranteed to be safe. Only use this if you:
- Have comprehensive test coverage of all LINQ query patterns
- Are willing to maintain trimming annotations as LinqToDB updates
- Can accept potential runtime failures in production

## Required: Entity Registration

Regardless of trimming configuration, you **must** register all entity types at startup:

```csharp
await SxmDatabase.InitializeAsync(options);
await SxmDatabase.RegisterEntitiesAsync(
	typeof(Product),
	typeof(Customer),
	typeof(Order)
	// ... all your entity types
);
```

This ensures the trimmer preserves metadata for your entities.

## Testing iOS Builds

### 1. Local Testing
Build and deploy to a physical iOS device or simulator in Release mode:

```bash
dotnet publish -f net8.0-ios -c Release
```

### 2. Validation Checklist
- [ ] App launches successfully
- [ ] Database initialization completes
- [ ] Entity CRUD operations work
- [ ] LINQ queries execute correctly
- [ ] Multi-database routing functions as expected
- [ ] No trimming warnings in build output

### 3. Common Issues

**Issue**: `MissingMethodException` or `TypeLoadException` at runtime
- **Cause**: Missing entity registration or aggressive trimming
- **Solution**: Ensure all entity types are passed to `RegisterEntitiesAsync`

**Issue**: LINQ queries fail with reflection errors
- **Cause**: LinqToDB internals were trimmed
- **Solution**: Add `<TrimmerRootAssembly Include="linq2db" />` (recommended configuration)

**Issue**: Build warnings about trimming annotations
- **Cause**: Third-party libraries without trimming support
- **Solution**: These warnings from non-SQLiteXM libraries are usually safe to ignore, but test thoroughly

## App Store Submission

The recommended configuration (rooting LinqToDB) is fully compatible with Apple App Store submission:
- ✅ Passes Apple's static analysis
- ✅ AOT compilation produces native code
- ✅ No dynamic code generation at runtime
- ✅ Meets size requirements (typical impact: +1.6 MB)

## Size Impact

Typical app size impact with recommended configuration:
- **Rooted LinqToDB**: ~1.6 MB additional
- **Trimmed SQLiteXM + Microsoft.Data.Sqlite**: ~500 KB
- **Total SQLiteXM stack**: ~2.1 MB

This is considered negligible for modern iOS applications, especially compared to the reliability benefits.

## Performance

iOS AOT compilation with SQLiteXM provides:
- **Native Performance**: No JIT overhead
- **Fast Startup**: Pre-compiled queries
- **Predictable**: No runtime compilation delays
- **Memory Efficient**: Optimized for mobile devices

Benchmarks show SQLiteXM on iOS AOT performs comparably to native Swift SQLite code.

## Support

If you encounter iOS deployment issues:

1. **Check Registration**: Verify all entity types are registered with `RegisterEntitiesAsync`
2. **Review Configuration**: Ensure your `.csproj` matches the recommended settings above
3. **Test Incrementally**: Start with simple queries and gradually add complexity
4. **Build Logs**: Check for trimming warnings and address them

For issues specific to SQLiteXM, please file an issue on GitHub with:
- Your `.csproj` configuration
- Build output and any warnings
- Minimal reproduction code
- iOS version and device type

## Summary

**Recommended for Production iOS Apps:**
- ✅ Use `PublishAot=true` and `PublishTrimmed=true`
- ✅ Use `TrimMode=partial`
- ✅ Root LinqToDB with `<TrimmerRootAssembly Include="linq2db" />`
- ✅ Register all entity types at startup
- ✅ Test on physical devices in Release mode

This configuration prioritizes **reliability over minimal size savings**, ensuring your SQLiteXM-based app works consistently across all iOS devices and scenarios.
