# Sing Box Manager

A lightweight Windows manager for running `sing-box` and opening the local dashboard inside an embedded WebView2 window.

## Features

- Import and switch sing-box JSON configurations
- Show `external_controller`, UI port, and `secret`
- Copy dashboard secret to clipboard
- Start, stop, and restart the sing-box core
- Open `http://127.0.0.1:9095/ui/#/proxies` inside the app
- Follow the Windows light/dark app theme by default

## Privacy

Do not commit your personal `config.json`, `profiles/`, `cache.db`, or logs. These files may contain proxy nodes, passwords, tokens, or runtime state. They are ignored by `.gitignore`.

## Runtime Files

For a portable release package, place these files beside `SingBoxManager.exe`:

- `sing-box.exe`
- `sing-box.ico`
- `Microsoft.Web.WebView2.Core.dll`
- `Microsoft.Web.WebView2.WinForms.dll`
- `WebView2Loader.dll`

Users also need Microsoft Edge WebView2 Runtime installed. Most Windows 10/11 systems already include it.

## Build

This project is currently a single-file WinForms application. On Windows with .NET Framework compiler available:

```powershell
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 /utf8output /target:winexe /platform:x64 /win32icon:sing-box.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /out:SingBoxManager.exe SingBoxManager.cs
```

## Release

GitHub Actions builds and uploads a portable Windows zip when a version tag is pushed:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow downloads pinned runtime dependencies, builds `SingBoxManager.exe`, packages the portable files, and creates a GitHub Release automatically.

## Notes

`sing-box` is a third-party project by SagerNet. Check its license and release terms before redistributing the core binary.
