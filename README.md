# Sing Box Manager

A lightweight Windows manager for running `sing-box` and opening the local dashboard inside an embedded WebView2 window.

## Features

- Import and switch sing-box JSON configurations
- Import and update sing-box JSON configurations from a URL
- Show `external_controller`, UI port, and `secret`
- Copy dashboard secret to clipboard
- Start, stop, and restart the sing-box core
- Open `http://127.0.0.1:9095/ui/#/proxies` inside the app
- Follow the Windows light/dark app theme by default

## Privacy

Do not commit your personal `config.json`, `profiles/`, `cache.db`, or logs. These files may contain proxy nodes, passwords, tokens, or runtime state. They are ignored by `.gitignore`.

URL configuration sources are stored under `profiles/` together with the last update timestamp, so they are treated as private runtime data too.

## Runtime Files

For a portable release package, place these files beside `SingBoxManager.exe`:

- `sing-box.exe`
- `sing-box.ico`
- `Microsoft.Web.WebView2.Core.dll`
- `Microsoft.Web.WebView2.WinForms.dll`
- `WebView2Loader.dll`

Users also need Microsoft Edge WebView2 Runtime installed. Most Windows 10/11 systems already include it.

## URL Config Updates

On the config page, paste a direct `http` or `https` URL to a sing-box JSON config and click **下载导入**. The app downloads the file, validates it with `sing-box check`, confirms the local dashboard port is present, then replaces `config.json`.

After a successful URL import, click **更新** to download from the saved URL again. The config page shows the last successful update time.

## Build

This project is currently a single-file WinForms application. On Windows with .NET Framework compiler available, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` restores missing local dependencies first, then compiles `SingBoxManager.exe`.

To restore dependencies without building:

```powershell
powershell -ExecutionPolicy Bypass -File .\restore-deps.ps1
```

The compiled app embeds `app.manifest` and requests administrator privileges at startup, so Windows shows a UAC prompt when users launch `SingBoxManager.exe`.

## Release

GitHub Actions builds and uploads a portable Windows zip when a version tag is pushed:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow downloads pinned runtime dependencies, builds `SingBoxManager.exe`, packages the portable files, and creates a GitHub Release automatically.

## Notes

`sing-box` is a third-party project by SagerNet. Check its license and release terms before redistributing the core binary.
