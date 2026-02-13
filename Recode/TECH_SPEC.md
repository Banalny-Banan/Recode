# Recode - Technical Specification

## 1. Overview

Recode is a Windows desktop video compression utility. Users drop video files into the app, pick a codec and quality level, and compress — either replacing the originals in-place or outputting to a separate folder. FFmpeg is downloaded automatically on first launch.

---

## 2. Technology Stack

| Layer                    | Technology                               | Rationale                                                                        |
|--------------------------|------------------------------------------|----------------------------------------------------------------------------------|
| **Runtime**              | .NET 9                                   | Best performance, single-file publish                                            |
| **UI Framework**         | Avalonia UI 11                           | Cross-platform XAML, first-class Rider support (previewer, Hot Reload, templates) |
| **MVVM**                 | CommunityToolkit.Mvvm                    | Source-generated, lightweight, official Microsoft toolkit                         |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection | Standard .NET DI container                                                       |
| **FFmpeg wrapper**       | CliWrap                                  | Piped output, cancellation tokens, robust process lifecycle                      |
| **Serialization**        | System.Text.Json                         | Built-in, source-generated                                                       |
| **Theming**              | Avalonia FluentTheme                     | Built-in light/dark themes with custom accent colors via Palettes                |
| **Packaging**            | Single-file self-contained               | Zero-install portable executable                                                 |

---

## 3. Architecture

### 3.1 Project Structure

```
Recode.sln
|
+-- Recode/                      # Avalonia app (entry point, views, DI setup)
+-- Recode.Core/                 # Interfaces, enums, models, utilities
+-- Recode.Infrastructure/       # FFmpeg integration, file system, settings persistence
```

### 3.2 Layer Responsibilities

**Recode.Core** (class library, no UI or platform dependencies)

- Service interfaces (`ICompressionService`, `IFfMpegService`, `IFfmpegManager`, `ISettingsService`)
- Enums (`Codec`, `AfterCompletionAction`)
- Data types (`CompressionOptions`, `CompressionResult`, `OutputOptions`, `AppSettings`)
- Utilities (`Formatting`)
- Custom attributes (`TooltipAttribute`)

**Recode.Infrastructure** (class library, platform-specific implementations)

- `FfmpegManager` - FFmpeg auto-download to `%LOCALAPPDATA%\Recode\`
- `FfMpegService` - FFmpeg process execution and progress parsing via CliWrap
- `CompressionService` - Orchestrates compression with output path resolution and file replacement
- `SettingsService` - JSON-based persistent settings

**Recode** (Avalonia desktop executable)

- `App.axaml` - DI container setup, theme configuration with centralized color resources
- `MainWindow.axaml` - Single window shell with Grid layout
- UserControls: `FileDropZone`, `CompressionControls`, `FileQueue`, `ControlBar`
- Custom controls: `EnumSelector` (reusable enum-to-ListBox/ComboBox)
- `MainWindowViewModel` (with `MainWindowViewModel.Settings.cs` partial)
- `QueueItemViewModel`

### 3.3 Dependency Flow

```
Views --> ViewModels --> Core Interfaces <-- Infrastructure Implementations
                              ^
                              |
                         DI Container (registered in App.axaml.cs)
