# Runs Cleora.exe --selftest on the Windows runner: a fake machine cleaned end to end (with a junction it must not
# follow), a read-only scan of the runner itself, apps, a startup switch flipped and back, the Recycle Bin.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$exe = Join-Path $root 'src/Cleora.App/bin/Release/net48/Cleora.exe'
$out = Join-Path $root 'artifacts/selftest'
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out 'selftest.txt'
$process = Start-Process -FilePath $exe -ArgumentList @('--selftest', "`"$out`"") -NoNewWindow -Wait -PassThru -RedirectStandardOutput $log
Get-Content $log | Write-Host
if ($process.ExitCode -ne 0) { throw "selftest failed ($($process.ExitCode))" }
if (-not (Select-String -Path $log -Pattern '^selftest OK' -Quiet)) { throw "selftest did not report OK" }
