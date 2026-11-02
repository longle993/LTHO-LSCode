param([switch]$Test, [switch]$Publish, [switch]$Smoke, [string]$PublishDirectory = 'artifacts\publish')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'artifacts\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'artifacts\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet restore LSCode.sln -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build LSCode.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    dotnet tests\LSCode.Tests\bin\Release\net10.0\LSCode.Tests.dll
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
}
if ($Smoke) {
    $previousDataPath = $env:LSCODE_DATA_HOME
    try {
        $env:LSCODE_DATA_HOME = Join-Path $PSScriptRoot ('artifacts\smoke-' + [Guid]::NewGuid().ToString('N'))
        $appPath = Join-Path $PSScriptRoot 'src\LSCode.Shell\bin\Release\net10.0-windows\LSCode.exe'
        $smokeProcess = Start-Process -FilePath $appPath -ArgumentList '--smoke' -WindowStyle Hidden -PassThru
        if (-not $smokeProcess.WaitForExit(30000)) { $smokeProcess.Kill(); throw 'WPF smoke timed out.' }
        $resultFile = Join-Path $env:LSCODE_DATA_HOME 'smoke-result.txt'
        if (-not (Test-Path -LiteralPath $resultFile)) { throw 'WPF smoke did not produce a report.' }
        $report = Get-Content -LiteralPath $resultFile
        $report
        if ($report -match '^FAIL') { throw 'WPF smoke failed.' }
        Write-Output "WPF render/report: $env:LSCODE_DATA_HOME"
    }
    finally { $env:LSCODE_DATA_HOME = $previousDataPath }
}
if ($Publish) {
    dotnet publish src\LSCode.Shell\LSCode.Shell.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -o $PublishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'samples') -Destination $PublishDirectory -Recurse -Force
    Write-Output "Ready: $PublishDirectory\LSCode.exe (.NET 10 Desktop Runtime required)"
}
