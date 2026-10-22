# Builds the game assemblies outside Unity to catch compile errors.
# Strips <Analyzer> lines from the generated csproj files (they point at editor-only DLLs), builds, then restores them.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$projects = Get-ChildItem -Path $root -Filter '*.csproj' -File
$backup = @{}
foreach ($p in $projects) { $backup[$p.FullName] = [System.IO.File]::ReadAllBytes($p.FullName) }

try {
    foreach ($p in $projects) {
        $text = [System.IO.File]::ReadAllText($p.FullName)
        $stripped = ($text -split "`r?`n" | Where-Object { $_ -notmatch '<Analyzer Include=' }) -join "`r`n"
        [System.IO.File]::WriteAllText($p.FullName, $stripped)
    }
    dotnet build (Join-Path $root 'Assembly-CSharp-Editor.csproj') --nologo -v q -clp:ErrorsOnly
    $code = $LASTEXITCODE
}
finally {
    foreach ($k in $backup.Keys) { [System.IO.File]::WriteAllBytes($k, $backup[$k]) }
}

if ($code -eq 0) { Write-Output 'COMPILE OK: 0 errors' } else { Write-Output "COMPILE FAILED (exit $code)" }
exit $code
