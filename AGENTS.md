# PdfToGCode - Project Documentation

## 1. Tổng Quan Dự Án (Project Overview)

**Mục tiêu:** Xây dựng ứng dụng WPF (.NET 8.0) chuyển đổi file PDF thành G-code tối ưu cho máy vẽ (Pen Plotter) và máy CNC laser/phay nhỏ. Ứng dụng hỗ trợ trích xuất văn bản (Text) và hình học (Vector Shapes), sử dụng font đơn nét (Single-line Fonts) để tạo đường chạy dao chính xác. Ngoài ra, tích hợp trình gửi G-code (G-code Sender) để điều khiển máy trực tiếp qua cổng Serial (GRBL).

**Công nghệ sử dụng:**
- **Framework:** .NET 8.0 (Windows Desktop).
- **UI:** WPF (Windows Presentation Foundation), kiến trúc Code-behind (đơn giản hóa, không bắt buộc MVVM cứng nhắc).
- **PDF Processing:** `PdfPig` (trích xuất text, tọa độ, vector paths).
- **Rendering:** `System.Windows.Shapes` (Path, Polygon) trên Canvas tùy biến (`ZoomPanCanvas`).
- **Communication:** `System.IO.Ports` (Serial Port) giao tiếp với GRBL firmware.
- **Font Parsing:** Custom SVG Parser cho font đơn nét (Hershey/CNC fonts).

---

## 2. Kiến Trúc Kỹ Thuật (Technical Architecture)

Dự án được chia thành 3 project chính:

1.  **`PdfToGCode.Core` (Class Library):**
    - Chứa toàn bộ logic xử lý nghiệp vụ, độc lập với giao diện.
    - Xử lý PDF (Extract Text/Shapes).
    - Xử lý Font (SVG Parsing, Linearization).
    - Sinh mã G-code (Sorting, Path Optimization, Z-Hop logic).
    - Giao tiếp máy (Serial Communication, GRBL Protocol).

2.  **`PdfToGCode.App` (WPF Application):**
    - Giao diện người dùng (UI).
    - Xử lý sự kiện (Button Click, Drag & Drop).
    - Hiển thị trực quan (Preview Canvas, Progress Bars).
    - Lưu trữ cấu hình (Settings).

3.  **`PdfToGCode.Tests` (Unit Tests):**
    - Kiểm thử các module logic trong Core.

---

## 3. Cấu Trúc Thư Mục (Directory Structure)

```
PdfToGCode/
├── PdfToGCode.sln                  # Solution File
├── AGENTS.md                       # Project Documentation
├── .gitignore
├── PdfToGCode.Core/                # [Library] Core Logic
│   ├── PdfToGCode.Core.csproj
│   ├── Pdf/                        # PDF Processing Logic
│   │   ├── PdfTextLayoutExtractor.cs # Main Extraction Logic (PdfPig)
│   │   ├── ExtractedPageContent.cs   # Data Model (Page)
│   │   ├── ExtractedText.cs          # Data Model (Text Block)
│   │   ├── ExtractedGlyph.cs         # Data Model (Character/Glyph)
│   │   └── ExtractedShape.cs         # Data Model (Vector Shape)
│   ├── GCode/                      # G-Code Generation
│   │   ├── GCodeGenerator.cs         # Generator Logic
│   │   └── GCodeSettings.cs          # Configuration Model (Feed, Z, etc.)
│   ├── Services/                   # Hardware/System Services
│   │   └── GrblSender.cs             # Serial Port Communication (GRBL)
│   ├── Fonts/                      # Font Management
│   │   ├── FontData.cs               # Internal Font Representation
│   │   └── SvgFontParser.cs          # SVG Path Parser -> Points
│   └── Utils/                      # Utilities
│       ├── CoordinateMapper.cs       # PDF <-> GCode <-> Canvas
│       ├── FontManager.cs            # Loads Fonts from Folder/Embed
│       ├── GeometryUtils.cs          # Math helpers
│       └── PageRangeParser.cs        # String Range Parser ("1-3, 5")
├── PdfToGCode.App/                 # [App] WPF UI
│   ├── PdfToGCode.App.csproj
│   ├── App.xaml / .cs
│   ├── MainWindow.xaml / .cs       # Main UI Entry Point
│   ├── Fonts/                      # Embedded Resources or CopyToOutput
│   │   └── CHUINHOA.svg              # Default Embedded Font
│   ├── Rendering/                  # Visualization
│   │   ├── VectorSceneRenderer.cs    # Renders G-code paths to Canvas
│   │   └── ZoomPanCanvas.cs          # Custom Canvas with Zoom/Pan
│   └── Views/                      # (Optional) UserControls for Tabs
│       ├── DesignView.xaml           # (Planned) Design Tab UI
│       └── MachineControlView.xaml   # (Planned) Machine Tab UI
└── PdfToGCode.Tests/               # [Tests]
```

