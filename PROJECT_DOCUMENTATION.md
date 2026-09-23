# Cypher Tools — Technical & Architectural Documentation

## 1. System Overview & Architecture

Cypher Tools (`CypherNavisTools.dll`) is a modular, high-reliability Autodesk Navisworks Manage add-in designed with a clean MVVM (Model-View-ViewModel) architecture.

### High-Level Layers
```
┌─────────────────────────────────────────────────────────────┐
│                   Dedicated Ribbon UI & WPF                 │
│  - "Cypher" Ribbon Tab (Clash Matrix, Distill, Viewpoints)  │
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
- **`GetOrCreatePocSearchSet`**: Discovers all Point of Connection (POC) elements using native search query (`Item > Name` contains `"POC"`) and fallback hierarchy traversal, creating or refreshing the `Tests > POC Elements` Selection Set with duplicate proliferation prevention.

### 2.3 NamingService
- Strips file extension and leading project code before the first hyphen or underscore (e.g. `F1-STS-HDLS202-MX.nwc` → `STS-HDLS202-MX`).
- Computes clash test names based on the target manual set:
  - If manual set name is `Base Build` or `BaseBuild` → `STS-HDLS202-MX`
  - Any other manual set → `T-STS-HDLS202-MX`
- **Tools Test Naming**: `GetToolsTestClashName` prefixes generated 1-to-1 test names with `T-` (e.g., `T-STS-HDLS202-MX`).
- **Base Build Naming**: `GetBaseBuildClashName` produces clean trimmed model code without `T-` prefix.
- **Constructability Naming**: `GetConstructabilityClashName` formats clash tests with `C-` prefix (`C-[TrimmedCode]` for single model, `C-[ParentContainer]` like `C-MEI` when models share a container, or `C-Constructability`).

### 2.4 ClashExecutionService (Unified Execution Pipeline)
- **Unified Single-Test Engine (`ExecuteSingleClashTest`)**:
  - Consolidates test creation, registration, execution, and verification into a single core pipeline method across all 4 runners (`RunClashMatrix`, `RunToolsTest`, `RunBaseBuildTest`, `RunConstructabilityTest`), eliminating ~350 lines of duplicate code.
  - Bypasses known Navisworks `new SelectionSourceCollection()` constructor crash (ISS-001) by directly mutating `SelectionSources.Add(sourceA)`.
  - Strictly enforces ISS-041: Selection A dynamically binds to the Sets tree via `SelectionSources.Add(sourceA)`, while Selection B directly binds to the model hierarchy via `CopyFrom(itemsB)`.
  - Operates without outer document transactions (ISS-037), allowing Navisworks Clash Detective to manage per-test transaction atomicity internally.
  - Replaced legacy blocking `Thread.Sleep(30)` with non-blocking `System.Threading.Thread.Yield()`, saving up to 15 seconds on 500-test runs.
- **Full Matrix Run**: Builds Cartesian product between selected Models and manual Search Sets, skipping existing tests in $O(1)$ time.
- **Tools Test (1-to-1 Automated Pairing)**: Automatically pairs each selected NWC model with its corresponding Selection Set by trimmed name matching, with `T-` prefix.
- **Base Build Clash Runner**: Automatically pairs each selected NWC model with the document's `Base Build` Selection Set with clean model code naming.
- **Constructability (POC Clearance Clash Runner)**: Ensures `Tests > POC Elements` Selection Set is created, clashes all selected models against POCs in a single combined test with `C-` prefix and 1.0 ft (0.3048 m) clearance tolerance, and enables the "Ignore items in same file" rule to eliminate false-positive self-clashes.

### 2.5 ClashDistillerService
- **ReRunTests**: Runs tests against updated model geometry.
- **GroupByDistance**:
  - **$O(N)$ Spatial Voxel Hash Grid**: Uses 3D spatial voxel binning with squared euclidean distance comparisons ($dx^2 + dy^2 + dz^2 \le \text{dist}^2$) rather than brute-force $O(N^2)$ pairwise loops, reducing clustering time on large tests by orders of magnitude. Converts feet to Navisworks internal meters via `maxProximityFt * 0.3048`.
  - **Zero-`IndexOf` Single-Pass Reverse Move**: Pre-creates group containers and moves clash results in a single descending loop from `test.Children.Count - 1` down to `0`, eliminating millions of $O(N)$ `test.Children.IndexOf` scans.
  - **STA Dispatcher Pumping (`DoEvents`)**: Periodically pumps the Windows message loop every 25 moves, preventing the Windows "Not Responding" state and eliminating the blue spinning wait cursor.
- **ExportViewpoints**: Generates native viewpoints for matching clash results/groups based on active status filters (`New`, `Active`, `Reviewed`, `Approved`, `Resolved`), filing them under dedicated test folders or optional timestamped master folders in `SavedViewpoints`. Includes dispatcher message pumping to ensure continuous UI responsiveness.

### 2.6 ShiftClickBehavior & ISelectableItem
- Enables rapid range selection on all 4 WPF `ListView` controls across all tabs (Models, Search Sets, Distiller Tests, Viewpoints Tests).
- Users can click any row or checkbox, then hold `Shift` and click a second row/checkbox to select or deselect the entire range at once.
- TwoWay bound to `IsSelected` on `ListViewItem` container and data models for synchronized row and checkbox updates.
- Attached in code-behind to preserve complete immunity against Windows 11 Smart App Control (`0x800711C7`) build-time XAML reflection blocks.

### 2.7 LoggerService
- Thread-safe, timestamped logging with auto-rotation (10MB threshold) stored in `%LOCALAPPDATA%\CypherNavisTools\Logs\session_YYYY-MM-DD.log`.

### 2.8 LicenseService & Hardware Fingerprinting (Remote Kill-Switch)
- **Stealth Coworker Deployment**: Zero trial warnings, countdowns, or lease expiration popups. Operates seamlessly online and offline for all end users.
- **Cloud Backend**: Connected via REST to Firebase Realtime Database.
- **Hardware Fingerprint**: Deterministic SHA-256 hash derived from `Win32_Processor.ProcessorId`, `Win32_BaseBoard.SerialNumber`, system volume serial, and `MachineGuid`.
- **Silent Auto-Registration**: Quietly registers user name, computer name, OS, and HWID upon first connection.
- **Encrypted Offline Lease**: AES-256-CBC encrypted 14-day lease stored in `%LOCALAPPDATA%\CypherNavisTools\License\.lease` using PBKDF2 key derived from HWID + master salt.
- **Anti-Bypass Protection**: 
  - Primary Gate: `App.cs` entry point before any window is loaded.
  - Secondary Gate: `MainViewModel.cs` async background re-verification.
  - Clock Rollback Defense: Invalidation if local time is rewound behind last-seen UTC timestamp.
### 2.9 Common & Utility Subsystems
- **`Common/AppConstants.cs`**:
  - Centralized single source of truth for Navisworks internal unit conversion (`MetersPerFoot = 0.3048`), default tolerances (`0.0m` standard, `0.3048m` constructability), folder and set names (`Tests`, `POC Elements`, `Base Build`), and naming prefixes (`T-`, `C-`).
- **`Utils/DispatcherUtils.cs`**:
  - Standardized STA dispatcher message pumping (`DoEvents()`) across all tabs and services to maintain smooth UI rendering without stalling.

---

## 3. Multi-Version Architecture & Deployment (Navisworks 2020 - 2026)

### 3.1 Dual Compilation Engines
Because .NET Framework 4.8 enforces strict CLR strong-name version binding on `Autodesk.Navisworks.Api`:
- **Navisworks 2020 - 2023 Target (`Release2023`)**: Compiles against `Version 20.0.1382.63` (`lib\2023\`). Output: `bin\Release\2023\CypherNavisTools.dll`.
- **Navisworks 2024 - 2026 Target (`Release2024`)**: Compiles against `Version 21.0.0.0` (`lib\2024\`). Output: `bin\Release\2024\CypherNavisTools.dll`.

### 3.2 Universal Multi-Version Manifest (`PackageContents.xml`)
```xml
<Components Description="Navisworks 2020-2023">
    <RuntimeRequirements OS="Win64" Platform="NAVMAN|NAVSIM" SeriesMin="Nw17" SeriesMax="Nw20" />
    <ComponentEntry AppName="Cypher Tools" ModuleName="./Contents/2023/CypherNavisTools.dll" AppType="ManagedPlugin" />
</Components>
<Components Description="Navisworks 2024-2026">
    <RuntimeRequirements OS="Win64" Platform="NAVMAN|NAVSIM" SeriesMin="Nw21" SeriesMax="Nw24" />
    <ComponentEntry AppName="Cypher Tools" ModuleName="./Contents/2024/CypherNavisTools.dll" AppType="ManagedPlugin" />
</Components>
```

### 3.3 Universal 1-Click Multi-Version Installer (`Install_CypherTools.bat`)
- Immune to Windows Smart App Control (SAC) blocks.
- Requests Administrator UAC elevation.
- Automatically discovers all installed Navisworks versions under `C:\Program Files\Autodesk\Navisworks*` and installs the matching engine (2023 vs 2024+).
- Deploys the multi-version bundle to `%ProgramData%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle\`.
- Automatically removes legacy broken plugin folders.

