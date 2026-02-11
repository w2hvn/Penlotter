# PdfToGCode Project Documentation

## 1. Project Overview
**PdfToGCode** is a C# WPF application targeting .NET 8.0 designed to convert PDF documents into G-code for CNC machines, plotters, or writing robots. It focuses on extracting text and vector shapes (tables, borders) from PDFs and converting them into single-line vector paths suitable for pen plotting.

## 2. Technology Stack
- **Language:** C#
- **Framework:** .NET 8.0 (Windows)
- **UI:** WPF (Windows Presentation Foundation)
- **PDF Library:** `UglyToad.PdfPig` (v0.1.13) for content extraction.
- **Rendering:** `PdfiumViewer` (via Bitmap) for PDF visualization.
- **Testing:** xUnit

## 3. Architecture
The solution is organized into three projects:
- **`PdfToGCode.App`**: The WPF User Interface.
  - `MainWindow.xaml`: Main UI with PDF preview (left) and Vector preview (right).
  - `VectorSceneRenderer.cs`: Handles rendering extracted vectors to the WPF Canvas.
- **`PdfToGCode.Core`**: The core business logic (Standard Library).
  - `Pdf/PdfTextLayoutExtractor.cs`: Extracts text and shapes from PDF pages.
  - `GCode/GCodeGenerator.cs`: Generates G-code from extracted content.
  - `Fonts/SvgFontParser.cs`: Parses single-line SVG fonts.
- **`PdfToGCode.Tests`**: Unit tests for Core logic.

## 4. Key Features & Implementation Details

### 4.1. Shape Extraction (Tables & Borders)
- **Library:** Uses `PdfPig` to iterate page paths.
- **Command Handling:**
  - Handles standard PDF commands: `MoveTo`, `LineTo`, `ClosePath`.
  - **Fix for PdfPig v0.1.13:** Explicitly handles command classes named `Move` (property `Location`), `Line` (property `To`), `Close`.
  - **Rectangle Handling:** Explicitly handles `Rectangle` (or `AppendRectangle`) commands by decomposing them into 4 line segments using properties `LowerLeftX`, `LowerLeftY`, `Width`, `Height`.
- **Filled vs. Stroked:**
  - Extracts paths that are **Stroked** (`IsStroked = true`).
  - Extracts paths that are **Filled** (`IsFilled = true`) **ONLY IF** they are **NOT White**.
  - **White Filter:** The `IsWhite` method checks `IColor` (RGB, Gray, CMYK). If a filled shape is white (e.g., RGB > 0.99), it is ignored to prevent extracting background masks/borders. This ensures dark tables are captured while white backgrounds are ignored.

### 4.2. Text Extraction & Font Selection
- **Single-Line Fonts:** Uses SVG fonts located in the `Fonts` folder. No embedded default font logic.
- **Font Selection Logic:**
  - **Title Font:** Applied if the text block is "All Uppercase".
  - **Body Font:** Applied otherwise.
  - **Critical Logic (`IsAllUpperCase`):** Returns `true` **only if** the text contains at least one letter AND all letters are uppercase. Pure numeric strings ("123") or symbols return `false` and thus use the **Body Font**.

### 4.3. Bezier Curve Optimization
- **Adaptive Subdivision:** Converts Bezier curves (from fonts or PDF shapes) into polyline segments adaptively.
- **Logic:**
  - Calculates estimated Arc Length (sum of control point distances).
  - `Steps = Length / Resolution`.
  - **Resolution:**
    - PDF Shapes: ~1.0 unit (approx 0.35mm).
    - SVG Fonts: ~20.0 units (relative to UPM ~1000).
  - **Limits:** Min 2 steps, Max 100 steps.
- **Benefit:** Reduces G-code file size and processing time for small curves while maintaining smoothness for large curves.

### 4.4. G-code Generation
- **Files:** Generates separate G-code files for each selected page (`{filename}_{page}.gcode`).
- **Modes:**
  - **Servo Mode:** Uses `M3 S{Z_Down}` for Pen Down and `M3 S{Z_Up}` for Pen Up.
  - **Z-Axis Mode:** Uses `G1 Z{Z_Down}` and `G0 Z{Z_Up}`.
- **Sorting:** Optimizes plotting order:
  1. Shapes (Tables/Borders) sorted Top-Down.
  2. Text blocks sorted Top-Down, then Left-Right.

## 5. Development Guidelines
- **Always Verify:** Run `dotnet test` after changes.
- **Coordinate Systems:** PDF origin is Bottom-Left. WPF origin is Top-Left. `CoordinateMapper` handles the flip. G-code output preserves PDF coordinates (Bottom-Left origin).
- **Frozen Geometry:** WPF Geometry objects created in background threads must be frozen (`.Freeze()`) before accessing on UI thread.

## 6. Current Status (Finalized)
- Shape extraction works for lines, rectangles, and tables (filled shapes).
- White background artifacts are filtered out.
- Numbers use the Body font.
- G-code is optimized with adaptive curves.
