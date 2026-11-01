# Builds cleora-<version>-windows-x64-setup.exe (Inno Setup, per-user) from the folder package.ps1 staged.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
$stage = Join-Path $root 'artifacts/package/Cleora'
if (-not (Test-Path (Join-Path $stage 'Cleora.exe'))) { throw "run package.ps1 first: $stage has no Cleora.exe" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    choco install innosetup --no-progress -y | Out-Host
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}

$out = Join-Path $root 'artifacts'
& $iscc /Q "/DVersion=$version" "/DSource=$stage" "/DOutput=$out" "/DIcon=$(Join-Path $root 'src/Cleora.App/Assets/cleora.ico')" (Join-Path $root 'tools/installer/cleora.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
$setup = Join-Path $out "cleora-$version-windows-x64-setup.exe"
$size = (Get-Item $setup).Length
Write-Host ("{0}: {1:N1} MB" -f $setup, ($size / 1MB))
if ($size -gt 5MB) { throw "installer is $size bytes, over the 5 MB budget" }

# Silent install and uninstall on the runner: the installed exe must start its self-check.
$install = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=' -Wait -PassThru
if ($install.ExitCode -ne 0) { throw "silent install exited with $($install.ExitCode)" }
$installed = Join-Path $env:LOCALAPPDATA 'Programs\Cleora\Cleora.exe'
if (-not (Test-Path $installed)) { throw "installer did not put Cleora.exe in place" }
$roundtrip = Start-Process -FilePath $installed -ArgumentList '--ui-screenshot', "`"$(Join-Path $out 'installed-check.png')`"", '--page', 'Settings' -NoNewWindow -Wait -PassThru
if ($roundtrip.ExitCode -ne 0) { throw "installed Cleora.exe failed to run ($($roundtrip.ExitCode))" }
# "Cập nhật ngay" path: the same installer run silently over the installed app with /RELAUNCH=1 must start it again.
Get-Process Cleora -ErrorAction SilentlyContinue | Stop-Process -Force
# Not -Wait: it waits for every child too, and the app the installer starts again never exits on its own.
$update = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/RELAUNCH=1' -PassThru
if (-not $update.WaitForExit(180000)) { throw "update install did not finish in 3 minutes" }
if ($update.ExitCode -ne 0) { throw "update install exited with $($update.ExitCode)" }
$relaunched = $null
for ($i = 0; $i -lt 20 -and -not $relaunched; $i++) { Start-Sleep -Milliseconds 500; $relaunched = Get-Process Cleora -ErrorAction SilentlyContinue }
if (-not $relaunched) { throw "the update install did not start Cleora again" }
Write-Host "update install relaunched Cleora (pid $($relaunched[0].Id))"
$relaunched | Stop-Process -Force
Start-Sleep -Seconds 1
$uninstaller = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Programs\Cleora') -Filter 'unins*.exe' | Select-Object -First 1
$remove = Start-Process -FilePath $uninstaller.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
Start-Sleep -Seconds 3
if (Test-Path $installed) { throw "uninstall left Cleora.exe behind" }
Write-Host "installer OK: install, run, uninstall"
"setup=$setup" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
