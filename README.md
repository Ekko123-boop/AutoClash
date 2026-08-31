# Rimo Tools — Automated Model Clash Runner & Distiller for Autodesk Navisworks

An enterprise-grade, high-performance Autodesk Navisworks add-in designed for BIM Coordinators and VDC Engineers. It automates clash matrix generation, 1-to-1 automated model-to-set test generation ("Tools test"), dynamic search-set creation, spatial clash grouping by element, and viewpoint generation for reviewed clashes.

Supports **Autodesk Navisworks Manage 2020, 2021, 2022, 2023, 2024, 2025, and 2026**.

---

## Key Capabilities

### 1. Dedicated "Rimo" Ribbon Tab
- Clean native ribbon tab with large icons for **Clash Matrix**, **Distill Clashes**, and **Create Viewpoints**.
- Direct fallback registration in the standard **Tool Add-ins** tab.

### 2. Generate Matrix & Tools Test Tab
- **Automated Model Discovery**: Traverses the federated model hierarchy up to 20 levels deep, identifying all appended `.nwc` discipline models.
- **Dynamic Set Wiring**: Generates static selection sets for discovered models under an organized `Tests` folder in Navisworks Selection Sets.
- **Full Matrix Clash Execution**: Pairs discovered models against user-selected manual Search Sets (e.g., `Base Build`, `Architectural`, `MEP`).
- **Tools Test (1-to-1 Automated Pairing)**:
  - Select any number of `.nwc` models and click **Tools test**.
  - Automatically matches each selected `.nwc` to its corresponding Selection Set by trimmed name (e.g. `F1-STS-HDLS202-MX.nwc` matches set `STS-HDLS202-MX`).
  - Sets Selection A to the Selection Set and Selection B to the model.
  - Automatically generates clash tests prefixed with `T-` (e.g., `T-STS-HDLS202-MX`).
- **Dynamic Prefix Naming Rule**:
  - Sets named `Base Build` or `BaseBuild` generate standard trimmed tests (e.g., `STS-HDLS202-MX`).
  - All other sets automatically receive the `T-` prefix (e.g., `T-STS-HDLS202-MX`).
- **Configurable Clash Test Parameters**: Live UI control for Clash Test Type (`Clearance`, `Hard`, `Duplicate`) and Tolerance value in meters.

### 3. Distill Clashes Tab
- **Real-Time Breakdown Metrics**: Live numerical columns for `Active/New`, `Reviewed`, `Approved`, `Resolved`, and `Total` clashes for every test.
- **Searchable Test Overview**: Live, instantaneous search and filter across all tests in Clash Detective.
- **Focused ⟵——|——⟶ Global Proximity Slider**: Fine-tune spatial clustering range from 1 to 150 ft (precisely mapped to Navisworks internal meter coordinates).
- **Spatial Element Grouping**: Groups raw clashes by master named elements in Selection A into clean `{TestName}-{001, 002, ...}` groups.
- **Batch Actions**: Distill selected tests or one-click `Distill All Tests`.

### 4. Create Viewpoints Tab
- **Test Metric Table**: Live breakdown metrics matching Clash Detective counts.
- **Flexible Status Filtering**: Interactive toggle button chips `[ New ] [ Active ] [ Reviewed ] [ Approved ] [ Resolved ]`.
- **Viewpoint Grouping**: Organize generated viewpoints in dedicated test folders or optional timestamped master folders.
- **Batch Creation**: `Create Viewpoints for Selected Test(s)` or `Create Viewpoints for All Tests`.

### 5. Execution Summary & Reporting
- **Rich Results Dialog**: Visual color-coded summary (Green ✓ for success, Amber ⚠ for skipped, Red ✗ for errors).
- **Clipboard & CSV Export**: One-click export to clipboard or `.csv` spreadsheet for BIM coordination tracking.

### 6. Remote License Control & Kill-Switch
- **Cloud Administration**: Instant authorization or revocation via Firebase Realtime Database.
- **Hardware-Locked**: Fingerprints host CPU, motherboard, and system volume serial.
- **14-Day Offline Grace**: Auto-renews silently on connected launches; locks if offline > 14 days or revoked.
- **Anti-Tampering**: Clock rollback detection and deep execution gates.

---

## Installation & Distribution

### 1-Click Universal Installer (Recommended)
Right-click **`Install_RimoTools.bat`** and select **Run as Administrator** (or double-click it):
- Automatically detects all installed Navisworks versions (2020 through 2026).
- Automatically installs the matching runtime engine (2023 vs 2024+).
- Deploys the multi-version bundle to `%ProgramData%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle\`.
- Immune to Windows Smart App Control (SAC) blocks.

---

## System Requirements
- **OS**: 64-bit Windows 10 / 11 / Server
- **Host Application**: Autodesk Navisworks Manage (2020 to 2026)
- **Runtime**: .NET Framework 4.8

---

## Building from Source

Open Developer PowerShell or command prompt and run:
```powershell
.\build_all.ps1
```
This script compiles both the Navisworks 2023 engine and the Navisworks 2024 engine, packages the multi-version `RimoNavisTools.bundle`, and prepares `Install_RimoTools.bat`.
