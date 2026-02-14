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
| **Theming**              | Avalonia FluentTheme (Dark)              | System accent color, custom color resources via ThemeDictionaries                |
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

- Service interfaces (`ICompressionService`, `IFfMpegService`, `IFfmpegManager`, `ISettingsService`, `IPowerService`, `IHistoryService`)
- Enums (`Codec`, `AfterCompletionAction`)
- Data types (`FfMpegOptions`, `FfMpegResult`, `CompressionResult`, `OutputOptions`, `AppSettings`)
- Utilities (`Formatting`)
- Custom attributes (`TooltipAttribute`)

**Recode.Infrastructure** (class library, platform-specific implementations)

- `FfmpegManager` - FFmpeg auto-download to `%LOCALAPPDATA%\Recode\`
- `FfMpegService` - FFmpeg process execution and progress parsing via CliWrap
- `CompressionService` - Orchestrates compression with output path resolution, temp file handling, and file replacement
- `SettingsService` - JSON-based persistent settings
- `PowerService` - Shutdown via `shutdown.exe`, sleep via P/Invoke `SetSuspendState`
- `HistoryService` - Tracks compressed files via partial SHA-256 hashing

**Recode** (Avalonia desktop executable)

- `App.axaml` - DI container setup, theme configuration with centralized color resources, global Window style
- `MainWindow.axaml` - Single window shell with Grid layout
- `AppDialog` - Reusable modal dialog (ShowError, AskYesNo, ShowCountdown)
- UserControls: `CompressionSettings`, `FileQueue`, `ControlBar`
- Custom controls: `EnumSelector` (reusable enum-to-ListBox/ComboBox)
- ViewModels split via partial classes:
  - `MainWindowViewModel.cs` - Settings properties, queue management, computed properties
  - `MainWindowViewModel.Compression.cs` - Compression loop, per-item cancellation
  - `MainWindowViewModel.Countdown.cs` - Post-compression countdown via AppDialog
  - `MainWindowViewModel.FfmpegInit.cs` - FFmpeg download/initialization
  - `MainWindowViewModel.Settings.cs` - Auto-save on property change
- `QueueItemViewModel` - Per-file observable state with Retry/Remove commands

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

- Drag-and-drop files onto the file queue
- Click "Drop files here or click to browse" text to open file picker (multi-select)
- Duplicate files are silently skipped
- Files previously compressed (tracked via history service) trigger a Yes/No dialog asking whether to add them anyway
- **Supported input**: .mp4, .mkv, .avi, .mov, .flv, .wmv, .webm, .ts, .m2ts

#### 4.1.2 Codec Options

| Codec      | Software Encoder | CRF Range |
|------------|-----------------|-----------|
| H.264      | libx264         | 0-51      |
| H.265/HEVC | libx265         | 0-51      |
| VP9        | libvpx-vp9      | 0-63      |

Each codec has a `[Tooltip]` attribute with usage guidance shown on hover.

Audio is always copied without re-encoding (`-c:a copy`).

#### 4.1.3 GPU Acceleration (planned)

GPU-accelerated encoding option for significantly faster processing (5-10x) at the cost of slightly larger output files:

| Codec | NVIDIA (NVENC)  | AMD (AMF)  | Intel (QSV) |
|-------|-----------------|------------|-------------|
| H.264 | h264_nvenc      | h264_amf   | h264_qsv    |
| H.265 | hevc_nvenc      | hevc_amf   | hevc_qsv    |

- Checkbox or toggle in compression settings to enable GPU encoding
- Auto-detect available GPU encoders by probing ffmpeg
- Fall back to software encoding if no GPU encoder is available for the selected codec
- Quality parameters differ per GPU vendor (NVENC uses `-cq`, AMF uses `-rc`/`-qp`, QSV uses `-global_quality`)
- VP9 has no widely supported GPU encoder — always uses software

#### 4.1.4 Output Mode

Two modes, toggled via a checkbox on the compression settings:

**Replace original**

- Encode to a temp file (`input.temp.mp4`) in the same directory as the source
- On success: atomic overwrite via `File.Move(temp, original, overwrite: true)`
- On failure/cancellation: temp file cleaned up by FfMpegService

**Output to folder**

- Encode to a user-selected output directory
- Original files are never modified
- If output path matches input path (file already in output folder), automatically uses temp file strategy to avoid read/write conflict

#### 4.1.5 Compression Settings

- **Codec selector**: `EnumSelector` in List mode (horizontal segmented control)
- **Quality slider**: 0-100 mapped inversely to codec-specific CRF ranges. Percentage label shown to the right of the slider
- **Output path**: Read-only TextBox that opens a folder picker on click
- **Replace files**: CheckBox that disables the output path when checked
- All settings disabled while compression is running

#### 4.1.6 Compression Queue & Progress

- File queue displayed as an `ItemsControl` inside a `ScrollViewer` with drag-and-drop support
- Per-file columns: filename, size display, progress bar, status text
- Per-file size display shows `3.5 MB → 945 KB` after completion
- Per-file action buttons: Retry (↻) and Remove (✕)
  - **Remove**: Always available. If item is processing, cancels its compression via per-item linked `CancellationTokenSource` and moves to next file
  - **Retry**: Available for Completed and Failed items. Resets progress to 0 and status to Pending
- "Drop files here or click to browse" text at the bottom of the queue, centered vertically when queue is empty
- Overall progress bar below the queue
- **Start button**: Always enabled when FFmpeg is ready. If compression is already running, the loop picks up any newly added pending items automatically
- **Cancel button**: Cancels all compression via main `CancellationToken`. The current file reverts to Pending, remaining files stay Pending

#### 4.1.7 Post-Compression Actions

Configured via dropdown in the control bar:

- Do nothing (default)
- Sleep the computer
- Shut down the computer

When sleep/shutdown is selected, a modal countdown dialog appears for 20 seconds with a Cancel button. Main window is brought to foreground (restored from minimized if needed). If not cancelled, the action is executed via `PowerService`.

#### 4.1.8 Duplicate Processing Prevention

- SHA-256 hash of file size + first 64KB + last 64KB for fast identification
- Hashes stored in `%APPDATA%\Recode\history.json`
- Hash is recorded after successful compression using the output file path
- When adding files, already-compressed files trigger a Yes/No dialog via `AppDialog.AskYesNo`

### 4.2 Command Line Arguments (planned)

- Accept file paths as command line arguments so files can be dragged onto the exe in Windows Explorer
- Files passed via CLI are added to the queue on startup
- Supports "Open with" from Windows Explorer context menu
- Same video extension filtering as drag-and-drop

---

### 4.3 FFmpeg Auto-Download

FFmpeg is **not bundled** with the app. It is downloaded on first launch.

#### 4.3.1 Download Source

[gyan.dev](https://www.gyan.dev/ffmpeg/builds/) essentials build (~45 MB zip).

- Stable URL: `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip`
- Contains all required codecs (libx264, libx265, libvpx-vp9) and GPU encoders

#### 4.3.2 Download Flow

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
                      +--> On failure: error dialog via AppDialog.ShowError, app closes
```

