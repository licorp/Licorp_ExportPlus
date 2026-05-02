<p align="center">
  <img src="docs/images/banner.png" alt="Licorp Export+" width="100%"/>
</p>

<h1 align="center">Licorp Export+</h1>

<p align="center">
  <strong>🚀 Professional Batch Export Plugin for Autodesk Revit</strong>
</p>

<p align="center">
  <a href="#-features"><img src="https://img.shields.io/badge/formats-9+-00C7B7?style=for-the-badge&logo=files&logoColor=white" alt="Formats"/></a>
  <a href="#-supported-revit-versions"><img src="https://img.shields.io/badge/Revit-2020%20|%202025%20|%202027-0696D7?style=for-the-badge&logo=autodesk&logoColor=white" alt="Revit"/></a>
  <a href="#-installation"><img src="https://img.shields.io/badge/license-Proprietary-FF6B6B?style=for-the-badge" alt="License"/></a>
  <a href="https://github.com/licorp/Licorp_ExportPlus/releases"><img src="https://img.shields.io/github/v/release/licorp/Licorp_ExportPlus?style=for-the-badge&color=28A745&label=Download" alt="Release"/></a>
</p>

<p align="center">
  <em>Export hàng trăm sheets & views sang PDF, DWG, IFC, NWC và nhiều định dạng khác — chỉ với một click.</em>
</p>

---

## 📋 Mục lục

