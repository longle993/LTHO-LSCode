param([switch]$Test,[switch]$Smoke,[switch]$Publish,[switch]$SqlIntegration,[string]$PublishDirectory='artifacts/publish-webview')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot 'artifacts/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PSScriptRoot 'artifacts/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location src/LSCode.Web
try {
    npm ci --no-fund
    if($LASTEXITCODE -ne 0){throw 'Frontend restore failed.'}
    npm run build
    if($LASTEXITCODE -ne 0){throw 'Frontend build failed.'}
} finally {Pop-Location}
dotnet build src/LSCode.Desktop/LSCode.Desktop.csproj -c Release -m:1 -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Desktop build failed.'}
if($Test){
    dotnet run --project tests/LSCode.WebView.Tests/LSCode.WebView.Tests.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'WebView application checks failed.'}
    dotnet run --project tests/LSCode.Tests/LSCode.Tests.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'Existing Core checks failed.'}
}
if($SqlIntegration){
    dotnet run --project tests/LSCode.WebView.Tests/LSCode.WebView.Tests.csproj -c Release -- --sql --profiler
    if($LASTEXITCODE -ne 0){throw 'Local SQL/Profiler integration failed.'}
}
if($Smoke){
    $previousData=$env:LSCODE_DATA_HOME
    try {
        $env:LSCODE_DATA_HOME=Join-Path $PSScriptRoot ('artifacts/webview-smoke-'+[guid]::NewGuid().ToString('N'))
        $fixture=Join-Path $env:LSCODE_DATA_HOME 'workspace'
        New-Item -ItemType Directory -Force -Path $fixture | Out-Null
        New-Item -ItemType File -Path (Join-Path $env:LSCODE_DATA_HOME 'webview-smoke.marker') | Out-Null
        [IO.File]::WriteAllText((Join-Path $fixture 'fixture.xml'),'<root>Fixture</root>',[Text.UnicodeEncoding]::new($false,$true,$true))
        @{Theme='Dark';Workspace=$fixture} | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $env:LSCODE_DATA_HOME 'settings.json')
        $app=Join-Path $PSScriptRoot 'src/LSCode.Desktop/bin/Release/net10.0-windows/LSCode.WebView.exe'
        $process=Start-Process -FilePath $app -ArgumentList '--smoke' -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(60000)){ $process.Kill();throw 'WebView smoke timed out.' }
        $report=Join-Path $env:LSCODE_DATA_HOME 'webview-smoke.json'
        if(-not(Test-Path -LiteralPath $report)){throw 'WebView smoke did not produce a report.'}
        $result=Get-Content -Raw -Encoding utf8 -LiteralPath $report | ConvertFrom-Json
        if($result -is [string]){$result=$result | ConvertFrom-Json}
        if(-not $result.passed){throw ('WebView smoke failed: '+($result | ConvertTo-Json -Compress))}
        Write-Output ($result | ConvertTo-Json)
        $qaDirectory=Join-Path $PSScriptRoot 'artifacts/webview-ui'
        New-Item -ItemType Directory -Force -Path $qaDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $env:LSCODE_DATA_HOME 'webview.png') -Destination (Join-Path $qaDirectory 'native-webview.png') -Force
        Write-Output "Native screenshot: $env:LSCODE_DATA_HOME/webview.png"
    } finally {$env:LSCODE_DATA_HOME=$previousData}
}
if($Publish){
    dotnet publish src/LSCode.Desktop/LSCode.Desktop.csproj -c Release --no-restore -m:1 -o $PublishDirectory
    if($LASTEXITCODE -ne 0){throw 'WebView publish failed.'}
    Write-Output "Ready: $PublishDirectory/LSCode.WebView.exe"
}
