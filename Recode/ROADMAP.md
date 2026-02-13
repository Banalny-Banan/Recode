# Recode - Build Roadmap

## Phase 1: Foundation

- [x] Create solution with 3 projects (Recode, Recode.Core, Recode.Infrastructure)
- [x] Set up project references (Recode → Core + Infrastructure, Infrastructure → Core)
- [x] Install NuGet packages
    - Recode: Microsoft.Extensions.DependencyInjection, Serilog, Serilog.Sinks.File, Serilog.Sinks.Debug
    - Recode.Infrastructure: CliWrap
    - Recode.Core: nothing
- [x] Set up dependency injection in App.axaml.cs
- [ ] Build MainWindow layout (title bar with gear icon, content area)
- [ ] Build CompressionView layout (drop zone, codec selector, quality slider, speed/audio controls, output mode, queue list, bottom bar)
- [ ] Create core models and enums as needed (CodecType, SpeedPreset, OutputMode, etc.)
- [ ] Create CompressionViewModel with basic properties and commands

## Phase 2: Settings + FFmpeg

- [ ] Define ISettingsService interface in Core
- [ ] Implement SettingsService in Infrastructure (JSON read/write to %APPDATA%\Recode\settings.json)
- [ ] Build SettingsWindow UI (theme, compression defaults, FFmpeg path, about)
- [ ] Create SettingsViewModel
- [ ] Wire up gear icon to open SettingsWindow
- [ ] Define IFfmpegManager interface in Core
- [ ] Implement FfmpegManager in Infrastructure (path resolution, download, version check)
- [ ] FFmpeg auto-download flow with progress bar and banner UI
- [ ] Register services in DI container

## Phase 3: Compression

- [ ] Define ICompressionService interface in Core
- [ ] Implement FfmpegCompressionService in Infrastructure (CliWrap, argument building, progress parsing)
- [ ] File input: drag-and-drop + browse button + command-line args
- [ ] Compression queue with per-file status (Pending / Encoding / Done / Failed)
- [ ] Per-file progress bar (percentage, elapsed, ETA)
- [ ] Overall progress bar
- [ ] Cancel and Pause/Resume support
- [ ] Replace-original mode (temp file → rename)
- [ ] Output-to-folder mode
- [ ] Post-compression actions (sleep, shutdown, open folder, notification sound)
- [ ] Completion summary banner

## Phase 4: Polish

- [ ] Error handling for all scenarios (see TECH_SPEC.md Section 9)
- [ ] Serilog integration (file + debug logging throughout)
- [ ] Theme switching (System / Light / Dark)
- [ ] Window Job Object to ensure FFmpeg terminates on app crash
- [ ] FFmpeg update check (once per day, non-blocking banner)
- [ ] Settings hot-reload
- [ ] Testing and bug fixes
- [ ] Single-file publish configuration