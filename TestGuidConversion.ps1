# Quick test to verify GUID BLOB conversion works correctly
Write-Host "Testing GUID BLOB Conversion..." -ForegroundColor Cyan

# Create a test GUID
$testGuid = [Guid]::Parse("bdd426ef-e035-432e-8a04-1eea74686eeb")
Write-Host "Original GUID: $testGuid"

# Get native .NET bytes
$nativeBytes = $testGuid.ToByteArray()
Write-Host "Native bytes (hex): $([BitConverter]::ToString($nativeBytes))"

# Convert back
$roundtrip = [Guid]::new($nativeBytes)
Write-Host "Roundtrip GUID: $roundtrip"

if ($testGuid -eq $roundtrip) {
    Write-Host "✓ GUID roundtrip successful!" -ForegroundColor Green
} else {
    Write-Host "✗ GUID roundtrip FAILED!" -ForegroundColor Red
}

# Show the byte structure
Write-Host "`nByte structure:" -ForegroundColor Yellow
Write-Host "  First 4 bytes (little-endian int32): $([BitConverter]::ToString($nativeBytes[0..3]))"
Write-Host "  Next 2 bytes (little-endian int16):  $([BitConverter]::ToString($nativeBytes[4..5]))"
Write-Host "  Next 2 bytes (little-endian int16):  $([BitConverter]::ToString($nativeBytes[6..7]))"
Write-Host "  Last 8 bytes (big-endian):           $([BitConverter]::ToString($nativeBytes[8..15]))"
