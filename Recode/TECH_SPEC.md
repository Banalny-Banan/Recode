# Recode - Technical Specification

## 1. Overview

Recode is a Windows desktop video compression utility. Users drop video files into the app, pick a codec and quality level, and compress — either replacing the originals in-place or outputting to a separate folder. FFmpeg is downloaded automatically on first launch.

---

## 2. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| **Runtime** | .NET 9 | Best performance, single-file publish |
| **UI Framework** | Avalonia UI 11 | Cross-platform XAML, first-class Rider support (previewer, Hot Reload, templates) |
| **MVVM** | CommunityToolkit.Mvvm | Source-generated, lightweight, official Microsoft toolkit |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection | Standard .NET DI container |
| **Configuration** | Microsoft.Extensions.Configuration + JSON | Structured settings with hot-reload |
| **Logging** | Serilog (file + debug sinks) | Structured logging |
| **FFmpeg wrapper** | CliWrap | Piped output, cancellation tokens, robust process lifecycle |
| **Serialization** | System.Text.Json | Built-in, source-generated |
| **Theming** | Semi.Avalonia | Fluent-style theme pack with light/dark modes and accent colors |
| **Packaging** | Single-file self-contained | Zero-install portable executable |

---

## 3. Architecture

### 3.1 Project Structure

```
Recode.sln
|
+-- src/
|   +-- Recode/                      # Avalonia app (entry point, views, DI setup)
|   +-- Recode.Core/                 # Business logic, models, interfaces
|   +-- Recode.Infrastructure/       # FFmpeg integration, file system, OS APIs
|
```

### 3.2 Layer Responsibilities

**Recode.Core** (class library, no UI or platform dependencies)
- Domain models (`CompressionJob`, `CompressionSettings`)
- Service interfaces (`ICompressionService`, `IFfmpegManager`, `ISettingsService`)
- Enums (`CodecType`, `SpeedPreset`, `OutputMode`, `PostAction`)
- Progress reporting contracts

**Recode.Infrastructure** (class library, platform-specific implementations)
- `FfmpegManager` - FFmpeg auto-download, version detection, path resolution
- `FfmpegCompressionService` - FFmpeg process management via CliWrap
- `SettingsService` - JSON-based persistent settings
- `PowerManagementService` - Sleep/shutdown via Windows APIs

**Recode** (Avalonia desktop executable)
- `App.axaml` - DI container setup, theme configuration
- `MainWindow.axaml` - Single window with title bar gear icon for settings
- `CompressionView.axaml` - Main (and only) view
- `SettingsWindow.axaml` - Separate window opened from gear icon
- `CompressionViewModel`, `SettingsViewModel`
- Converters, styles, resources

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
- Drag-and-drop files onto the compression area
- "Browse" button opens file picker (multi-select)
- Command-line arguments: `Recode.exe file1.mp4 file2.mkv`
- **Supported input**: .mp4, .mkv, .avi, .mov, .flv, .wmv, .webm, .ts, .m2ts

#### 4.1.2 Codec Options

| Codec | Library | CRF Range | Default CRF | Container | Preset |
|---|---|---|---|---|---|
| H.264 | libx264 | 0-51 | 23 | .mp4 | medium |
| H.265/HEVC | libx265 | 0-51 | 28 | .mp4 | medium |
| VP9 | libvpx-vp9 | 0-63 | 31 | .webm | N/A (quality mode) |
| AV1 | libsvtav1 | 0-63 | 35 | .mp4 | preset 6 |

#### 4.1.3 Output Mode

Two modes, toggled via a control on the compression page:

**Replace original**
- Encode to a temp file in the same directory as the source
- On success: delete original, rename temp to original filename
- On failure: delete temp, original is untouched
- If the codec changes the container (e.g., VP9 → .webm), the replacement gets the new extension and the original is deleted

