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

# Include every .NET project, including the WebView Clean Architecture layers.
foreach ($root in @('src', 'tests')) {
    $targets += Get-ChildItem -LiteralPath (Join-Path $workspace $root) -Filter '*.csproj' -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(node_modules|bin|obj)\\' } |
        ForEach-Object { Join-Path $_.DirectoryName 'bin'; Join-Path $_.DirectoryName 'obj' }
}
$webRoot = Join-Path $workspace 'src\LSCode.Web'
$targets += Join-Path $webRoot 'dist'
$targets += Get-ChildItem -LiteralPath $webRoot -Filter '*.tsbuildinfo' -File | Select-Object -ExpandProperty FullName

# Keep current distributions, package caches, and source backups.
if (Test-Path -LiteralPath $artifactRoot) {
    $targets += Get-ChildItem -LiteralPath $artifactRoot -Directory | Where-Object {
        $_.Name -match '^(smoke-|webview-(final-)?smoke-|catalog-(compact-regression|regression|transfer)-|compact-check-)' -or
        $_.Name -in @('publish-ready', 'publish-save-fix', 'publish-web-link', 'publish-xml-save-fix', 'ui-a-validation')
    } | Select-Object -ExpandProperty FullName
}

$removedBytes = 0L
$removedCount = 0
foreach ($target in ($targets | Sort-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $resolved = (Resolve-Path -LiteralPath $target).ProviderPath
    if (-not $resolved.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the workspace: $resolved"
    }
    $entry = Get-Item -LiteralPath $resolved -Force
    $items = @($entry)
    if ($entry.PSIsContainer) { $items += @(Get-ChildItem -LiteralPath $resolved -Recurse -Force) }
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
Write-Output ('Removed {0} generated paths ({1:N1} MB).' -f $removedCount, ($removedBytes / 1MB))