- [✨ Features](#-features)
- [🎯 Export Formats](#-export-formats)
- [🏗️ Architecture](#️-architecture)
- [📦 Installation](#-installation)
- [🚀 Quick Start](#-quick-start)
- [⚙️ Configuration](#️-configuration)
- [🔧 Build from Source](#-build-from-source)
- [📊 So sánh với đối thủ](#-so-sánh-với-đối-thủ)
- [📝 Changelog](#-changelog)

---

## ✨ Features

<table>
  <tr>
    <td width="50%">

### 📤 Batch Export Engine
- Export **hàng trăm sheets/views** cùng lúc
- Hỗ trợ **9+ định dạng** file output
- **Async processing** — UI luôn mượt mà
- **Cancel** export bất cứ lúc nào
- **Retry** tự động cho các sheets lỗi

</td>
<td width="50%">

### 📝 Parametric Naming
- Template linh hoạt: `{SheetNumber}_{SheetName}`
- Truy cập **mọi Revit parameter** trên sheet
- Biến môi trường: `%UserName%`, `%ComputerName%`
- Biến thời gian: `{Date}`, `{Time}`, `{DateTime}`
- **Subfolder tự động** theo parameter: `{Discipline}\{Level}`

</td>
  </tr>
  <tr>
    <td width="50%">

### 💾 Profile System
- **Save/Load** cấu hình export (JSON format)
- **Import profiles XML** từ các tool khác
- Chia sẻ profiles trong team
- Áp dụng nhanh cho các loại dự án khác nhau

</td>
<td width="50%">

### ⏱️ Scheduling
- Hẹn giờ export: **Once / Daily / Weekly / Monthly**
- Timer polling tự động mỗi phút
- Chạy ngầm khi Revit đang mở
- Lý tưởng cho batch export ban đêm

</td>
  </tr>
  <tr>
    <td width="50%">

### 🔗 AutoCAD XREF Bind
- **Tự động phát hiện** AutoCAD trên máy
- **Bind tất cả XREFs** vào DWG chính
- **Dọn dẹp** XREF files thừa sau bind
- Script-based — không cần mở AutoCAD thủ công

</td>
<td width="50%">

### 📊 Reporting & Transmittal
- **Export Report** CSV với chi tiết mỗi file
- **Drawing Transmittal** tự động
- Export Queue tracking real-time
- Filename preview trước khi export

</td>
  </tr>
</table>

---

## 🎯 Export Formats

<table>
<tr>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-PDF-FF0000?style=flat-square&logo=adobe-acrobat-reader&logoColor=white" alt="PDF"/><br/>
  <sub><b>PDF</b></sub><br/>
  <sub>Native API +<br/>PrintManager</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-DWG-0696D7?style=flat-square&logo=autodesk&logoColor=white" alt="DWG"/><br/>
  <sub><b>DWG</b></sub><br/>
  <sub>+ XREF Bind<br/>tự động</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-DXF-4A90D9?style=flat-square&logo=autodesk&logoColor=white" alt="DXF"/><br/>
  <sub><b>DXF</b></sub><br/>
  <sub>AutoCAD<br/>Exchange</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-IFC-88C540?style=flat-square&logo=buildkite&logoColor=white" alt="IFC"/><br/>
  <sub><b>IFC</b></sub><br/>
  <sub>2x2/2x3/4<br/>All MVDs</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-NWC-1B8045?style=flat-square&logo=autodesk&logoColor=white" alt="NWC"/><br/>
  <sub><b>NWC</b></sub><br/>
  <sub>Navisworks<br/>Cache</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-XML-E34F26?style=flat-square&logo=w3c&logoColor=white" alt="XML"/><br/>
  <sub><b>XML</b></sub><br/>
  <sub>Data<br/>Export</sub>
</td>
<td align="center" width="11%">
  <img src="https://img.shields.io/badge/-IMG-FF69B4?style=flat-square&logo=image&logoColor=white" alt="IMG"/><br/>
  <sub><b>Images</b></sub><br/>
  <sub>JPEG/PNG<br/>TIFF</sub>
</td>
</tr>
</table>

### PDF Export Highlights

| Feature | Chi tiết |
|---------|---------|
| **Native API** | Revit 2024+ native `PDFExportOptions` — nhanh & chất lượng cao |
| **PrintManager** | Legacy fallback cho Revit 2020-2023 |
| **Combined PDF** | Gộp nhiều sheets thành 1 file PDF duy nhất |
| **Paper Size Auto-detect** | Tự động nhận diện khổ giấy từ TitleBlock (có cache) |
| **Color Options** | Color / Grayscale / Black & White |
| **Raster Quality** | Low / Medium / High / Maximum |

### IFC Export Highlights

| Feature | Chi tiết |
|---------|---------|
| **Setup Reader** | Đọc IFC configurations từ Revit ExtensibleStorage |
| **Built-in MVDs** | IFC 2x2, 2x3 CV2, GSA, FM Handover, COBie, IFC4 RV/DTV |
| **Custom Property Sets** | Hỗ trợ User Defined Psets file |
| **Parameter Mapping** | External parameter mapping file |
| **3D View Export** | Export 3D views riêng biệt |

---

## 🏗️ Architecture

```
LicorpExportPlus/
├── Source/
│   ├── LicorpExportPlus.Shared/          # 🔧 Shared codebase (core logic)
│   │   ├── Models/                        #    Data models & settings
│   │   │   ├── ExportPlusModels.cs        #    Export settings, formats, enums
│   │   │   ├── SheetItem.cs               #    Sheet ViewModel with parameters
│   │   │   ├── Profile.cs                 #    Profile & settings container
│   │   │   └── ExportQueueItem.cs         #    Queue item with progress tracking
│   │   ├── Services/                      #    Business logic layer
│   │   │   ├── PDFExportService.cs        #    PDF export (Native + PrintManager)
│   │   │   ├── DWGExportService.cs        #    DWG export + link management
│   │   │   ├── DXFExportService.cs        #    DXF export
│   │   │   ├── IFCExportService.cs        #    IFC export + setup reader
│   │   │   ├── NWCExportService.cs        #    Navisworks export
│   │   │   ├── BatchExportService.cs      #    Export coordinator
│   │   │   ├── ProfileManagerService.cs   #    Profile CRUD operations
│   │   │   ├── AutoCADBindService.cs      #    XREF binding via AutoCAD
│   │   │   ├── SchedulingAssistant.cs     #    Timer-based scheduling
│   │   │   ├── ExportReportService.cs     #    CSV report generation
│   │   │   ├── DrawingTransmittalService.cs # Transmittal documentation
│   │   │   └── ViewSheetSetService.cs     #    ViewSheetSet CRUD
│   │   ├── Utils/                         #    Utility classes
│   │   │   ├── FileNameGenerator.cs       #    Parametric filename engine
│   │   │   ├── SheetSizeDetector.cs       #    TitleBlock size detection (cached)
│   │   │   ├── SheetBatchLoader.cs        #    Async batch loading
│   │   │   └── AsyncSheetViewManager.cs   #    Virtualized loading
│   │   ├── Views/                         #    WPF UI
│   │   ├── Events/                        #    Revit ExternalEvent handlers
│   │   ├── Helpers/                       #    Reflection & file helpers
│   │   └── Converters/                    #    WPF value converters
│   │
│   ├── LicorpExportPlus.R20/             # 🏠 Revit 2020 target
│   ├── LicorpExportPlus.R25/             # 🏠 Revit 2025 target
│   ├── LicorpExportPlus.R27/             # 🏠 Revit 2027 target
│   └── LicorpExportPlus.Tests/           # 🧪 Unit tests
│
├── docs/                                  # 📚 Documentation & images
├── build-package.ps1                      # 📦 Build & packaging script
└── build-package.bat                      # 📦 Build script (batch)
```

### Tech Stack

| Component | Technology |
|-----------|-----------|
| **Framework** | .NET Framework 4.8 / .NET 8.0 |
| **UI** | WPF (XAML) |
| **Revit API** | Autodesk.Revit.DB / Autodesk.Revit.UI |
| **DI Container** | Ricaun.Revit.DI |
| **Serialization** | Newtonsoft.Json |
| **Multi-version** | Shared Project (.shproj) |
| **Extensions** | Nice3point.Revit.Extensions |

---

## 📦 Installation

### Cách 1: Download Release (Khuyến nghị)

1. Vào [**Releases**](https://github.com/licorp/Licorp_ExportPlus/releases)
2. Download file `LicorpExportPlus_Setup_x.x.x.zip`
3. Giải nén vào thư mục Revit Add-ins:
   ```
   %AppData%\Autodesk\Revit\Addins\20xx\
   ```
4. Khởi động lại Revit

### Cách 2: Build from Source

```powershell
# Clone repository
git clone https://github.com/licorp/Licorp_ExportPlus.git
cd Licorp_ExportPlus

# Build cho Revit 2025
dotnet build Source/LicorpExportPlus.R25/LicorpExportPlus.R25.csproj -c Release

# Hoặc dùng build script
.\build-package.ps1
```

---

## 🚀 Quick Start

```
1️⃣  Mở Revit → Tab "Licorp" → Click "Export+"
2️⃣  Chọn sheets/views cần export (hoặc dùng ViewSheetSet)
3️⃣  Tick các format cần xuất: ☑ PDF  ☑ DWG  ☑ IFC
4️⃣  Chọn thư mục output & cấu hình naming template
5️⃣  Click "Export" → Xong! ✅
```

### Naming Template Examples

| Template | Output |
|----------|--------|
| `{SheetNumber}_{SheetName}` | `A101_Floor Plan Level 1.pdf` |
| `{ProjectNumber}-{SheetNumber}` | `PRJ001-A101.dwg` |
| `{Date}_{SheetNumber}_Rev{CurrentRevision}` | `2026-05-02_A101_RevC.pdf` |
| `%UserName%\{Discipline}\{SheetNumber}` | `Admin\Architecture\A101.pdf` |

---

## ⚙️ Configuration

### Export Settings

<details>
<summary><b>📄 PDF Settings</b></summary>

| Setting | Options | Default |
|---------|---------|---------|
| Color Depth | Color / Grayscale / B&W | Color |
| Raster Quality | Low / Medium / High / Max | High |
| Paper Placement | Center / Offset | Center |
| Hidden Lines | Vector / Raster Processing | Vector |
| Zoom | Fit to Page / Custom % | Fit to Page |
| Combine Files | Single PDF / Separate | Separate |
| Hide Crop Boundaries | Yes / No | Yes |
| Hide Scope Boxes | Yes / No | Yes |

</details>

<details>
<summary><b>📐 DWG Settings</b></summary>

| Setting | Options | Default |
|---------|---------|---------|
| DWG Version | AutoCAD 2018-2027 | 2018 |
| Export Setup | From Revit setups | Default |
| Layer Settings | Standard / AIA / ISO | Standard |
| XREF Handling | Bind / Keep / None | Bind |
| Link Management | Include / Exclude | Include |
| Cleanup XREFs | Yes / No | Yes |

</details>

<details>
<summary><b>🏗️ IFC Settings</b></summary>

| Setting | Options | Default |
|---------|---------|---------|
| IFC Version | 2x2 / 2x3 CV2 / IFC4 RV / IFC4 DTV | IFC 2x3 CV2 |
| Space Boundaries | None / 1st / 2nd Level | None |
| Base Quantities | Yes / No | No |
| Split Walls by Level | Yes / No | Yes |
| Export Linked Files | Yes / No | No |
| Store IFC GUID | Yes / No | No |
| Property Sets | Built-in / User-defined | Built-in |

</details>

<details>
<summary><b>🗂️ NWC Settings</b></summary>

| Setting | Options | Default |
|---------|---------|---------|
| Coordinates | Shared / Internal / Project | Shared |
| Convert Elements | Yes / No | Yes |
| Export Parts | Yes / No | No |
| Export Room Geometry | Yes / No | No |
| Divide File | No / By Level / By Design Option | No |

</details>

### Profile Management

```
📁 %AppData%\ExportPlusProfiles\
├── Default.json          # Profile mặc định
├── Architecture.json     # Profile kiến trúc
├── MEP_Submittal.json    # Profile MEP
└── IFC_Exchange.json     # Profile IFC
```

---

## 🔧 Build from Source

### Prerequisites

- Visual Studio 2022+
- .NET Framework 4.8 SDK
- .NET 8.0 SDK
- Revit 2020 / 2025 / 2027 (for API references)

### Build Commands

```powershell
# Restore NuGet packages
dotnet restore Source/LicorpExportPlus.sln

# Build all targets
dotnet build Source/LicorpExportPlus.sln -c Release

# Build specific version
dotnet build Source/LicorpExportPlus.R25/LicorpExportPlus.R25.csproj -c Release

# Run tests
dotnet test Source/LicorpExportPlus.Tests/LicorpExportPlus.Tests.csproj

# Package for distribution
.\build-package.ps1
```

---

## 📊 So sánh với đối thủ

<table>
<tr>
<th>Feature</th>
<th>Export+</th>
<th>ProSheet</th>
<th>RTV Xporter</th>
<th>CTC BIM</th>
</tr>
<tr>
<td><b>Export Formats</b></td>
<td>✅ 9 formats</td>
<td>7 formats</td>
<td>10 formats</td>
<td>4 formats</td>
</tr>
<tr>
<td><b>XREF Auto-Bind</b></td>
<td>✅ Unique</td>
<td>❌</td>
<td>❌</td>
<td>❌</td>
</tr>
<tr>
<td><b>IFC Setup Reader</b></td>
<td>✅ Unique</td>
<td>❌</td>
<td>❌</td>
<td>❌</td>
</tr>
<tr>
<td><b>Combined PDF</b></td>
<td>✅</td>
<td>❌</td>
<td>✅</td>
<td>❌</td>
</tr>
<tr>
<td><b>Async Loading</b></td>
<td>✅</td>
<td>❌</td>
<td>❌</td>
<td>❌</td>
</tr>
<tr>
<td><b>Drawing Transmittal</b></td>
<td>✅</td>
<td>❌</td>
<td>✅ (paid)</td>
<td>❌</td>
</tr>
<tr>
<td><b>Env Variables in Naming</b></td>
<td>✅ Unique</td>
<td>❌</td>
<td>❌</td>
<td>❌</td>
</tr>
<tr>
<td><b>Multi-Revit Versions</b></td>
<td>✅ R20/25/27</td>
<td>✅</td>
<td>✅</td>
<td>✅</td>
</tr>
<tr>
<td><b>Price</b></td>
<td><b>🆓 Free</b></td>
<td>Freemium</td>
<td>~$600/yr</td>
<td>~$500/yr</td>
</tr>
</table>

---

## 🗺️ Roadmap

- [x] Batch export PDF/DWG/DXF/IFC/NWC/IMG/XML
- [x] Parametric file naming
- [x] Profile save/load system
- [x] AutoCAD XREF binding
- [x] Drawing Transmittal
- [x] Combined PDF export
- [x] Export scheduling
- [ ] DWF/DGN format support
- [ ] XLSX export reports
- [ ] Visual filename builder (drag-drop)
- [ ] Rule-based dynamic sheet sets
- [ ] Cloud integration (BIM360/ACC)

---

## 📝 Changelog

### v1.0.0 (2026-05-02)
- 🎉 Initial release
- ✅ Batch export: PDF, DWG, DXF, IFC, NWC, XML, Images
- ✅ Parametric naming with Revit parameters
- ✅ Profile management system (JSON)
- ✅ AutoCAD XREF auto-bind
- ✅ Combined PDF export
- ✅ Drawing Transmittal generation
- ✅ Export scheduling (Once/Daily/Weekly/Monthly)
- ✅ Multi-Revit version support (R20, R25, R27)

---

## 🏢 Supported Revit Versions

| Version | .NET | Status |
|---------|------|--------|
| Revit 2020 | .NET Framework 4.8 | ✅ Supported |
| Revit 2021 | .NET Framework 4.8 | ✅ Supported |
| Revit 2022 | .NET Framework 4.8 | ✅ Supported |
| Revit 2023 | .NET Framework 4.8 | ✅ Supported |
| Revit 2024 | .NET Framework 4.8 | ✅ Supported (Native PDF) |
| Revit 2025 | .NET 8.0 | ✅ Supported |
| Revit 2026 | .NET 8.0 | ✅ Supported |
| Revit 2027 | .NET 8.0 | ✅ Supported |

---

<p align="center">
  <b>Made with ❤️ by Licorp</b><br/>
  <sub>Professional BIM Tools for AEC Industry</sub>
</p>

<p align="center">
  <a href="https://github.com/licorp/Licorp_ExportPlus/issues">Report Bug</a> •
  <a href="https://github.com/licorp/Licorp_ExportPlus/issues">Request Feature</a> •
  <a href="https://github.com/licorp/Licorp_ExportPlus/releases">Download</a>
</p>