```

Views depend on ViewModels. ViewModels depend on Core interfaces only. Infrastructure implements Core interfaces. Core has zero dependencies on Infrastructure or UI.

---

## 4. Features

### 4.1 Video Compression

#### 4.1.1 File Input

- Drag-and-drop files onto the drop zone (entire zone is a clickable button)
- Click the drop zone to open file picker (multi-select)
- Duplicate files are silently skipped
- **Supported input**: .mp4, .mkv, .avi, .mov, .flv, .wmv, .webm, .ts, .m2ts

#### 4.1.2 Codec Options

| Codec      | Library    | CRF Range | Encoder     |
|------------|------------|-----------|-------------|
| H.264      | libx264    | 0-51      | libx264     |
| H.265/HEVC | libx265    | 0-51      | libx265     |
| VP9        | libvpx-vp9 | 0-63      | libvpx-vp9  |
| AV1        | libaom-av1 | 0-63      | libaom-av1  |

Each codec has a `[Tooltip]` attribute with usage guidance shown on hover.

Audio is always copied without re-encoding (`-c:a copy`).

#### 4.1.3 Output Mode

Two modes, toggled via a checkbox on the compression controls:

**Replace original**

- Encode to a temp file (`input.mp4.temp`) in the same directory as the source
- On success: atomic overwrite via `File.Move(temp, original, overwrite: true)`
- On failure/cancellation: temp file cleaned up by FfMpegService

**Output to folder**

- Encode to a user-selected output directory
- Original files are never modified
- Existing files in the output folder are overwritten silently

#### 4.1.4 Compression Controls

- **Codec selector**: `EnumSelector` in List mode (horizontal segmented control)
- **Quality slider**: 0-100 mapped inversely to codec-specific CRF ranges. Tooltip shows label (Smallest / Smaller / Balanced / High Quality / Lossless)
- **Output path**: Read-only TextBox that opens a folder picker on click
- **Replace files**: CheckBox that disables the output path when checked

#### 4.1.5 Compression Queue & Progress

- File queue displayed as an `ItemsControl` with per-file progress bar and status icon
- Status icons: dash (Pending), circular arrow (Processing), checkmark (Completed), X (Failed)
- Per-file size display shows `3.5 MB → 945 KB` after completion
- Overall progress bar below the queue
- **Start button**: Always enabled when FFmpeg is ready. If compression is already running, the loop picks up any newly added pending items automatically
- **Cancel button**: Cancels the current FFmpeg process via `CancellationToken`. The current file reverts to Pending, remaining files stay Pending

#### 4.1.6 Post-Compression Actions

- Do nothing (default)
- Sleep the computer
- Shut down the computer

#### 4.1.7 Duplicate Processing Prevention (planned)

- Store a list of file content hashes (SHA-256 of first/last N bytes) of previously compressed files
- Saved to `%APPDATA%\Recode\history.json` alongside settings
- When adding files to the queue, check against history and warn/skip already-processed files
- Hash is recorded after successful compression

---

### 4.2 FFmpeg Auto-Download

FFmpeg is **not bundled** with the app. It is downloaded on first launch.

#### 4.2.1 Download Source

[gyan.dev](https://www.gyan.dev/ffmpeg/builds/) essentials build (~45 MB zip).

- Stable URL: `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip`
- Contains all required codecs (libx264, libx265, libvpx-vp9, libaom-av1)

#### 4.2.2 Download Flow

```
App launch
  |
  +--> IFfmpegManager.EnsureAvailableAsync()
         |
         +--> Found at %LOCALAPPDATA%\Recode\ffmpeg.exe? --> yes --> done
         |
         +--> no --> Download with progress bar in MainWindow
                      - Stream zip to %TEMP% via HttpClient (with cancellation support)
                      - Extract only ffmpeg.exe from zip
                      - Move to %LOCALAPPDATA%\Recode\
                      - Clean up temp zip
                      |
                      +--> On failure: error dialog, app closes
```

#### 4.2.3 Storage

```
%LOCALAPPDATA%\Recode\
  ffmpeg.exe
