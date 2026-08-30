# Automated Model Clash Runner & Distiller for Autodesk Navisworks

An enterprise-grade, high-performance Autodesk Navisworks add-in designed for BIM Coordinators and VDC Engineers. It automates clash matrix generation, dynamic search-set creation, spatial clash grouping by element, and viewpoint generation for reviewed clashes.

Supports **Autodesk Navisworks Manage 2022, 2023, 2024, 2025, and 2026**.

---

## Key Capabilities

### 1. Matrix Generator Tab
- **Automated Model Discovery**: Automatically traverses the federated model hierarchy up to 20 levels deep, identifying all appended `.nwc` discipline models.
- **Dynamic Set Wiring**: Generates static selection sets for discovered models under a organized `Tests` folder in Navisworks Selection Sets.
- **Matrix Clash Execution**: Pairs discovered models against user-selected manual Search Sets (e.g., `Base Build`, `Architectural`, `MEP`).
- **Dynamic Prefix Naming Rule**:
  - Sets named `Base Build` or `BaseBuild` generate standard trimmed tests (e.g., `STS-HDLS202-MX`).
  - All other sets automatically receive the `T-` prefix (e.g., `T-STS-HDLS202-MX`).
- **Configurable Clash Test Parameters**: Live UI control for Clash Test Type (`Clearance`, `Hard`, `Duplicate`) and Tolerance value in meters.
- **Dynamic Selection Source Linking**: Uses native `SelectionSource` pointers for both Selection A and Selection B, ensuring clash tests remain fully dynamic and valid across model reloads.

### 2. Distiller / Manage Tests Tab
- **Searchable Test Overview**: Live, instantaneous search and filter across all tests in Clash Detective.
- **Re-Run Selected**: Re-executes chosen clash tests against updated geometry while preserving existing reviewed statuses.
- **Spatial Element Grouping**: Groups raw clashes by their master named element in Selection A and clusters them spatially based on an interactive proximity slider (0 to 150 ft), converted precisely to Navisworks internal meter coordinates.
- **Export Reviewed Viewpoints**: One-click extraction of viewpoints for all `Reviewed` clash groups directly into the Navisworks Saved Viewpoints tree, organized inside dedicated test folders.

### 3. Execution Summary & Reporting
- **Rich Results Dialog**: Visual color-coded summary (Green ✓ for success, Amber ⚠ for skipped, Red ✗ for errors).
- **Clipboard & CSV Export**: One-click export to clipboard or `.csv` spreadsheet for BIM coordination tracking.

---

## Installation & Distribution

### Option A: Standalone Installer (Recommended)
Run **`AutomatedClashRunner_Installer.exe`**. It automatically extracts the plugin bundle into:
`%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle`

### Option B: Manual Bundle Deployment
Copy the `AutomatedClashRunner.bundle` folder directly into:
`%APPDATA%\Autodesk\ApplicationPlugins\`

---

## System Requirements
- **OS**: 64-bit Windows 10 / 11 / Server
- **Host Application**: Autodesk Navisworks Manage (2022 to 2026)
- **Runtime**: .NET Framework 4.8

---

## Building from Source

Open Developer PowerShell or command prompt and run:
```powershell
.\build_all.ps1
```
This single script builds the `net48-windows` plugin DLL, deploys it to your Navisworks ApplicationPlugins folder, and compiles the standalone `AutomatedClashRunner_Installer.exe`.