---

## 4. Thiết Kế UI (User Interface)

Giao diện chính (`MainWindow`) sử dụng `TabControl` với 2 tabs chức năng:

### Tab 1: Design & Preview (Thiết Kế & Xem Trước)
*Mục đích: Load file, cấu hình tham số cắt, chọn font, xem trước đường chạy dao.*

*   **Layout Trái (Settings Panel):**
    *   **File Input:** Nút "Load PDF", hiển thị tên file.
    *   **Page Selection:** TextBox nhập range (VD: "1, 3-5").
    *   **Font Settings:**
        *   *Title Font:* ComboBox chọn font cho tiêu đề (nhận diện bằng chữ in hoa toàn bộ).
        *   *Body Font:* ComboBox chọn font cho nội dung thường.
    *   **G-Code Settings:**
        *   *Feed Rate (XY):* Tốc độ di chuyển cắt (mm/min).
        *   *Travel Speed (Rapid):* Tốc độ di chuyển không cắt (G0).
        *   *Z Down:* Độ sâu dao khi cắt (hoặc góc Servo).
        *   *Z Up (Safe):* Độ cao an toàn khi di chuyển giữa các vùng (Safe Lift).
        *   *Z Gap (Low):* Độ cao nhấc nhẹ khi di chuyển giữa các nét trong cùng một chữ (Fast Lift).
        *   *Servo Mode:* Checkbox (Sử dụng `M3 S...` thay vì trục Z).
    *   **Action Buttons:** "Generate G-Code", "Save G-Code".

*   **Layout Phải (Preview Area):**
    *   **Canvas:** `ZoomPanCanvas` hiển thị vector.
    *   **Visuals:**
        *   *Border Page:* Hình chữ nhật viền đen thể hiện khổ giấy.
        *   *Text Paths:* Màu Đỏ (Red) - stroke mảnh.
        *   *Shape Paths:* Màu Xám (Gray) - stroke mảnh.

### Tab 2: Machine Control (Điều Khiển Máy)
*Mục đích: Kết nối máy CNC/Plotter, gửi lệnh G-code, Jogging.*

*   **Connection Panel:**
    *   *Port:* ComboBox danh sách COM ports.
    *   *Baud:* ComboBox (default 115200).
    *   *Connect/Disconnect:* Button toggles connection.
    *   *Status:* Label hiển thị trạng thái (Connected/Disconnected/Alarm).
*   **Job Control:**
    *   *File Status:* Hiển thị file G-code đang nạp.
    *   *Control Buttons:* "Send" (Play), "Pause" (!), "Stop" (Reset/Soft Reset).
    *   *Progress Bar:* Tiền độ gửi dòng lệnh.
*   **Manual Control (Jogging):**
    *   Các nút mũi tên (Y+, Y-, X-, X+) để di chuyển trục XY.
    *   Nút Z+, Z- (hoặc Pen Up/Down cho Servo).
    *   *Step Size:* ComboBox (0.1, 1, 10, 50 mm).
*   **Console/Terminal:**
    *   TextBox hiển thị log giao tiếp (Gửi đi `>` và Nhận về `<`).
    *   TextBox nhập lệnh G-code thủ công.

---

## 5. Chi Tiết Logic & Method (Core Logic)

### 5.1. PDF Extraction (`PdfTextLayoutExtractor.cs`)
*   **Thư viện:** `UglyToad.PdfPig`.
*   **Logic Trích Xuất Text:**
    *   Duyệt qua `page.GetWords()`.
    *   Lưu trữ dưới dạng `ExtractedText` (Words) chứa danh sách `ExtractedGlyph` (Characters).
    *   *Quan trọng:* Giữ nguyên tọa độ gốc PDF (Bottom-Left origin) để xử lý thống nhất, chỉ convert khi render ra màn hình WPF (Top-Left).
*   **Logic Trích Xuất Shapes:**
    *   Duyệt qua `page.Paths`.
    *   Lọc bỏ các đường bao màu trắng (Background artifacts).
    *   Chuyển đổi các lệnh vẽ (`MoveTo`, `LineTo`, `Bezier`) thành danh sách điểm (`List<PdfPoint>`).
    *   **Bezier Linearization:** Đường cong Bezier được chia nhỏ thành các đoạn thẳng (segments) dựa trên độ dài polygon điều khiển (Resolution ~1.0 unit) để đảm bảo độ mịn nhưng không làm file G-code quá nặng.