```

#### 4.2.4 Constraints

- Progress bar shown in MainWindow during download, compression disabled until FFmpeg is ready
- No admin privileges required — `%LOCALAPPDATA%` is user-writable
- All instances of the app share the same ffmpeg.exe

---

### 4.3 Settings

No settings window. All compression preferences are persisted automatically as the user changes them via `On<Property>Changed` partial methods.

#### 4.3.1 Persisted Values

- Selected codec
- Quality level
- Replace original (on/off)
- Output directory
- After-completion action

#### 4.3.2 Storage

- Settings file: `%APPDATA%\Recode\settings.json`
- Settings loaded in ViewModel constructor (assigned to backing fields to avoid triggering saves)
- Corrupted file resets to defaults

---

## 5. UI / UX Design

### 5.1 Window

- **Single window**, no navigation
- **Default size**: 600 x 400
- **Resizable**: Yes
- **Min/Max/Close**: Standard window chrome

### 5.2 Layout

MainWindow uses a Grid with `RowDefinitions="Auto,Auto,*,Auto,Auto"` and 16px margin:

```
+--[ Recode ]------------------------------------------+
|                                                       |
|  +---------------------------------------------------+
|  |  Drop files here or click to browse                |
|  +---------------------------------------------------+
|                                                       |
|  Codec:   [H.264] [H.265] [VP9] [AV1]               |
|  Quality: [====O==============] (tooltip: Balanced)   |
|                     Output: [C:\...\Recode  ]  [x] Replace |
|                                                       |
|  +---------------------------------------------------+
|  | video1.mp4   1.2 GB → 450 MB  [||||||||||] ✓      |
|  | video2.mkv   800 MB           [||||      ] ↻      |
|  | video3.avi   2.1 GB           [          ] —      |
|  +---------------------------------------------------+
|  [======================== overall ==================] |
|                                                       |
|  After completion: [Do nothing v]    [Cancel] [Start] |
+-------------------------------------------------------+
```

### 5.3 Theming

- **Theme**: Avalonia FluentTheme with custom accent colors via `FluentTheme.Palettes`
- **Color resources**: Centralized in `App.axaml` with `ThemeDictionaries` for Light/Dark variants
- **Custom resources**: AppBackgroundBrush, AppSurfaceBrush, AppInsetBrush, AppTextPrimaryBrush, AppTextSecondaryBrush, AppTextDisabledBrush, AppTextOnAccentBrush, AppBorderBrush, AppErrorBrush, AppSuccessBrush, AppWarningBrush

---

## 6. FFmpeg Integration

### 6.1 Process Lifecycle (via CliWrap)

```
1. Probe duration: ffmpeg -i input -hide_banner (parse Duration from stderr)
2. Build arguments from codec + quality
3. Start process via CliWrap:
   - -y -nostdin flags (overwrite, don't hang)
   - Stderr piped for progress parsing and error capture
   - CancellationToken kills process on cancel
4. Parse time=HH:MM:SS.xx from stderr, report as progress / duration * 100
5. On non-zero exit code: return last stderr line as error
6. On cancellation: partial output file deleted by service
```

### 6.2 Argument Construction

```
H.264:  -y -nostdin -i "input" -c:v libx264 -crf {crf} -c:a copy "output"
H.265:  -y -nostdin -i "input" -c:v libx265 -crf {crf} -c:a copy "output"
VP9:    -y -nostdin -i "input" -c:v libvpx-vp9 -crf {crf} -b:v 0 -c:a copy "output"
AV1:    -y -nostdin -i "input" -c:v libaom-av1 -crf {crf} -b:v 0 -c:a copy "output"
```

Quality slider (0-100, higher = better) mapped to CRF (lower = better):
- H.264/H.265: `crf = 51 - (51 * quality / 100)`
- VP9/AV1: `crf = 63 - (63 * quality / 100)`

### 6.3 Progress Parsing

Regex on stderr: `time=(\d+:\d+:\d+\.\d+)` parsed via `TimeSpan.TryParse`, divided by probed duration. Uses `[GeneratedRegex]` for compile-time regex.

---

## 7. Error Handling

| Scenario                  | Handling                                                     |
|---------------------------|--------------------------------------------------------------|
| FFmpeg not found          | Auto-download on first launch, compression disabled until ready |
| FFmpeg download fails     | Error dialog, app closes                                     |
| FFmpeg crashes mid-encode | Mark file as Failed, continue to next file in queue          |
| Replace mode: move fails  | Atomic overwrite via `File.Move(overwrite: true)`            |
| Invalid input file        | FFmpeg reports error via non-zero exit, marked as Failed     |
| Settings file corrupt     | Reset to defaults                                            |

---

## 8. Packaging & Distribution

FFmpeg is **not bundled** — it is downloaded on first launch (see 4.2).

- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- No install needed, run from any folder
- ReadyToRun enabled for faster startup
- Trimming disabled (breaks reflection-based code)
- Settings in `%APPDATA%\Recode\`
- FFmpeg in `%LOCALAPPDATA%\Recode\`