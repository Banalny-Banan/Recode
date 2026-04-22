# Recode

A simple, fast video compression utility for Windows.

![Platform](https://img.shields.io/badge/platform-Windows-blue?style=for-the-badge)
[![Downloads](https://img.shields.io/github/downloads/Banalny-Banan/Recode/total?label=Downloads&color=333333&style=for-the-badge)](https://github.com/Banalny-Banan/Recode/releases/latest)

## Features

- **Drag-and-drop queue** — add multiple files at once and compress them in sequence
- **Three codecs** — H.264, H.265, VP9
- **Configurable quality** — you can select the desired compression level
- **Flexible output** — replace the original file or save to a separate folder
- **Duplicate prevention** — tracks previously compressed files by content hash to avoid re-encoding
- **Post-compression actions** — optionally sleep or shut down the computer when the queue finishes
- **GPU acceleration** — supports hardware encoding on compatible devices for faster processing

## Installation

Download the latest release from the [Releases](../../releases) page and run `Recode.exe`.

On first launch, the app will download FFmpeg (~45 MB) to `%LOCALAPPDATA%\Recode\`.
