# Renders every page with sample results, light and dark, at 100% and a simulated 150%.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/screenshots'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $root 'src/Cleora.App/bin/Release/net48/Cleora.exe'
try { Set-DisplayResolution -Width 1920 -Height 1080 -Force -ErrorAction Stop } catch { Write-Host "Set-DisplayResolution: $_" }
Add-Type -AssemblyName System.Drawing

foreach ($theme in @('light', 'dark')) {
    foreach ($scale in @('1', '1.5')) {
        if ($theme -eq 'dark' -and $scale -ne '1') { continue }
        foreach ($page in @('Clean', 'LargeFiles', 'Duplicates', 'Apps', 'Startup', 'Settings')) {
            $file = Join-Path $out ("ui-{0}-{1}-{2}.png" -f $theme, $page.ToLower(), $scale.Replace('.', '_'))
            $process = Start-Process -FilePath $exe -ArgumentList @('--ui-screenshot', "`"$file`"", '--ui-scale', $scale, '--page', $page, '--theme', $theme) -NoNewWindow -Wait -PassThru
            if ($process.ExitCode -ne 0 -or -not (Test-Path $file)) { throw "screenshot $file failed ($($process.ExitCode))" }
            $image = [System.Drawing.Image]::FromFile($file)
            Write-Host "saved $file ($($image.Width) x $($image.Height))"
            $image.Dispose()
        }
    }
}
