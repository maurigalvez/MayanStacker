# Temple regression pass in Unity batch mode.
#
# Backs up the editor's PlayerPrefs (the pass starts from a fresh profile), runs
# LevelRegressionCli.Run headless, then restores the prefs exactly as they were.
# The Unity editor must be closed: batch mode can't open a project that's already open.
#
#   powershell -ExecutionPolicy Bypass -File Tools\run-level-regression.ps1 [-From 1] [-To 20] [-Attempts 3] [-AimError 0.12]
#
# Exit code: 0 pass, 1 regressions found, 2 harness failure/timeout.

param(
    [int]$From = 1,
    [int]$To = 20,
    [int]$Attempts = 3,
    [double]$AimError = 0.12,
    [int]$TimeoutMinutes = 120,
    [string]$UnityPath = ""
)

# Continue, not Stop: reg.exe reports success on stderr, which Windows PowerShell 5.1
# would turn into a terminating error. Failures are checked via $LASTEXITCODE instead.
$ErrorActionPreference = "Continue"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not $UnityPath) {
    $version = (Select-String -Path (Join-Path $project "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion: (.+)").Matches[0].Groups[1].Value.Trim()
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) { throw "Unity not found at $UnityPath (pass -UnityPath)." }
if (Test-Path (Join-Path $project "Temp\UnityLockfile")) {
    try {
        $fs = [System.IO.File]::Open((Join-Path $project "Temp\UnityLockfile"), 'Open', 'ReadWrite', 'None'); $fs.Close()
    } catch { throw "The project is open in the Unity editor. Close it first." }
}

$outDir = Join-Path $project "Logs\Regression"
New-Item -ItemType Directory -Force $outDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$log = Join-Path $outDir "unity-$stamp.log"

# PlayerPrefs backup (editor prefs live in the registry on Windows).
$prefsKey = "HKCU\Software\Unity\UnityEditor\Torogoz Games\Mayan Stacker"
$backup = Join-Path $outDir "playerprefs-backup-$stamp.reg"
& reg.exe query $prefsKey *> $null
$hadPrefs = ($LASTEXITCODE -eq 0)
if ($hadPrefs) {
    & reg.exe export $prefsKey $backup /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "PlayerPrefs backup failed; not running." }
    Write-Host "PlayerPrefs backed up to $backup"
}

$env:TAMAL_REGRESSION_PREFS_BACKED_UP = "1"
$startedAt = Get-Date
$code = 2
try {
    Write-Host "Running temples $From-$To (log: $log)..."
    $unityArgs = @("-batchmode", "-nographics", "-projectPath", "`"$project`"", "-logFile", "`"$log`"",
              "-executeMethod", "LevelRegressionCli.Run",
              "-regressionFrom", $From, "-regressionTo", $To, "-regressionAttempts", $Attempts, "-regressionAimError", $AimError,
              "-regressionOut", "`"$outDir`"")
    $proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $proc.Handle  # cache the handle so ExitCode is readable after exit
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Write-Warning "Timed out after $TimeoutMinutes min; killing Unity."
        $proc.Kill(); $proc.WaitForExit()
        $code = 2
    } else {
        $code = $proc.ExitCode
    }
}
finally {
    Remove-Item Env:TAMAL_REGRESSION_PREFS_BACKED_UP -ErrorAction SilentlyContinue
    & reg.exe delete $prefsKey /f *> $null
    if ($hadPrefs) {
        & reg.exe import $backup *> $null
        if ($LASTEXITCODE -eq 0) { Write-Host "PlayerPrefs restored." }
        else { Write-Warning "PlayerPrefs restore FAILED - import $backup by hand." }
    }
}

$report = Get-ChildItem $outDir -Filter "regression-*.md" | Sort-Object LastWriteTime -Descending | Where-Object { $_.LastWriteTime -gt $startedAt } | Select-Object -First 1
if ($report) { Write-Host "Report: $($report.FullName)" }
Write-Host "Exit code: $code"
exit $code
