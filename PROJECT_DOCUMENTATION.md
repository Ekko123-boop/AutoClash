# Automated Clash Runner — Technical & Architectural Documentation

## 1. System Overview & Architecture

AutomatedClashRunner is a modular, high-reliability Autodesk Navisworks Manage add-in designed with a clean MVVM (Model-View-ViewModel) architecture.

### High-Level Layers
```
┌─────────────────────────────────────────────────────────────┐
│                       WPF Views                             │
│  - MainWindow.xaml (TabControl: Matrix, Distill, Viewpoints)│
│  - SummaryDialog.xaml (Color-coded items & CSV Export)      │
└──────────────────────────────┬──────────────────────────────┘
                               │ DataBinding / ICommand
┌──────────────────────────────▼──────────────────────────────┐
│                      ViewModels                             │
│  - MainViewModel (Host shell)                               │
│  - MatrixTabViewModel (Model & Set selection, Matrix run)   │
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
│  - Autodesk.Navisworks.Api.dll                              │
│  - Autodesk.Navisworks.Clash.dll                            │
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

### 2.2 SearchSetService
- Traverses the active document's `SelectionSets.RootItem` hierarchy.
- Creates/ensures a designated `Tests` folder in the Navisworks Sets tree.
- Instantiates static `SelectionSet` objects for discovered `.nwc` items.

### 2.3 NamingService
- Strips file extension and leading project code before the first hyphen (e.g. `UCSC-STS-HDLS202-MX.nwc` → `STS-HDLS202-MX`).
- Computes clash test names based on the target manual set:
  - If manual set name is `Base Build` or `BaseBuild` → `STS-HDLS202-MX`
  - Any other manual set → `T-STS-HDLS202-MX`

### 2.4 ClashExecutionService
- Builds the Cartesian product between selected Models and manual Search Sets.
- Skips pre-existing tests.
- Dynamically assigns `SelectionSource` pointers to `SelectionA` (from manual Search Sets).
- Directly sets `SelectionB` to the physical NWC model node from `doc.Models` via `test.SelectionB.Selection.CopyFrom(itemsB)`.
- Bypasses the known Navisworks `new SelectionSourceCollection()` constructor crash.
- Executes tests and gathers execution outcomes into `ExecutionResult`.

### 2.5 ClashDistillerService
- **ReRunTests**: Runs tests against updated model geometry.
- **GroupByElement**: Identifies master named ancestor items in Selection A and applies single-link spatial clustering with the proximity slider (converting feet to Navisworks internal meters via `maxProximityFt * 0.3048`). Moves items in reverse index order to prevent index shifting.
- **ExportViewpoints**: Generates native viewpoints for matching clash results/groups based on active status filters (`New`, `Active`, `Reviewed`, `Approved`, `Resolved`), filing them under dedicated test folders or optional timestamped master folders in `SavedViewpoints`.

### 2.6 LoggerService
- Thread-safe, timestamped logging with auto-rotation (10MB threshold) stored in `%LOCALAPPDATA%\AutomatedClashRunner\Logs\session_YYYY-MM-DD.log`.

---

## 3. Deployment & Multi-Version Support (2022 - 2026)
- **Universal Manifest**: `PackageContents.xml` targets `Platform="NAVMAN" SeriesMin="Nw19" SeriesMax="Nw24"`.
  - Navisworks 2022 (`Nw19`)
  - Navisworks 2023 (`Nw20`)
  - Navisworks 2024 (`Nw21`)
  - Navisworks 2025 (`Nw22`)
  - Navisworks 2026 (`Nw23` / `Nw24`)
- **Bundle Directory**: `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle`. A single bundle installation services all installed Navisworks versions concurrently.
- **Self-Extracting Installer**: `AutomatedClashRunner_Installer.exe` (built via `Installer\Installer.csproj` targeting `net48`). It extracts the bundle payload to `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle`.
- **Build & Packaging Pipeline**: Run `.\build_all.ps1` to compile the plugin, deploy locally, package `bundle.zip`, and compile the installer executable.