**Output to folder**
- Encode to a configurable output directory (default: `Videos\Recode\`)
- Original files are never modified
- If a file with the same name already exists in output, auto-rename with suffix (`_1`, `_2`, ...)

#### 4.1.4 Compression Controls
- **Quality slider**: Labeled scale from "Visually Lossless" to "Smallest File", mapped to codec-specific CRF ranges
- **Preset selector**: Ultrafast / Fast / Medium / Slow (encoding speed vs compression ratio)
- **Audio options**: Copy original / Re-encode AAC 128k / Re-encode AAC 256k / Strip audio
- **Output mode**: Replace original / Output to folder (see 4.1.3)
- **Output directory**: Folder picker, only visible when output mode is "Output to folder"

#### 4.1.5 Compression Queue & Progress
- File queue displayed as a list with per-file status (Pending / Encoding / Done / Failed)
- Per-file progress bar with percentage, elapsed time, and ETA
- Overall progress bar showing files completed / total
- Estimated output file size (based on current bitrate)
- **Cancel**: Stop current file with option to skip or abort entire queue
- **Pause/Resume**: Suspend FFmpeg process

#### 4.1.6 Post-Compression Actions
- Do nothing (default)
- Sleep
- Shutdown
- Open output folder
- Play notification sound

#### 4.1.7 Completion Summary
- Notification banner showing: files processed, total size saved, total time, per-file breakdown (original size -> compressed size, ratio)

---

### 4.2 FFmpeg Auto-Download

FFmpeg is **not bundled** with the app. It is downloaded on first launch, keeping the distributed app small.

#### 4.2.1 Download Source

**Primary**: [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) essentials build (~45 MB zip).
- Stable URL: `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip`
- Contains all required codecs (libx264, libx265, libvpx-vp9, libsvtav1)
- Linked from the official ffmpeg.org downloads page

**Fallback**: [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) on GitHub.
- Use GitHub API (`GET /repos/BtbN/FFmpeg-Builds/releases/latest`) to resolve the latest release
- Asset pattern: `ffmpeg-master-latest-win64-gpl.zip`
- Larger (~80 MB zip) but guaranteed to have all GPL-licensed codecs

#### 4.2.2 Resolution Order

```
1. User-configured path (from Settings)                            --> use if valid
2. App-local path: %LOCALAPPDATA%\Recode\ffmpeg\ffmpeg.exe     --> use if exists
3. System PATH                                                     --> use if found
4. Not found                                                       --> trigger download to (2)
```

#### 4.2.3 Download Flow

```
App launch
  |
  +--> IFfmpegManager.EnsureAvailableAsync()
         |
         +--> Found locally? --> yes --> done
         |
         +--> no --> Banner: "FFmpeg is required. Download now? (~45 MB)"
                     [Download]  [Choose existing...]
                       |
                       +--> Download with progress bar
                       |      - Stream zip to %TEMP% via HttpClient
                       |      - Extract only ffmpeg.exe from zip
                       |      - Move to %LOCALAPPDATA%\Recode\ffmpeg\
                       |      - Verify: `ffmpeg -version`
                       |      - Clean up temp zip
                       |
                       +--> On failure: error with retry button and
                            manual download link (opens browser)
```

#### 4.2.4 Update Check
- On startup (max once per day), HEAD request to compare against stored version
- If newer available, non-blocking banner: "FFmpeg update available. [Update]"
- User can ignore — existing version keeps working

#### 4.2.5 Storage

```
%LOCALAPPDATA%\Recode\
  ffmpeg\
    ffmpeg.exe
    version.json        # { "version": "7.1", "downloadedUtc": "...", "source": "gyan.dev" }
```

#### 4.2.6 Constraints
- Download does not block app startup — UI loads immediately, compression disabled until FFmpeg is ready
- User can override with a custom path in Settings
- No admin privileges required — `%LOCALAPPDATA%` is user-writable

---

### 4.3 Settings

Opened via gear icon in the title bar. Opens as a separate `Window` (Avalonia does not have a built-in ContentDialog; a modal child window is the standard approach).

#### 4.3.1 General
- **Theme**: System / Light / Dark
- **FFmpeg path**: Auto-detected or manual override (folder picker)

#### 4.3.2 Compression Defaults
- Default codec
- Default quality level
- Default speed preset
- Default audio option
- Default output mode (replace / output to folder)
- Default output directory (for "output to folder" mode)
- Default post-action

#### 4.3.3 Storage
- Settings file: `%APPDATA%\Recode\settings.json`
- Schema-versioned for forward migration
- Hot-reload: changes apply immediately

---

## 5. UI / UX Design

### 5.1 Window

- **Single window**, no navigation sidebar
- **Title bar**: App name on the left, gear icon button on the right
- **Min size**: 700 x 500
- **Default size**: 900 x 600
- **Resizable**: Yes
- **Min/Max/Close**: Standard window chrome buttons
- The entire window content is the compression view

### 5.2 Compression View Layout

```
+--[ Recode ]--------------------------------------[gear]--+
|                                                               |
|  +-----------------------------------------------------------+
|  |  [Browse Files]  or drag & drop files here                 |
|  +-----------------------------------------------------------+
|                                                               |
|  Codec:    [H.264] [H.265] [VP9] [AV1]   (segmented control) |
|  Quality:  [====O==========================] Balanced          |
|  Speed:    [Fast v]       Audio: [Copy Original v]             |
|  Output:   (o) Replace original  ( ) Output to folder [...]   |
|                                                               |
|  Queue:                                                        |
|  +-----------------------------------------------------------+
|  | video1.mp4    1.2 GB    [|||||||||||60%]  ETA 2:30         |
|  | video2.mkv    800 MB    Pending                             |
|  | video3.avi    2.1 GB    Pending                             |
|  +-----------------------------------------------------------+
|  Overall: 1/3 files  [||||||33%]        [Pause] [Cancel]      |
|                                                               |
|  After completion: [Do nothing v]                    [Start]   |
+---------------------------------------------------------------+
```

When "Output to folder" is selected, a folder path + browse button appears inline.

### 5.3 Settings Window

Separate modal window:

```
+-- Settings -------------------------------------------[X]--+
|                                                             |
|  Appearance                                                 |
|  +-------------------------------------------------------+ |
|  | Theme                          [System v]              | |
|  +-------------------------------------------------------+ |
|                                                             |
|  Compression Defaults                                       |
|  +-------------------------------------------------------+ |
|  | Default codec                  [H.265 v]              | |
|  | Default quality                [====O========]         | |
|  | Default speed                  [Medium v]              | |
|  | Default audio                  [Copy Original v]       | |
|  | Default output mode            [Replace original v]    | |
|  | Default output directory       C:\...\  [Browse]       | |
|  | After completion               [Do nothing v]          | |
|  +-------------------------------------------------------+ |
|                                                             |
|  FFmpeg                                                     |
|  +-------------------------------------------------------+ |
|  | Location       Auto-detected (7.1)        [Change]    | |
|  | [Check for update]                                     | |
|  +-------------------------------------------------------+ |
|                                                             |
|  About                                                      |
|  +-------------------------------------------------------+ |
|  | Recode v1.0.0                                      | |
|  +-------------------------------------------------------+ |
+-------------------------------------------------------------+
```

### 5.4 Theming

- **Theme library**: Semi.Avalonia — provides polished light/dark themes with grouped settings-card style controls
- **Default**: Follow system theme (Windows light/dark mode) via `RequestedThemeVariant`
- **Accent color**: Follow Windows accent or use a fixed app accent
- Standard Avalonia controls (TextBox, Slider, ComboBox, ProgressBar, RadioButton, ListBox) — Semi.Avalonia styles them consistently

---

## 6. Data Models

### 6.1 Core Models

```csharp
public record CompressionJob
{
    public required string InputPath { get; init; }
    public string? OutputPath { get; init; }       // null when OutputMode is Replace
    public OutputMode OutputMode { get; init; }
    public CodecType Codec { get; init; }
    public int Quality { get; init; }              // 0-100 (mapped to CRF per codec)
    public SpeedPreset Speed { get; init; }
    public AudioOption Audio { get; init; }
    public JobStatus Status { get; set; }
    public double Progress { get; set; }           // 0.0 - 1.0
    public TimeSpan Elapsed { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
    public long InputSize { get; set; }
    public long? OutputSize { get; set; }
}

public enum CodecType { H264, H265, VP9, AV1 }
public enum SpeedPreset { Ultrafast, Fast, Medium, Slow }
public enum AudioOption { Copy, Aac128, Aac256, Strip }
public enum OutputMode { Replace, OutputToFolder }
public enum PostAction { None, Sleep, Shutdown, OpenFolder, PlaySound }
public enum JobStatus { Pending, Encoding, Paused, Done, Failed, Cancelled }
```

### 6.2 Settings Model

```csharp
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public ThemeOption Theme { get; set; } = ThemeOption.System;
    public string? FfmpegPath { get; set; }             // null = auto-detect

    public CompressionDefaults Compression { get; set; } = new();
}

public sealed class CompressionDefaults
{
    public CodecType Codec { get; set; } = CodecType.H265;
    public int Quality { get; set; } = 50;
    public SpeedPreset Speed { get; set; } = SpeedPreset.Medium;
    public AudioOption Audio { get; set; } = AudioOption.Copy;
    public OutputMode OutputMode { get; set; } = OutputMode.Replace;
    public string OutputDirectory { get; set; } = "";    // empty = default (Videos\Recode\)
    public PostAction PostAction { get; set; } = PostAction.None;
}
```

---

## 7. Service Interfaces

```csharp
public interface ICompressionService
{
    IAsyncEnumerable<CompressionProgress> CompressAsync(
        CompressionJob job,
        CancellationToken ct = default);

    Task PauseAsync();
    Task ResumeAsync();
}

public record CompressionProgress(
    double Fraction,                     // 0.0 - 1.0
    TimeSpan Elapsed,
    TimeSpan? Estimated,
    long BytesWritten);

public interface IFfmpegManager
{
    string? ResolvedPath { get; }
    bool IsAvailable { get; }
    string? Version { get; }

    Task DownloadAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    Task<bool> CheckForUpdateAsync(CancellationToken ct = default);
}

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync();
    event Action? SettingsChanged;
}

