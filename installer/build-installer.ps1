<#
.SYNOPSIS
  Publishes SpaceSensor Designer and builds a Windows installer with Inno Setup.

.DESCRIPTION
  1. dotnet publish  -> a self-contained single-file win-x64 build
  2. (optional) Authenticode-sign the published exe
  3. iscc            -> installer\dist\SpaceSensorDesigner-Setup-<ver>.exe
  4. (optional) Authenticode-sign the installer

  Code signing is opt-in. Provide EITHER a PFX or an installed-cert thumbprint:
    -PfxPath  .\mycert.pfx  -PfxPassword (Read-Host -AsSecureString)
    -CertThumbprint  "AB12...CD"
  With no signing args the build still succeeds — it just produces an unsigned installer.

.EXAMPLE
  installer\build-installer.ps1
  installer\build-installer.ps1 -PfxPath cert.pfx -PfxPassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [switch]$SelfContained = $true,
    [string]$PfxPath,
    [securestring]$PfxPassword,
    [string]$CertThumbprint,
    [string]$TimestampUrl  = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$repoRoot   = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src\SpaceSensorDesigner.App\SpaceSensorDesigner.App.csproj"
$publishDir = Join-Path $repoRoot "src\SpaceSensorDesigner.App\bin\$Configuration\net8.0-windows\$Runtime\publish"
$issFile    = Join-Path $PSScriptRoot "installer.iss"
$appExe     = Join-Path $publishDir "SpaceSensorDesigner.App.exe"

function Find-Tool([string[]]$candidates, [string]$name) {
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-Sign([string]$file) {
    if (-not $PfxPath -and -not $CertThumbprint) { return }  # signing not requested
    $signtool = Find-Tool @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\signtool.exe"
    ) "signtool"
    if (-not $signtool) {
        Write-Warning "signtool.exe not found (install the Windows SDK). Skipping signing of $file."
        return
    }
    $args = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    if ($CertThumbprint) { $args += @("/sha1", $CertThumbprint) }
    elseif ($PfxPath) {
        $args += @("/f", $PfxPath)
        if ($PfxPassword) {
            $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PfxPassword))
            $args += @("/p", $plain)
        }
    }
    $args += $file
    Write-Host "Signing $file ..." -ForegroundColor Cyan
    & $signtool @args
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for $file" }
}

Write-Host "==> Publishing $Configuration / $Runtime ..." -ForegroundColor Green
dotnet publish $appProject -c $Configuration -r $Runtime `
    -p:PublishSingleFile=true --self-contained $($SelfContained.ToString().ToLower())
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Invoke-Sign $appExe

$iscc = Find-Tool @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) "iscc"
if (-not $iscc) {
    throw "Inno Setup (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php, then re-run."
}

Write-Host "==> Compiling installer ..." -ForegroundColor Green
& $iscc "/DPublishDir=$publishDir" $issFile
if ($LASTEXITCODE -ne 0) { throw "ISCC failed." }

$setupExe = Get-ChildItem (Join-Path $PSScriptRoot "dist\SpaceSensorDesigner-Setup-*.exe") |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($setupExe) {
    Invoke-Sign $setupExe.FullName
    Write-Host "`nDone: $($setupExe.FullName)" -ForegroundColor Green
}
