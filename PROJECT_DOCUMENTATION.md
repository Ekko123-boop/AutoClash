# Rimo Tools — Technical & Architectural Documentation

## 1. System Overview & Architecture

Rimo Tools (`RimoNavisTools.dll`) is a modular, high-reliability Autodesk Navisworks Manage add-in designed with a clean MVVM (Model-View-ViewModel) architecture.

### High-Level Layers
```
┌─────────────────────────────────────────────────────────────┐
│                   Dedicated Ribbon UI & WPF                 │
│  - "Rimo" Ribbon Tab (Clash Matrix, Distill, Viewpoints)    │
│  - MainWindow.xaml (TabControl: Matrix, Distill, Viewpoints)│
│  - SummaryDialog.xaml (Color-coded items & CSV Export)      │
└──────────────────────────────┬──────────────────────────────┘
                               │ DataBinding / ICommand
┌──────────────────────────────▼──────────────────────────────┐
│                      ViewModels                             │
│  - MainViewModel (Host shell)                               │
│  - MatrixTabViewModel (Model & Set selection, Tools Test)   │
│  - DistillerTabViewModel (Clustering & Proximity Slider)    │
│  - ViewpointsTabViewModel (Viewpoint generation & filters)  │
│  - SummaryViewModel (Status formatting & Reporting)         │
└──────────────────────────────┬──────────────────────────────┘
                               │ Dependency Injection
┌──────────────────────────────▼──────────────────────────────┐
│                    Service Interfaces                       │
│  - IModelDiscoveryService      - ISearchSetService          │
│  - INamingService              - IClashExecutionService     │
│  - IClashDistillerService      - IDialogService             │
│  - ILoggerService                                           │
└──────────────────────────────┬──────────────────────────────┘
                               │ API Invocations
┌──────────────────────────────▼──────────────────────────────┐
│         Autodesk Navisworks .NET & COM Assemblies           │
│  - Autodesk.Navisworks.Api.dll (2023 / 2024)                │
│  - Autodesk.Navisworks.Clash.dll (2023 / 2024)              │
│  - Autodesk.Navisworks.ComApi.dll                           │
│  - Autodesk.Navisworks.Interop.ComApi.dll                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Core Service Responsibilities

### 2.1 ModelDiscoveryService
- Recursively traverses `doc.Models` up to depth 20.
- Extracts leaf `.nwc` discipline models.
- Wraps them in `ModelSourceNode` with full property notification change guards.
- **`GetSiblingNwcs`**: Traverses up from a target NWC to find the enclosing `.nwd` parent container, collects all sibling `.nwc` models under that container, and excludes the target NWC itself to prevent self-clashing duplicates.

### 2.2 SearchSetService
- Traverses the active document's `SelectionSets.RootItem` hierarchy.
- Creates/ensures a designated `Tests` folder in the Navisworks Sets tree.
- Instantiates static `SelectionSet` objects for discovered `.nwc` items.
- **`GenerateSiblingSearchSet`**: Generates a static `SelectionSet` containing all sibling `.nwc` models under the parent NWD, files it into the `Tests` folder, and handles automatic version naming on collision.

### 2.3 NamingService
- Strips file extension and leading project code before the first hyphen or underscore (e.g. `F1-STS-HDLS202-MX.nwc` → `STS-HDLS202-MX`).
- Computes clash test names based on the target manual set:
  - If manual set name is `Base Build` or `BaseBuild` → `STS-HDLS202-MX`
  - Any other manual set → `T-STS-HDLS202-MX`
- **Tools Test Naming**: `GetToolsTestClashName` prefixes generated 1-to-1 test names with `T-` (e.g., `T-STS-HDLS202-MX`).
- **Base Build Naming**: `GetBaseBuildClashName` produces clean trimmed model code without `T-` prefix.

### 2.4 ClashExecutionService
- **Full Matrix Run**: Builds Cartesian product between selected Models and manual Search Sets, skipping duplicates.
- **Tools Test (1-to-1 Automated Pairing)**:
  - For each selected NWC model, automatically discovers its corresponding Selection Set by trimmed name matching (e.g. `F1-STS-HDLS202-MX.nwc` matches Selection Set `STS-HDLS202-MX`).
  - Sets Selection A to the matching Selection Set and Selection B to the physical NWC model item.
  - Automatically names test with `T-` prefix.
- **Base Build Clash Runner**:
  - Automatically identifies the document's `Base Build` / `BaseBuild` Selection Set.
  - Sets Selection A to Base Build and Selection B to the direct NWC model item.
  - Names generated clash tests with the clean model code (no `T-` prefix).
- Bypasses the known Navisworks `new SelectionSourceCollection()` constructor crash.
- Executes tests and gathers execution outcomes into `ExecutionResult`.

### 2.5 ClashDistillerService
- **ReRunTests**: Runs tests against updated model geometry.
- **GroupByElement**: Identifies master named ancestor items in Selection A and applies single-link spatial clustering with the proximity slider (converting feet to Navisworks internal meters via `maxProximityFt * 0.3048`). Moves items in reverse index order to prevent index shifting.
- **ExportViewpoints**: Generates native viewpoints for matching clash results/groups based on active status filters (`New`, `Active`, `Reviewed`, `Approved`, `Resolved`), filing them under dedicated test folders or optional timestamped master folders in `SavedViewpoints`.

### 2.6 ShiftClickBehavior & ISelectableItem
- Enables rapid range selection on all 4 WPF `ListView` controls across all tabs (Models, Search Sets, Distiller Tests, Viewpoints Tests).
- Users can click any row or checkbox, then hold `Shift` and click a second row/checkbox to select or deselect the entire range at once.
- Attached in code-behind to preserve complete immunity against Windows 11 Smart App Control (`0x800711C7`) build-time XAML reflection blocks.

### 2.6 LoggerService
- Thread-safe, timestamped logging with auto-rotation (10MB threshold) stored in `%LOCALAPPDATA%\AutomatedClashRunner\Logs\session_YYYY-MM-DD.log`.

### 2.7 LicenseService & Hardware Fingerprinting (Remote Kill-Switch)
- **Cloud Backend**: Connected via REST to Firebase Realtime Database.
- **Hardware Fingerprint**: Deterministic SHA-256 hash derived from `Win32_Processor.ProcessorId`, `Win32_BaseBoard.SerialNumber`, system volume serial, and `MachineGuid`.
- **Silent Auto-Registration**: Quietly registers user name, computer name, OS, and HWID upon first connection.
- **Encrypted Offline Lease**: AES-256-CBC encrypted 14-day lease stored in `%LOCALAPPDATA%\AutomatedClashRunner\License\.lease` using PBKDF2 key derived from HWID + master salt.
- **Anti-Bypass Protection**: 
  - Primary Gate: `App.cs` entry point before any window is loaded.
  - Secondary Gate: `MainViewModel.cs` async background re-verification.
  - Tertiary Gate: `ClashExecutionService.cs` pre-matrix execution check.
  - Clock Rollback Defense: Invalidation if local time is rewound behind last-seen UTC timestamp.
- **Remote Revocation**: Setting `enabled: false` on the target machine HWID or `global_kill: true` in Firebase immediately destroys local lease and halts execution.

---

## 3. Multi-Version Architecture & Deployment (Navisworks 2020 - 2026)

### 3.1 Dual Compilation Engines
Because .NET Framework 4.8 enforces strict CLR strong-name version binding on `Autodesk.Navisworks.Api`:
- **Navisworks 2020 - 2023 Target (`Release2023`)**: Compiles against `Version 20.0.1382.63` (`lib\2023\`). Output: `bin\Release\2023\RimoNavisTools.dll`.
- **Navisworks 2024 - 2026 Target (`Release2024`)**: Compiles against `Version 21.0.0.0` (`lib\2024\`). Output: `bin\Release\2024\RimoNavisTools.dll`.

### 3.2 Universal Multi-Version Manifest (`PackageContents.xml`)
```xml
<Components Description="Navisworks 2020-2023">
    <RuntimeRequirements OS="Win64" Platform="NAVMAN|NAVSIM" SeriesMin="Nw17" SeriesMax="Nw20" />
    <ComponentEntry AppName="Rimo tools" ModuleName="./Contents/2023/RimoNavisTools.dll" AppType="ManagedPlugin" />
</Components>
<Components Description="Navisworks 2024-2026">
    <RuntimeRequirements OS="Win64" Platform="NAVMAN|NAVSIM" SeriesMin="Nw21" SeriesMax="Nw24" />
    <ComponentEntry AppName="Rimo tools" ModuleName="./Contents/2024/RimoNavisTools.dll" AppType="ManagedPlugin" />
</Components>
```

### 3.3 Universal 1-Click Multi-Version Installer (`Install_RimoTools.bat`)
- Immune to Windows Smart App Control (SAC) blocks.
- Requests Administrator UAC elevation.
- Automatically discovers all installed Navisworks versions under `C:\Program Files\Autodesk\Navisworks*` and installs the matching engine (2023 vs 2024+).
- Deploys the multi-version bundle to `%ProgramData%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle\`.
- Automatically removes legacy broken plugin folders.

