# PTX Layout Viewer

A .NET WPF application that loads PTX (Pattern Exchange Format) files and renders a **rough layout** of rectangular parts as defined by the pattern’s cutting instructions.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)  
  The project targets `net9.0-windows`. To use an older SDK (e.g. .NET 8), change `TargetFramework` in `PTXLayoutViewer/PTXLayoutViewer.csproj` to `net8.0-windows` (or `net7.0-windows`).

## Build and run

From the repo root:

**Using the solution (recommended):**

```bash
dotnet build PTXChecker.sln
dotnet run --project PTXLayoutViewer/PTXLayoutViewer.csproj
```

**Using the project only:**

```bash
dotnet build PTXLayoutViewer/PTXLayoutViewer.csproj
dotnet run --project PTXLayoutViewer/PTXLayoutViewer.csproj
```

On Windows PowerShell you can use backslashes: `PTXLayoutViewer\PTXLayoutViewer.csproj`. Or `cd PTXLayoutViewer` then `dotnet run`.

**From an IDE:**

- **Visual Studio:** Open `PTXChecker.sln` and run the **PTXLayoutViewer** project (F5).
- **VS Code / Cursor:** Open the repo folder, then Run → Start Debugging (F5). The configured build task builds the solution before launch.

## Usage

1. Click **Open PTX…** and select a `.ptx` (or CSV) file.
2. If the file contains patterns with CUTS and BOARDS, a pattern dropdown is filled.
3. Select a pattern to see the board size and a canvas with part rectangles.
4. Click a part in the list or on the canvas to view **Part Details**; use the **Debug** and **Metadata** tabs for layout and document metadata.

Layout is reconstructed from **CUTS** (rip = strip boundaries, cross = segment boundaries). It is **approximate** and does not apply kerf or trim phases.

## Sample files

Use the `.ptx` files in the **`examples/`** folder to test, for example:

- `examples/1_Seq_TEST_hor_leg.ptx`
- `examples/Dev_b1_beta.PTX`
- `examples/Patterns PTX import.ptx`

## Project layout

- **PTXChecker.sln** – Solution (single project).
- **PTXLayoutViewer/** – WPF app
  - **Models/** – Document and layout types (`PtxBoard`, `PtxPart`, `PtxPattern`, `PtxCut`, `LayoutRectangle`, `PtxDocument`, etc.).
  - **PtxParser.cs** – Parses BOARDS, PARTS_REQ, PATTERNS, CUTS, and related PTX sections.
  - **LayoutBuilder.cs** – Rebuilds rectangular layout from CUTS for a pattern.
  - **MainWindow** – File picker, pattern combo, parts list, layout canvas, Part Details / Debug / Metadata tabs.
  - **PartDetailsDialog** – Part details popup (opened from main window).

File format details: **PTX_AI_AGENT_REFERENCE.md**.