#### 4.3.3 Storage

```
%LOCALAPPDATA%\Recode\
  ffmpeg.exe
```

#### 4.3.4 Constraints

- Progress bar shown in MainWindow during download, compression disabled until FFmpeg is ready
- No admin privileges required — `%LOCALAPPDATA%` is user-writable
- All instances of the app share the same ffmpeg.exe

---

### 4.4 Settings

No settings window. All compression preferences are persisted automatically as the user changes them via `On<Property>Changed` partial methods.

#### 4.4.1 Persisted Values

- Selected codec
- Quality level
- Replace original (on/off)
- Output directory
- After-completion action

#### 4.4.2 Storage

- Settings file: `%APPDATA%\Recode\settings.json`
- Settings loaded in ViewModel constructor (assigned to backing fields to avoid triggering saves)
- Corrupted file resets to defaults

---

## 5. UI / UX Design

### 5.1 Window

- **Single window**, no navigation
- **Default size**: 600 x 375
- **Resizable**: Yes
- **Min size**: 600 x 270
- **Min/Max/Close**: Standard window chrome

### 5.2 Layout

MainWindow uses a Grid with `RowDefinitions="Auto,*,Auto,Auto"` and 16px margin:

```
+--[ Recode ]------------------------------------------+
|                                                       |
|  Codec:   [H.264] [H.265] [VP9]                      |
|  Quality: [====O==============] 50%                   |
|                     Output: [C:\...\Recode  ]         |
|                     [x] Replace files                 |
|                                                       |
|  +---------------------------------------------------+
|  | video1.mp4  1.2 GB → 450 MB  [||||||||||] Done ↻✕ |
|  | video2.mkv  800 MB           [||||      ] 40%  ↻✕ |
|  | video3.avi  2.1 GB           [          ] Pend ↻✕ |
|  |                                                    |
|  |         Drop files here or click to browse         |
|  +---------------------------------------------------+
|  [======================== overall ==================] |
|                                                       |
|  After completion: [Do nothing v]    [Cancel] [Start] |
+-------------------------------------------------------+
```