public interface IPowerManagementService
{
    void Sleep();
    void Shutdown();
}
```

---

## 8. FFmpeg Integration

### 8.1 Process Lifecycle (via CliWrap)

```
1. Resolve path via IFfmpegManager.ResolvedPath
2. Build argument string from CompressionJob
3. Start process via CliWrap:
   - Stdout piped (for -progress output)
   - Stderr piped (for error capture)
   - Stdin open (for sending 'q' to gracefully quit)
   - CancellationToken linked to UI cancel
4. Parse progress stream in real-time
5. On completion: validate output file exists and is non-zero
6. On cancellation: send 'q', wait 5s, then force kill
7. On app crash: Windows Job Object ensures FFmpeg terminates
```

### 8.2 Argument Construction

```
H.264:  -i "input.mp4" -c:v libx264 -preset medium -crf 23 -c:a copy "output.mp4"
H.265:  -i "input.mkv" -c:v libx265 -preset medium -crf 28 -c:a aac -b:a 128k "output.mp4"
VP9:    -i "input.avi" -c:v libvpx-vp9 -b:v 0 -crf 31 -c:a copy "output.webm"
AV1:    -i "input.mov" -c:v libsvtav1 -preset 6 -crf 35 -c:a copy "output.mp4"
```

All commands include: `-progress pipe:1 -y`

For **Replace** mode, output goes to a temp file (e.g., `input.mp4.tmp.mp4`) in the same directory. On success the original is deleted and the temp is renamed.

### 8.3 Progress Parsing

FFmpeg's `-progress pipe:1` outputs structured key=value pairs:

```
out_time_us=5000000
speed=2.1x
progress=continue
```

Parse `out_time_us` against known duration (from probe pass) for fraction complete.

---

## 9. Error Handling

| Scenario | Handling |
|---|---|
| FFmpeg not found | Banner with [Download] and [Choose existing...]. Compression disabled until resolved |
| FFmpeg download fails | Error with retry button + link to manual download (opens browser) |
| FFmpeg download corrupted | Verify with `ffmpeg -version` after extraction. If fails, delete and re-prompt |
| FFmpeg crashes mid-encode | Mark job as Failed, log stderr, continue to next file in queue |
| Replace mode: rename fails | Keep temp file, show error with path to temp so user can recover manually |
| File access denied | Show error with option to retry as admin (UAC elevation) |
| Disk full during encode | Detect via output monitoring, pause queue, notify user |
| Invalid input file | FFmpeg reports error, mark as Failed, log, skip to next |
| Settings file corrupt | Reset to defaults, log warning, notify user |
| Unexpected exception | Global handler logs to file, shows crash dialog with "Copy log" button |

All exceptions logged via Serilog. No silent catches.

---

## 10. Packaging & Distribution

FFmpeg is **not bundled** — it is downloaded on first launch (see 4.2). This keeps the distributed app small.

### Option A: Single-file portable (recommended)
- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true`
- No install needed, run from any folder
- Settings in `%APPDATA%\Recode\`
- Distributed size: ~20-30 MB (trimmed self-contained)

### Option B: Framework-dependent
- Requires .NET 9 runtime installed on user's machine
- Distributed size: ~5-10 MB
- Smaller but adds a prerequisite