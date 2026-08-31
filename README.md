# Automated Model Clash Runner & Distiller for Autodesk Navisworks

An enterprise-grade, high-performance Autodesk Navisworks add-in designed for BIM Coordinators and VDC Engineers. It automates clash matrix generation, dynamic search-set creation, spatial clash grouping by element, and viewpoint generation for reviewed clashes.

Supports **Autodesk Navisworks Manage 2022, 2023, 2024, 2025, and 2026**.

---

## Key Capabilities

### 1. Generate Matrix Tab
- **Automated Model Discovery**: Automatically traverses the federated model hierarchy up to 20 levels deep, identifying all appended `.nwc` discipline models.
- **Dynamic Set Wiring**: Generates static selection sets for discovered models under an organized `Tests` folder in Navisworks Selection Sets.
- **Matrix Clash Execution**: Pairs discovered models against user-selected manual Search Sets (e.g., `Base Build`, `Architectural`, `MEP`).
- **Dynamic Prefix Naming Rule**:
  - Sets named `Base Build` or `BaseBuild` generate standard trimmed tests (e.g., `STS-HDLS202-MX`).
  - All other sets automatically receive the `T-` prefix (e.g., `T-STS-HDLS202-MX`).
- **Configurable Clash Test Parameters**: Live UI control for Clash Test Type (`Clearance`, `Hard`, `Duplicate`) and Tolerance value in meters.
- **Standard Hierarchy Selection B**: Directly references physical `.nwc` model nodes from `doc.Models` for standard hierarchy clashing.

### 2. Distill Clashes Tab
- **Real-Time Breakdown Metrics**: Live numerical columns for `Active/New`, `Reviewed`, `Approved`, `Resolved`, and `Total` clashes for every test.
- **Searchable Test Overview**: Live, instantaneous search and filter across all tests in Clash Detective.
- **Focused ⟵——|——⟶ Global Proximity Slider**: Fine-tune spatial clustering range from 1 to 150 ft (precisely mapped to Navisworks internal meter coordinates).
- **Spatial Element Grouping**: Groups raw clashes by master named elements in Selection A into clean `{TestName}-{001, 002, ...}` groups.
- **Batch Actions**: Distill selected tests or one-click `Distill All Tests`.

### 3. Create Viewpoints Tab
- **Test Metric Table**: Live breakdown metrics matching Clash Detective counts.
- **Flexible Status Filtering**: Interactive toggle button chips `[ New ] [ Active ] [ Reviewed ] [ Approved ] [ Resolved ]`.
- **Viewpoint Grouping**: Organize generated viewpoints in dedicated test folders or optional timestamped master folders.
- **Batch Creation**: `Create Viewpoints for Selected Test(s)` or `Create Viewpoints for All Tests`.

### 4. Execution Summary & Reporting
- **Rich Results Dialog**: Visual color-coded summary (Green ✓ for success, Amber ⚠ for skipped, Red ✗ for errors).
- **Clipboard & CSV Export**: One-click export to clipboard or `.csv` spreadsheet for BIM coordination tracking.

### 5. Remote License Control & Kill-Switch
- **Cloud Administration**: Instant authorization or revocation via Firebase Realtime Database.
- **Hardware-Locked**: Fingerprints host CPU, motherboard, and system volume serial.
- **14-Day Offline Grace**: Auto-renews silently on connected launches; locks if offline > 14 days or revoked.
- **Anti-Tampering**: Clock rollback detection and deep execution gates.

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

---

## Architecture Note: Multi-Version Support
Unlike other Navisworks addins that utilize massive Multi-Folder `Contents/2022`, `Contents/2023` directory structures, this plugin compiles into a **single, unified binary** supporting Navisworks 2022-2026.
This is achieved by natively compiling against the **Navisworks 2023 API via NuGet**, injecting `<SpecificVersion>False</SpecificVersion>` to allow dynamic .NET CLR upgrades in 2024+, and utilizing `System.Reflection` to dynamically invoke newer features (like `TestsViewpointForResult`) if they are detected in the host engine.
