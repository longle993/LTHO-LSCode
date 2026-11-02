# Generates the LS Code variant of the supplied SQL2014 template.
# Only adds DatabaseName (column 35) to event records and the column list.
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'SQL2014.tdf'
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne '05AA960C39B9AB61D3698BD269B3F917ACA01E18A851600D7164758A037BF1A1') { throw 'Template source changed; review its record format before regenerating.' }
$bytes = [IO.File]::ReadAllBytes($source)
$result = New-Object 'System.Collections.Generic.List[byte]'
for ($i = 0; $i -lt $bytes.Length;) {
    if ($i -ge 650 -and $i + 2 -lt $bytes.Length -and $bytes[$i] -eq 252 -and $bytes[$i + 1] -in @(255,251)) {
        $length = [int]$bytes[$i + 2]
        $result.Add($bytes[$i]); $result.Add($bytes[$i + 1]); $result.Add([byte]($length + 2))
        for ($j = 0; $j -lt $length; $j++) { $result.Add($bytes[$i + 3 + $j]) }
        $result.Add(35); $result.Add(0)
        $i += 3 + $length
    } else { $result.Add($bytes[$i]); $i++ }
}
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'SQL2014.WithDatabase.tdf'), $result.ToArray())
