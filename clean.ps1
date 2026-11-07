[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$artifactRoot = Join-Path $workspace 'artifacts'
$targets = @(
    'src\LSCode.Core\bin', 'src\LSCode.Core\obj',
    'src\LSCode.Shell\bin', 'src\LSCode.Shell\obj',
    'src\LSCode.TraceHost\bin',
    'tests\LSCode.Tests\bin', 'tests\LSCode.Tests\obj'
) | ForEach-Object { Join-Path $workspace $_ }

# Keep current distributions, package caches, and source backups.
if (Test-Path -LiteralPath $artifactRoot) {
    $targets += Get-ChildItem -LiteralPath $artifactRoot -Directory | Where-Object {
        $_.Name -match '^(smoke-|catalog-(compact-regression|regression|transfer)-|compact-check-)' -or
        $_.Name -in @('publish-ready', 'publish-save-fix', 'publish-web-link', 'publish-xml-save-fix', 'ui-a-validation')
    } | Select-Object -ExpandProperty FullName
}

$removedBytes = 0L
$removedCount = 0
foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $resolved = (Resolve-Path -LiteralPath $target).ProviderPath
    if (-not $resolved.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the workspace: $resolved"
    }
    $items = @(Get-Item -LiteralPath $resolved -Force) + @(Get-ChildItem -LiteralPath $resolved -Recurse -Force)
    if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw "Cleanup target contains a junction or symbolic link: $resolved"
    }
    $size = ($items | Where-Object { -not $_.PSIsContainer } | Measure-Object Length -Sum).Sum
    if ($PSCmdlet.ShouldProcess($resolved, 'Remove generated files')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
        $removedBytes += $size
        $removedCount++
    }
}
Write-Output ('Removed {0} directories ({1:N1} MB).' -f $removedCount, ($removedBytes / 1MB))
