# One-time patch: WinAppSDK's MrtCore.PriGen.targets imports a task DLL that
# only ships with Visual Studio. Copy it from VS into the active dotnet SDK
# so `dotnet publish` can find it. Run this from an elevated PowerShell.

$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot\..

$sdkVer = (dotnet --version).Trim()
Write-Host "Active dotnet SDK: $sdkVer"

$vsRoots = @(
    "C:\Program Files\Microsoft Visual Studio\18\Community",
    "C:\Program Files\Microsoft Visual Studio\18\Professional",
    "C:\Program Files\Microsoft Visual Studio\18\Enterprise",
    "C:\Program Files\Microsoft Visual Studio\2022\Community",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise"
)
$vsAppx = $null
foreach ($root in $vsRoots) {
    if (-not (Test-Path "$root\MSBuild")) { continue }
    $vsAppx = Get-ChildItem "$root\MSBuild" -Filter AppxPackage -Recurse -Directory `
        | Where-Object { Test-Path "$($_.FullName)\Microsoft.Build.Packaging.Pri.Tasks.dll" } `
        | Select-Object -First 1
    if ($vsAppx) { break }
}
if (-not $vsAppx) { throw "Could not find PriGen DLL in any VS install" }

$vsVerDir = $vsAppx.Parent.Name
Write-Host "Source: $($vsAppx.FullName) (source verDir: $vsVerDir)"

# dotnet's default VisualStudioVersion can be different from the installed VS
# version (e.g. on a machine with VS 18, dotnet still resolves to v17.0). Copy
# to both v17.0 and v18.0 so the lookup succeeds regardless.
$verDirs = @('v17.0', 'v18.0') | Select-Object -Unique
foreach ($vd in $verDirs) {
    $target = "C:\Program Files\dotnet\sdk\$sdkVer\Microsoft\VisualStudio\$vd\AppxPackage"
    Write-Host "Target: $target"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item "$($vsAppx.FullName)\*" $target -Force -Recurse
}
Write-Host "Done."