### 5.3 Dialogs

Reusable `AppDialog` class with static methods — no inline C# UI code needed at call sites:

- `AppDialog.ShowError(title, message)` - OK button
- `AppDialog.AskYesNo(title, message)` - Yes/No, returns bool
- `AppDialog.ShowCountdown(action, seconds)` - Countdown timer with Cancel, returns bool

Dialog uses `SizeToContent="Height"`, centers on owner window, inherits app theme automatically.

### 5.4 Theming

- **Theme**: Avalonia FluentTheme, forced Dark variant via `RequestedThemeVariant="Dark"` on Application
- **Accent color**: System accent color (no custom palette override)
- **Window background**: Applied globally via `Style Selector="Window"` in App.axaml
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
   - CancellationToken kills process on cancel (per-item linked token)
4. Parse time=HH:MM:SS.xx from stderr, report as progress / duration * 100
5. On non-zero exit code: return last stderr line as error
6. On cancellation: partial output file deleted by service
```

### 6.2 Argument Construction

```
H.264:  -y -nostdin -i "input" -c:v libx264 -crf {crf} -c:a copy "output"
H.265:  -y -nostdin -i "input" -c:v libx265 -crf {crf} -c:a copy "output"
VP9:    -y -nostdin -i "input" -c:v libvpx-vp9 -crf {crf} -b:v 0 -c:a copy "output"
```

Quality slider (0-100, higher = better) mapped to CRF (lower = better):
- H.264/H.265: `crf = 51 - (51 * quality / 100)`
- VP9: `crf = 63 - (63 * quality / 100)`

### 6.3 Progress Parsing

Regex on stderr: `time=(\d+:\d+:\d+\.\d+)` parsed via `TimeSpan.TryParse`, divided by probed duration. Uses `[GeneratedRegex]` for compile-time regex.

### 6.4 Result Types

- `FfMpegResult(bool Success, string? ErrorMessage)` - raw FFmpeg process result
- `CompressionResult(bool Success, string? ErrorMessage, long OutputSize, string OutputPath)` - orchestrated result with output file info

---

## 7. Error Handling

| Scenario                    | Handling                                                     |
|-----------------------------|--------------------------------------------------------------|
| FFmpeg not found            | Auto-download on first launch, compression disabled until ready |
| FFmpeg download fails       | Error dialog via AppDialog, app closes                       |
| FFmpeg crashes mid-encode   | Mark file as Failed, continue to next file in queue          |
| Output path = input path    | Automatic temp file strategy to avoid read/write conflict    |
| Replace mode: move fails    | Atomic overwrite via `File.Move(overwrite: true)`            |
| Invalid input file          | FFmpeg reports error via non-zero exit, marked as Failed     |
| Settings file corrupt       | Reset to defaults                                            |
| Item removed while encoding | Per-item CancellationToken cancelled, loop skips to next     |

---

## 8. Packaging & Distribution

FFmpeg is **not bundled** — it is downloaded on first launch (see 4.3).

- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- No install needed, run from any folder
- ReadyToRun enabled for faster startup
- Trimming disabled (breaks reflection-based code)
- Settings and history in `%APPDATA%\Recode\`
- FFmpeg in `%LOCALAPPDATA%\Recode\`
