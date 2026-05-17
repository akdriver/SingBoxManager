$ErrorActionPreference = "Stop"

$SingBoxVersion = "1.13.11"
$WebView2Version = "1.0.2365.46"
$Root = $PSScriptRoot
$DepsDir = Join-Path $Root ".deps"

$RequiredFiles = @(
  "sing-box.exe",
  "Microsoft.Web.WebView2.Core.dll",
  "Microsoft.Web.WebView2.WinForms.dll",
  "WebView2Loader.dll"
)

$MissingFiles = $RequiredFiles | Where-Object {
  -not (Test-Path (Join-Path $Root $_))
}

if ($MissingFiles.Count -eq 0) {
  Write-Host "Dependencies are already restored."
  exit 0
}

New-Item -ItemType Directory -Force $DepsDir | Out-Null

$WebView2Package = Join-Path $DepsDir "Microsoft.Web.WebView2.$WebView2Version.zip"
$WebView2ExtractDir = Join-Path $DepsDir "Microsoft.Web.WebView2.$WebView2Version"

if (-not (Test-Path $WebView2Package)) {
  $WebView2Url = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$WebView2Version"
  Write-Host "Downloading Microsoft.Web.WebView2 $WebView2Version..."
  Invoke-WebRequest -Uri $WebView2Url -OutFile $WebView2Package
}

if (-not (Test-Path $WebView2ExtractDir)) {
  Expand-Archive -Path $WebView2Package -DestinationPath $WebView2ExtractDir -Force
}

$WebView2LibDir = Join-Path $WebView2ExtractDir "lib\net45"
if (-not (Test-Path $WebView2LibDir)) {
  $WebView2LibDir = Get-ChildItem (Join-Path $WebView2ExtractDir "lib") -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "Microsoft.Web.WebView2.WinForms.dll") } |
    Select-Object -First 1 -ExpandProperty FullName
}

if (-not $WebView2LibDir) {
  throw "Microsoft.Web.WebView2 assemblies were not found in the downloaded package."
}

Copy-Item (Join-Path $WebView2LibDir "Microsoft.Web.WebView2.Core.dll") $Root -Force
Copy-Item (Join-Path $WebView2LibDir "Microsoft.Web.WebView2.WinForms.dll") $Root -Force
Copy-Item (Join-Path $WebView2ExtractDir "runtimes\win-x64\native\WebView2Loader.dll") $Root -Force

$SingBoxZip = Join-Path $DepsDir "sing-box-$SingBoxVersion-windows-amd64.zip"
$SingBoxExtractDir = Join-Path $DepsDir "sing-box-$SingBoxVersion-windows-amd64"

if (-not (Test-Path $SingBoxZip)) {
  $SingBoxUrl = "https://github.com/SagerNet/sing-box/releases/download/v$SingBoxVersion/sing-box-$SingBoxVersion-windows-amd64.zip"
  Write-Host "Downloading sing-box $SingBoxVersion..."
  Invoke-WebRequest -Uri $SingBoxUrl -OutFile $SingBoxZip
}

if (-not (Test-Path $SingBoxExtractDir)) {
  Expand-Archive -Path $SingBoxZip -DestinationPath $SingBoxExtractDir -Force
}

$SingBoxExe = Get-ChildItem $SingBoxExtractDir -Recurse -Filter sing-box.exe | Select-Object -First 1
if (-not $SingBoxExe) {
  throw "sing-box.exe was not found in the downloaded archive."
}

Copy-Item $SingBoxExe.FullName (Join-Path $Root "sing-box.exe") -Force

Write-Host "Dependencies restored."