### 5.2. Font Processing (`SvgFontParser.cs`, `FontManager.cs`)
*   **Định dạng:** Sử dụng file SVG chứa các glyphs dạng "Single-line" (font nét đơn) chuyên dụng cho CNC.
*   **Parsing:**
    *   Đọc thẻ `<glyph>` hoặc `<path>` trong SVG.
    *   Parse thuộc tính `d` (path data). Hỗ trợ các lệnh: `M` (Move), `L` (Line), `H` (Horizontal), `V` (Vertical), `C` (Cubic Bezier), `Q` (Quad Bezier), `Z` (Close).
    *   Chuẩn hóa về tọa độ relative/absolute.
*   **Quản lý Font:**
    *   Load font từ thư mục `Fonts/`.
    *   Tự động load font mặc định `CHUINHOA` (nhúng trong resource) nếu không tìm thấy font ngoài.
    *   **Fallback Mechanism:** Nếu ký tự không có trong Font A, thử tìm trong Font B (Body font) trước khi bỏ qua.

### 5.3. G-Code Generation (`GCodeGenerator.cs`)
*   **Quy trình:**
    1.  **Header:** `G21` (mm), `G90` (Absolute), `G64` (Constant Velocity).
    2.  **Sorting (Tối ưu đường chạy):**
        *   Ưu tiên vẽ **Shapes** (Khung, bảng biểu) trước. Sắp xếp từ Trên xuống Dưới (Top-Down based on MaxY).
        *   Vẽ **Text** sau. Sắp xếp theo dòng (Top-Down) và trái qua phải (Left-Right).
    3.  **Text Logic:**
        *   Kiểm tra `IsAllUpperCase(text)` -> Nếu đúng dùng **Title Font**, ngược lại dùng **Body Font**.
    4.  **Z-Hop Logic (Tối ưu thời gian):**
        *   **Z-Up (Safe Height):** Nhấc cao an toàn khi di chuyển giữa các cụm từ hoặc shapes xa nhau.
        *   **Z-Gap (Low Height):** Nhấc thấp (chỉ vừa đủ thoát mặt giấy) khi di chuyển giữa các nét trong cùng một chữ hoặc giữa các chữ gần nhau. Giúp giảm rung động và tăng tốc độ.
    5.  **Servo Mode:**
        *   Nếu bật: Sử dụng `M3 S{ZGap}` (Pen Up) và `M3 S{ZDown}` (Pen Down).
        *   Nếu tắt: Sử dụng `G0 Z{ZUp}` và `G1 Z{ZDown}`.

### 5.4. Machine Control (`GrblSender.cs`)
*   **Giao tiếp:** Serial Port (COM), Baud rate thường dùng 115200.
*   **Polling:** Gửi lệnh `?` mỗi 200ms để lấy trạng thái (`<Idle|MPos:0.000,0.000,0.000|FS:0,0>`).
*   **Flow Control:**
    *   Gửi từng dòng lệnh.
    *   Chờ phản hồi `ok` trước khi gửi dòng tiếp theo (Simple Send-Response protocol).
    *   Hỗ trợ lệnh thời gian thực: `!` (Feed Hold/Pause), `~` (Cycle Start/Resume), `Ctrl-X` (Soft Reset).

---

## 6. Quy Ước & Lưu Ý (Conventions & Notes)
*   **Hệ Tọa Độ:**
    *   PDF: Gốc (0,0) ở Bottom-Left.
    *   WPF Canvas: Gốc (0,0) ở Top-Left -> Cần `CoordinateMapper` để lật trục Y (`CanvasY = PageHeight - PdfY`).
    *   G-Code: Tuân theo hệ tọa độ PDF (Gốc Bottom-Left) để máy vẽ đúng chiều.
*   **Đơn vị:** Milimeters (mm). PDF Points (pt) thường được convert: 1 pt = 1/72 inch = 0.3527 mm.
*   **Async/Await:** Các tác vụ nặng (Load PDF, Generate G-code, Send Serial) phải chạy bất đồng bộ (`Task.Run`) để không chặn UI thread.
*   **Tài nguyên:** Các file SVG Font phải được copy vào thư mục output (`Copy if newer`) hoặc nhúng làm Embedded Resource.

---
*Tài liệu này được cập nhật tự động bởi Agent để phản ánh trạng thái chính xác của dự án.*
