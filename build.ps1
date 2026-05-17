$ErrorActionPreference = "Stop"

$RequiredFiles = @(
  "sing-box.exe",
  "Microsoft.Web.WebView2.Core.dll",
  "Microsoft.Web.WebView2.WinForms.dll",
  "WebView2Loader.dll"
)

$MissingFiles = $RequiredFiles | Where-Object {
  -not (Test-Path (Join-Path $PSScriptRoot $_))
}

if ($MissingFiles.Count -gt 0) {
  & (Join-Path $PSScriptRoot "restore-deps.ps1")
}

& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe `
  /nologo `
  /codepage:65001 `
  /utf8output `
  /target:winexe `
  /platform:x64 `
  /win32icon:sing-box.ico `
  /reference:System.Windows.Forms.dll `
  /reference:System.Drawing.dll `
  /reference:Microsoft.Web.WebView2.Core.dll `
  /reference:Microsoft.Web.WebView2.WinForms.dll `
  /out:SingBoxManager.exe `
  SingBoxManager.cs
