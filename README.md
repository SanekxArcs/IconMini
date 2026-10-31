<h1 align="center">Icon Mini</h1>

<p align="center">
  <strong>Your image. Your little icon.</strong><br>
  An AI-generated companion tool for a Windhawk mod, made to create favicons and folder icons.<br>
  Just a fun experiment — don't take it too seriously.
</p>

<p align="center">
  <strong>Built with</strong><br>
  <a href="https://learn.microsoft.com/windows/apps/winui/winui3/"><img src="https://shieldcn.dev/badge/WinUI-3-violet.svg" alt="WinUI 3"></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://shieldcn.dev/badge/.NET-8-violet.svg?logo=dotnet" alt=".NET 8"></a>
  <a href="https://github.com/mono/SkiaSharp"><img src="https://shieldcn.dev/badge/SkiaSharp-blue.svg" alt="SkiaSharp"></a>
  <a href="https://github.com/wieslawsoltes/Svg.Skia"><img src="https://shieldcn.dev/badge/Svg.Skia-orange.svg" alt="Svg.Skia"></a>
</p>

<p align="center">
  <a href="#getting-started">Getting started</a> &middot;
  <a href="#usage">Usage</a> &middot;
  <a href="#build-from-source">Build from source</a> &middot;
  <a href="#project-map">Project map</a>
</p>

## What is Icon Mini?

Icon Mini is a Windows app that turns a single image into an **ICO file containing multiple sizes**. Choose a file, adjust its composition, and save an icon for your website or folder.

This project is entirely AI-generated and was made as a companion tool for a Windhawk mod. It's a casual side experiment, so don't take it too seriously.

All images are processed locally on your PC, without uploading them to the internet. The app interface is currently in Ukrainian.

## What you can do

| Tool | Highlights |
| --- | --- |
| **Website favicon** | Save `favicon.ico` with 16, 32, and 48 px sizes. |
| **Windows folder icon** | Save `folder.ico` with 16, 24, 32, 48, 64, 128, and 256 px sizes. |
| **Image import** | Choose a file through the system dialog or drag it into the window. |
| **Composition** | Fit the entire image or crop from the center, with padding from 0 to 30%. |
| **Background** | Keep transparency or choose a white or dark background. |
| **Preview** | Check an enlarged preview and the actual appearance at 16 px. |
| **Export** | Save your ICO through the system dialog and reveal the finished file in Explorer. |

## Getting started

Requires **Windows 10 version 1809 or later, or Windows 11, x64**.

1. Extract the **entire** `IconMini-win-x64.zip` archive into one folder.
2. Run `IconMini.exe` from that folder.
3. Choose an image and create your icon.

The DLLs and resource files next to the EXE are required, so move the whole folder together. You do not need to install .NET or Windows App Runtime separately: both are included in the self-contained build.

If you build the app yourself, the archive is created at `dist/IconMini-win-x64.zip`, and the executable is at `dist/IconMini/IconMini.exe`.

## Usage

The steps below describe the controls in English; the app displays Ukrainian labels.

1. Click the image selection button or drag a single file into the window.
2. Choose the destination: **website favicon** or **Windows folder icon**.
3. Adjust fit or crop, background, and padding.
4. Check the enlarged preview and the **16 px** preview.
5. Click the save button for `favicon.ico` or `folder.ico`.

### Website icon

Place `favicon.ico` in your website root and reference it in your HTML:

```html
<link rel="icon" href="/favicon.ico" sizes="16x16 32x32 48x48">
```

### Windows folder icon

Right-click a folder, go to **Properties → Customize → Change Icon → Browse**, and select `folder.ico`.

Keep the ICO in a permanent location: Windows uses that file path to display the folder icon.

## Supported formats

| Input files | Details |
| --- | --- |
| **PNG, JPEG, WebP, GIF, BMP, ICO** | Raster images and other formats recognized by Skia. Animated images use the first frame; JPEG images respect EXIF orientation. |
| **SVG** | Vector artwork is rendered separately for each icon size. External resources must be embedded in the file before import. |

The file size limit is **80 MB**; raster images also have a **64 megapixel** limit. HEIC, RAW, PSD, and PDF are not supported.

## Build from source

### Prerequisites

- Windows x64.
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).
- Internet access for the initial NuGet package download.

### Create a self-contained build

Open PowerShell in the project root:

```powershell
.\publish.ps1
```

The script creates a Release build for `win-x64`, runs the converter checks, and packages a ZIP that can be moved to another PC.

## Available commands

| Command | What it does |
| --- | --- |
| `.\publish.ps1` | Builds the self-contained app, checks the converter, and creates `dist/IconMini-win-x64.zip`. |
| `dotnet build -c Release -p:Platform=x64` | Compiles the project in the Release configuration. |
| `.\dist\IconMini\IconMini.exe` | Launches the published app. |
| `.\dist\IconMini\IconMini.exe --self-test` | Checks ICO structure, PNG frame decoding, transparency, composition, SVG, and invalid input. |
| `.\dist\IconMini\IconMini.exe --smoke-test` | Opens the window, loads a test SVG, renders both ICO variants, and saves a XAML screenshot. |

Run commands using `dist` after `publish.ps1`. Visual verification output is saved in the `verification` folder next to the application folder. The smoke test does not interact with system dialogs or test drag and drop; those actions require manual verification.

## Project map

```text
App.xaml             WinUI resources
Program.cs           App startup, test modes, and error logging
MainWindow.cs        Interface, image import, previews, and icon export
IconRenderer.cs      Image decoding, composition, and ICO generation
ConversionChecks.cs  Converter checks
IconMini.csproj      .NET, WinUI, and dependency configuration
app.manifest         Windows application manifest
publish.ps1          Self-contained build, checks, and ZIP packaging
```

## Contributing

1. Create a focused branch for your change.
2. Make your changes and run `.\publish.ps1`.
3. For interface changes, run `--smoke-test` and manually check file selection, drag and drop, and saving.
4. Update the README when you change features or startup instructions.
5. Include a short explanation and screenshots for visual changes.

## Links

- [WinUI 3 / Windows App SDK](https://learn.microsoft.com/windows/apps/winui/winui3/)
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SkiaSharp](https://github.com/mono/SkiaSharp)
- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia)

<p align="center">
  <sub>Made with love for the little details by <a href="https://github.com/SanekxArcs">SanekxArcs</a>.</sub><br>
  <sub>README badges powered by <a href="https://shieldcn.dev">shieldcn.dev</a>.</sub>
</p>
