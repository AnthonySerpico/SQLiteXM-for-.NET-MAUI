# SQLiteXM Test Suite Refactor Script
# Converts old immediate API calls to new deferred DML pattern

$testPath = "C:\Users\ajser\source\repos\SQLiteXM\SQLiteXM.Tests"
$files = Get-ChildItem -Path $testPath -Filter "*.cs" -Recurse | Where-Object { $_.Name -notlike "*AssemblyInfo*" -and $_.Name -notlike "*GlobalUsings*" }

$stats = @{
    FilesProcessed = 0
    InsertAsyncFixed = 0
    UpdateAsyncFixed = 0
    DeleteAsyncFixed = 0
    BulkDeleteFixed = 0
}

foreach ($file in $files) {
    Write-Host "Processing: $($file.Name)" -ForegroundColor Cyan
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    $changes = 0

    # Pattern 1: ctx.InsertAsync(entity) → ctx.InsertOnSubmit(entity); await ctx.SubmitChangesAsync()
    # Match single-line InsertAsync calls
    $pattern1 = '(\s+)await\s+ctx\.InsertAsync\(([^;]+)\);'
    $replacement1 = '$1ctx.InsertOnSubmit($2);$1await ctx.SubmitChangesAsync();'
    if ($content -match $pattern1) {
        $content = $content -replace $pattern1, $replacement1
        $matches = [regex]::Matches($originalContent, $pattern1)
        $stats.InsertAsyncFixed += $matches.Count
        $changes += $matches.Count
    }

    # Pattern 2: ctx.UpdateAsync(entity) → ctx.UpdateOnSubmit(entity); await ctx.SubmitChangesAsync()
    $pattern2 = '(\s+)await\s+ctx\.UpdateAsync\(([^;]+)\);'
    $replacement2 = '$1ctx.UpdateOnSubmit($2);$1await ctx.SubmitChangesAsync();'
    if ($content -match $pattern2) {
        $content = $content -replace $pattern2, $replacement2
        $matches = [regex]::Matches($originalContent, $pattern2)
        $stats.UpdateAsyncFixed += $matches.Count
        $changes += $matches.Count
    }

    # Pattern 3: ctx.DeleteAsync(entity) → ctx.DeleteOnSubmit(entity); await ctx.SubmitChangesAsync()
    $pattern3 = '(\s+)await\s+ctx\.DeleteAsync\(([^;]+)\);'
    $replacement3 = '$1ctx.DeleteOnSubmit($2);$1await ctx.SubmitChangesAsync();'
    if ($content -match $pattern3) {
        $content = $content -replace $pattern3, $replacement3
        $matches = [regex]::Matches($originalContent, $pattern3)
        $stats.DeleteAsyncFixed += $matches.Count
        $changes += $matches.Count
    }

    # Pattern 4: table.Where(...).DeleteAsync() → needs SubmitChangesAsync after
    # This is trickier - bulk delete operations now require SubmitChangesAsync
    # We'll handle this by finding lines with .DeleteAsync() that don't have a preceding ctx.
    $pattern4 = '(\s+)(await\s+(?:ctx\.GetTable<\w+>\(\)|table|\w+)\.Where\([^)]+\)\.DeleteAsync\(\);)'
    $replacement4 = '$1$2$1await ctx.SubmitChangesAsync();'
    if ($content -match $pattern4) {
        $content = $content -replace $pattern4, $replacement4
        $matches = [regex]::Matches($originalContent, $pattern4)
        $stats.BulkDeleteFixed += $matches.Count
        $changes += $matches.Count
    }

    # Save changes if any were made
    if ($changes -gt 0) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $stats.FilesProcessed++
        Write-Host "  ✓ Fixed $changes issues" -ForegroundColor Green
    } else {
        Write-Host "  - No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Yellow
Write-Host "Files Processed: $($stats.FilesProcessed)" -ForegroundColor White
Write-Host "InsertAsync to InsertOnSubmit: $($stats.InsertAsyncFixed)" -ForegroundColor Green
Write-Host "UpdateAsync to UpdateOnSubmit: $($stats.UpdateAsyncFixed)" -ForegroundColor Green
Write-Host "DeleteAsync to DeleteOnSubmit: $($stats.DeleteAsyncFixed)" -ForegroundColor Green
Write-Host "Bulk Delete operations fixed: $($stats.BulkDeleteFixed)" -ForegroundColor Green
Write-Host "`nTotal fixes applied: $(($stats.InsertAsyncFixed + $stats.UpdateAsyncFixed + $stats.DeleteAsyncFixed + $stats.BulkDeleteFixed))" -ForegroundColor Cyan
