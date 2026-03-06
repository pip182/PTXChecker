# PTX Layout Viewer

A .NET WPF application that loads PTX (Pattern Exchange Format) files and renders a **rough layout** of rectangular parts as defined by the pattern’s cutting instructions.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download) (or .NET 7/8; adjust `TargetFramework` in `PTXLayoutViewer.csproj` if needed)

## Build and run

```bash
cd PTXChecker
dotnet build PTXLayoutViewer/PTXLayoutViewer.csproj
dotnet run --project PTXLayoutViewer/PTXLayoutViewer.csproj
```

Or open `PTXChecker.sln` in Visual Studio and run **PTXLayoutViewer**.

## Usage

1. Click **Open PTX…** and select a `.ptx` (or CSV) file.
2. If the file contains patterns with CUTS and BOARDS, a pattern dropdown is filled.
3. Select a pattern to see the board size and a canvas with part rectangles.
4. Parts are drawn as colored rectangles; labels show part name or index.

Layout is reconstructed from **CUTS** (rip = strip boundaries, cross = segment boundaries). It is **approximate** and does not apply kerf or trim phases.

## Sample file

Use `Sample.ptx` in the repo root to test: one 2440×1220 mm board with one 720×560 mm part (SIDE).

## Project layout

- **PTXLayoutViewer** – WPF app
  - **Models/** – `PtxBoard`, `PtxPart`, `PtxPattern`, `PtxCut`, `LayoutRectangle`, `PtxDocument`
  - **PtxParser.cs** – Parses BOARDS, PARTS_REQ, PATTERNS, CUTS
  - **LayoutBuilder.cs** – Rebuilds rectangular layout from CUTS for a pattern
  - **MainWindow** – File picker, pattern combo, canvas with part rectangles

File format details: see **PTX_AI_AGENT_REFERENCE.md**.
