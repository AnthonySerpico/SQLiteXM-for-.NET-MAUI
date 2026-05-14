# SQLiteXM Test Suite Refactor Script
# Converts old immediate API calls to new deferred DML pattern

$testPath = "C:\Users\ajser\source\repos\SQLiteXM\SQLiteXM.Tests"
$files = Get-ChildItem -Path $testPath -Filter "*.cs" -Recurse | Where-Object { $_.Name -notlike "*AssemblyInfo*" -and $_.Name -notlike "*GlobalUsings*" }

$totalInsert = 0
$totalUpdate = 0
$totalDelete = 0
$totalFiles = 0

foreach ($file in $files) {
    Write-Host "Processing: $($file.Name)"
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix InsertAsync
    $pattern1 = 'await ctx\.InsertAsync\(([^;]+)\);'
    $replacement1 = 'ctx.InsertOnSubmit($1); await ctx.SubmitChangesAsync();'
    $before = $content
    $content = $content -replace $pattern1, $replacement1
    if ($content -ne $before) {
        $count = ([regex]::Matches($before, $pattern1)).Count
        $totalInsert += $count
        Write-Host "  Fixed $count InsertAsync calls"
    }

    # Fix UpdateAsync
    $pattern2 = 'await ctx\.UpdateAsync\(([^;]+)\);'
    $replacement2 = 'ctx.UpdateOnSubmit($1); await ctx.SubmitChangesAsync();'
    $before = $content
    $content = $content -replace $pattern2, $replacement2
    if ($content -ne $before) {
        $count = ([regex]::Matches($before, $pattern2)).Count
        $totalUpdate += $count
        Write-Host "  Fixed $count UpdateAsync calls"
    }

    # Fix DeleteAsync
    $pattern3 = 'await ctx\.DeleteAsync\(([^;]+)\);'
    $replacement3 = 'ctx.DeleteOnSubmit($1); await ctx.SubmitChangesAsync();'
    $before = $content
    $content = $content -replace $pattern3, $replacement3
    if ($content -ne $before) {
        $count = ([regex]::Matches($before, $pattern3)).Count
        $totalDelete += $count
        Write-Host "  Fixed $count DeleteAsync calls"
    }

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $totalFiles++
    }
}

Write-Host ""
Write-Host "=== Summary ==="
Write-Host "Files updated: $totalFiles"
Write-Host "InsertAsync fixed: $totalInsert"
Write-Host "UpdateAsync fixed: $totalUpdate"
Write-Host "DeleteAsync fixed: $totalDelete"
$total = $totalInsert + $totalUpdate + $totalDelete
Write-Host "Total fixes: $total"
