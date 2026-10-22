<#
Ralph loop runner for the Mayan Stacker "Spins" pass.

You decide the limits, every time:
  -MaxHours       REQUIRED. Wall-clock budget. No new iteration starts once it is spent, and a running
                  iteration is killed at the deadline. The runner never waits or retries past it.
  -MaxIterations  Optional cap on iterations (default 12).
  -IterationMinutes  Cap for a single iteration (default 45), always clipped to the time left.
  -MaxBudgetUsd   Optional per-iteration API spend cap passed to claude.
  -Model          Optional model alias.

Stop at any time: create ralph\STOP (or press Ctrl+C).
If claude reports a usage/rate limit the runner STOPS. It does not sleep until the limit resets.

Example:  powershell -File ralph\run-ralph.ps1 -MaxHours 3 -MaxIterations 8
#>
param(
    [Parameter(Mandatory = $true)][double]$MaxHours,
    [int]$MaxIterations = 12,
    [int]$IterationMinutes = 45,
    [double]$MaxBudgetUsd = 0,
    [string]$Model = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if ($MaxHours -le 0) { throw 'MaxHours must be > 0' }

$deadline = (Get-Date).AddHours($MaxHours)
$logDir = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$stop = Join-Path $PSScriptRoot 'STOP'
$prompt = Join-Path $PSScriptRoot 'PROMPT.md'
$limitPattern = '(?i)usage limit|rate limit|limit reached|limit will reset|out of (extra )?usage|credit balance|quota'

$allowed = @(
    'Read', 'Write', 'Edit', 'Glob', 'Grep',
    'Bash(git status:*)', 'Bash(git diff:*)', 'Bash(git log:*)', 'Bash(git add:*)', 'Bash(git commit:*)', 'Bash(git show:*)', 'Bash(git check-ignore:*)',
    'Bash(powershell:*)', 'Bash(dotnet:*)', 'Bash(ls:*)', 'Bash(cat:*)', 'Bash(sed:*)', 'Bash(node:*)',
    'PowerShell(git status:*)', 'PowerShell(git diff:*)', 'PowerShell(git log:*)', 'PowerShell(git add:*)', 'PowerShell(git commit:*)', 'PowerShell(dotnet:*)'
)
$denied = @(
    'Bash(git push:*)', 'Bash(git reset:*)', 'Bash(git checkout:*)', 'Bash(git switch:*)', 'Bash(git rebase:*)', 'Bash(git clean:*)', 'Bash(git branch:*)',
    'PowerShell(git push:*)', 'PowerShell(git reset:*)', 'PowerShell(git checkout:*)', 'PowerShell(git clean:*)',
    'Agent', 'CronCreate', 'ScheduleWakeup', 'RemoteTrigger'
)

Write-Host "Ralph start. Budget $MaxHours h, deadline $($deadline.ToString('s')), max $MaxIterations iterations."
$i = 0
while ($true) {
    $i++
    if (Test-Path $stop)                 { Write-Host 'STOP file found. Ending.'; break }
    if ($i -gt $MaxIterations)           { Write-Host 'Iteration cap reached. Ending.'; break }
    $left = $deadline - (Get-Date)
    if ($left.TotalMinutes -lt 2)        { Write-Host 'Hour budget spent. Ending.'; break }

    $timeoutMin = [Math]::Min($IterationMinutes, [Math]::Floor($left.TotalMinutes))
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $out = Join-Path $logDir "iter-$i-$stamp.out.txt"
    $err = Join-Path $logDir "iter-$i-$stamp.err.txt"
    Write-Host "[$i/$MaxIterations] starting, timeout $timeoutMin min, $([Math]::Round($left.TotalMinutes)) min of budget left."

    $args = @('-p', '--permission-mode', 'acceptEdits', '--allowedTools', ($allowed -join ','), '--disallowedTools', ($denied -join ','))
    if ($Model)          { $args += @('--model', $Model) }
    if ($MaxBudgetUsd -gt 0) { $args += @('--max-budget-usd', "$MaxBudgetUsd") }

    $proc = Start-Process -FilePath 'claude' -ArgumentList $args -WorkingDirectory $root -NoNewWindow -PassThru `
        -RedirectStandardInput $prompt -RedirectStandardOutput $out -RedirectStandardError $err
    if (-not $proc.WaitForExit($timeoutMin * 60 * 1000)) {
        Write-Host "Iteration $i hit its time cap. Killing it."
        Stop-Process -Id $proc.Id -Force
        & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
        Add-Content (Join-Path $PSScriptRoot 'PROGRESS.md') "`n- $(Get-Date -Format s) iteration $i killed by runner time cap; check git status for partial work."
        continue
    }

    $text = (Get-Content $out -Raw -ErrorAction SilentlyContinue) + (Get-Content $err -Raw -ErrorAction SilentlyContinue)
    if ($text -match 'ROADMAP COMPLETE')  { Write-Host 'Completion promise seen. Ending.'; break }
    if ($proc.ExitCode -ne 0 -and $text -match $limitPattern) {
        Write-Host 'Usage/rate limit reported. Stopping, not waiting for reset.'
        Add-Content (Join-Path $PSScriptRoot 'PROGRESS.md') "`n- $(Get-Date -Format s) runner stopped: usage limit reported."
        break
    }
    if ($proc.ExitCode -ne 0) { Write-Host "Iteration $i exited $($proc.ExitCode); see $err. Ending to be safe."; break }
    Write-Host "[$i] done."
}
Write-Host "Ralph finished after $([Math]::Round(((Get-Date) - $deadline.AddHours(-$MaxHours)).TotalMinutes)) min."
