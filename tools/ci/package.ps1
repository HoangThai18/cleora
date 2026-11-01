# Builds cleora-<version>-windows-x64-portable.zip: Cleora.exe, its DLLs and the license.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
$bin = Join-Path $root 'src/Cleora.App/bin/Release/net48'
$stage = Join-Path $root 'artifacts/package/Cleora'
Remove-Item -Recurse -Force (Join-Path $root 'artifacts/package') -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $stage 'licenses') | Out-Null

Copy-Item (Join-Path $bin '*') $stage -Include '*.exe', '*.dll', '*.config'
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'licenses/Cleora-LICENSE.txt')

$zip = Join-Path $root "artifacts/cleora-$version-windows-x64-portable.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $zip
Get-ChildItem $stage -Recurse | Select-Object FullName, Length | Format-Table -AutoSize | Out-String | Write-Host
$size = (Get-Item $zip).Length
Write-Host ("{0}: {1:N1} MB" -f $zip, ($size / 1MB))
if ($size -gt 5MB) { throw "portable zip is $size bytes, over the 5 MB budget" }
"zip=$zip" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
