# Generates the LS Code variant of the supplied SQL2014 template.
# Adds DatabaseName (column 35), RPC:Starting (11) and User Error Message (162).
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'SQL2014.tdf'
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne '05AA960C39B9AB61D3698BD269B3F917ACA01E18A851600D7164758A037BF1A1') { throw 'Template source changed; review its record format before regenerating.' }
$bytes = [IO.File]::ReadAllBytes($source)
$result = New-Object 'System.Collections.Generic.List[byte]'
$extraEventsWritten = $false
for ($i = 0; $i -lt $bytes.Length;) {
    if (-not $extraEventsWritten -and $i -ge 650 -and $i + 2 -lt $bytes.Length -and $bytes[$i] -eq 252 -and $bytes[$i + 1] -eq 251) {
        foreach ($eventId in @(11, 162)) {
            $columns = if ($eventId -eq 162) { @(1, 9, 10, 11, 6, 12, 14, 20, 35) } else { @(1, 9, 10, 11, 6, 12, 14, 35) }
            $result.Add([byte]252); $result.Add([byte]255); $result.Add([byte](2 + $columns.Count * 2))
            $result.Add([byte]($eventId -band 255)); $result.Add([byte](($eventId -shr 8) -band 255))
            foreach ($column in $columns) { $result.Add([byte]$column); $result.Add([byte]0) }
        }
        $extraEventsWritten = $true
    }
    if ($i -ge 650 -and $i + 2 -lt $bytes.Length -and $bytes[$i] -eq 252 -and $bytes[$i + 1] -in @(255,251)) {
        $length = [int]$bytes[$i + 2]
        $result.Add($bytes[$i]); $result.Add($bytes[$i + 1]); $result.Add([byte]($length + 2))
        for ($j = 0; $j -lt $length; $j++) { $result.Add($bytes[$i + 3 + $j]) }
        $result.Add(35); $result.Add(0)
        $i += 3 + $length
    } else { $result.Add($bytes[$i]); $i++ }
}
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'SQL2014.WithDatabase.tdf'), $result.ToArray())
