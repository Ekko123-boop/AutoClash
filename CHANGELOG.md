# Changelog

All notable changes to the Automated Clash Runner & Distiller addin are documented here.

## [1.7.0] - 2026-09-03
### Added
- **License Notice & Clear Error Messaging**:
  - `App.cs` now displays a descriptive warning dialog for all license denial scenarios (uninitialized offline lease, grace period expiration, clock tampering, revoked license) rather than silently exiting.
- **Navisworks 2023 Viewpoint Generation Fallback**:
  - Added active document camera viewpoint fallback in `ClashDistillerService` for Navisworks 2020-2023 where `TestsViewpointForResult` is unavailable.
- **UI Responsiveness & Dispatcher Pumping**:
  - Added STA dispatcher render pumping (`DoEvents()`) across all batch clash and selection set generation routines in `MatrixTabViewModel`, preventing "(Not Responding)" UI freezes.
- **In-Place Selection Set Overwrites**:
  - Re-running "Generate Sets" now cleanly replaces outdated sibling sets with the same model code inside the `Tests` folder, preventing duplicate proliferation of `(2)`, `(3)`, etc.
- **Accurate Installer Version Targeting & Validation**:
  - `CypherTools_Installer.exe` now strictly respects the user-checked Navisworks versions in the UI list.
  - Accurate installation status reporting that validates directory operations and reports failures properly.
  - Automatic purging of conflicting legacy standalone plugin folders in AppData.

### Changed
- Migrated local data storage (logs, license leases) to `%LOCALAPPDATA%\CypherNavisTools\` with automatic migration of existing `.lease` files.
- Replaced corrupted mojibake emoji in `SummaryDialog.xaml` with a clean SVG vector icon.
- Replaced non-deterministic `LastOrDefault()` clash test lookups with deterministic name queries in `ClashExecutionService`.
- Updated main window title, header text, and badge from legacy "ACR" to "CYPH" and "Cypher Tools".

## [1.6.0] - 2026-09-02
### Added
- **"Base Build" Automated Clash Test Runner**:
  - Added dedicated orange action button `Base Build ({0} NWCs)` on the Generate Matrix tab.
  - Automatically identifies the document's `Base Build` (or `BaseBuild`) Selection/Search Set.
  - Sets Selection A = Base Build and Selection B = direct selected NWC model.
  - Generates clash tests using clean model codes without the `T-` prefix (e.g. `F1-STS-HDLS201-DR.nwc` -> `STS-HDLS201-DR`).
- **Automated Sibling Selection Set Generator ("Generate Sets")**:
  - Added `Generate Sets` button to the Generate Matrix tab.
  - For each selected NWC (e.g., `F1-STS-HDLS202-MX.nwc` under `F1-MEI - A&B.nwd`), automatically traverses the hierarchy to find the enclosing `.nwd` parent container.
  - Collects all sibling `.nwc` models under that container **excluding the selected NWC itself** (eliminating self-clashing duplicates).
  - Creates static Selection Sets named with the trimmed model code (e.g., `STS-HDLS202-MX`) under a organized `Tests` folder in Navisworks Selection Sets.
  - Instantly refreshes the UI Search Sets list so sets are immediately ready for Tools Test or Matrix execution.
- **Shift + Click Range Multi-Selection Support**:
  - Implemented `ShiftClickBehavior` attached behavior and `ISelectableItem` interface across all 4 ListViews (Models, Search Sets, Distiller Tests, Viewpoints Tests).
  - Allows single-click row checking and Shift+Click range selection.
- **Standalone GUI Multi-Version Installer & Uninstaller (`CypherTools_Installer.exe`)**:
  - Modern dark-themed Windows Forms UI with auto-detection of all installed Navisworks versions (2020 through 2026).
  - Features 1-Click "Install / Update" and 1-Click "Uninstall" buttons.
  - Embeds both Navisworks 2023 and 2024+ payloads in a single self-contained executable.
  - Supports silent command-line flags (`/install`, `/silent`, `/uninstall`).

## [1.5.0] - 2026-08-31
### Added
- **"Cypher Tools" Branding & Dedicated "Cypher" Ribbon Tab**:
  - Implemented official Autodesk `CommandHandlerPlugin` architecture paired with `en-US\CypherRibbon.xaml` declaring the dedicated **"Cypher"** tab.
  - Added 3 direct action buttons: **Clash Matrix**, **Distill Clashes**, and **Create Viewpoints** with 16x16 and 32x32 icons.
  - Preserved fallback entry point under the standard **Tool Add-ins** tab.
- **"Tools test" 1-to-1 Automated Pairing Engine**:
  - Added "Tools test" button to the Matrix tab for instantaneous 1-to-1 clash test creation.
  - Automatically discovers matching Selection Sets by trimmed model name (e.g., `F1-STS-HDLS202-MX.nwc` matches set `STS-HDLS202-MX`).
  - Automatically names generated tests with the required `T-` prefix.
- **Multi-Version Dual Runtime Compilation Engines**:
  - `Release2023` configuration targeting Navisworks 2020-2023 (linking against API `Version=20.0.1382.63`).
  - `Release2024` configuration targeting Navisworks 2024-2026 (linking against API `Version=21.0.0.0`).
  - Multi-target manifest in `PackageContents.xml` declaring dual `<Components>` blocks.
- **Universal 1-Click Multi-Version Installer (`Install_RimoTools.bat`)**:
  - Auto-elevating UAC batch installer that is 100% immune to Windows 11 Smart App Control (SAC) blocks.
  - Auto-scans `C:\Program Files\Autodesk\` for all installed Navisworks versions (2020 to 2026) and deploys the corresponding runtime engine.
  - Automatically purges legacy broken folders (`AutomatedClashRunner`, old builds).

## [1.4.0] - 2026-08-31
### Added
- **Remote Kill-Switch & Silent Cloud License Gate**:
  - Integration with Firebase Realtime Database REST API for instant remote license control and deactivation.
  - Hardware Fingerprinting engine (`HardwareFingerprint.cs`) binding licenses to CPU ID, Motherboard Serial, and Volume Serial Number.
  - AES-256 encrypted 14-day offline lease management (`LicenseService.cs`) stored in `%LOCALAPPDATA%\AutomatedClashRunner\License\.lease`.
  - Silent background auto-registration recording machine name, OS user, and hardware ID in Firebase.
  - Anti-tamper defenses: clock rollback detection, cross-machine lease rejection, and multi-tier secondary validation gates in `App.cs`, `MainViewModel.cs`, and `ClashExecutionService.cs`.
  - String masking and compile-time encryption (`StringProtection.cs`) for cloud endpoints and cryptographic salts.
  - Admin Firebase setup and test utility (`test_firebase.ps1`).

## [1.3.0] - 2026-08-30
### Added
- **Complete UI/UX Pro Max Overhaul**: Redesigned modern interface inspired by Sherlock/Flypaper BIM standards.
- **3-Tab Coordination Workflow**:
  1. `Generate Matrix` (Split card layout, live search, count badges, clash settings, progress overlay).
  2. `Distill Clashes` (Live metric breakdown table with `Active/New`, `Reviewed`, `Approved`, `Resolved`, `Total` columns; `Focused <--> Global` proximity slider; `Distill Selected` & solid green `Distill All Tests` buttons).
  3. `Create Viewpoints` (Live breakdown metrics, status toggle buttons `[New] [Active] [Reviewed] [Approved] [Resolved]`, timestamped master folder option, `Create Viewpoints for Selected` & solid green `Create Viewpoints for All Tests` buttons).
- `ViewpointsTabViewModel` dedicated ViewModel for viewpoint generation.
- Automated XAML validation in build pipeline (`validate_and_save.ps1`).

### Changed
- Selection B explicitly selects standard NWC model hierarchy node via `CopyFrom(ModelItemCollection)` from `doc.Models`.

### Fixed
- Fixed WPF `TabItem` custom template by adding `ContentSource="Header"` and `RecognizesAccessKey="True"`.
- Fixed multi-byte character encoding and sanitized XAML markup to ensure 100% stable `XamlReader` parsing.

## [1.2.0] - 2026-08-30
### Added
- Full MVVM architectural overhaul with separated `MatrixTabViewModel`, `DistillerTabViewModel`, and `SummaryViewModel`.
- Service interfaces (`IModelDiscoveryService`, `ISearchSetService`, `IClashExecutionService`, `IClashDistillerService`, `IDialogService`, `ILoggerService`).
- Live UI control for Clash Test Type (`Clearance`, `Hard`, `Duplicate`) and numeric Tolerance value.
- Progress bar and status overlay during matrix execution.
- Select All / Select None buttons for visible/filtered items on both tabs.
- Selection count badges ("X of Y selected") and empty-state informative messages.
- Single-click CSV export in the Execution Summary dialog.
- Timestamped rotating diagnostic logging in `%LOCALAPPDATA%\AutomatedClashRunner\Logs\`.
- Multi-version Navisworks support for versions 2022 through 2026 in `PackageContents.xml`.
- Comprehensive one-click build and packaging script `build_all.ps1`.

### Fixed
- Fixed coordinate unit conversion in `ClashDistillerService.GroupByElement` to correctly use Navisworks internal meter coordinates (`* 0.3048`).
- Fixed clash group move order to process in reverse index order, eliminating index shift corruption.
- Fixed Selection B in `ClashExecutionService` to use dynamic `SelectionSource` instead of static explicit item copies.
- Fixed null reference vulnerability on `addedTest` during test registration.
- Fixed search set traversal and creation fragility with robust root indexing and error logging.
- Fixed CollectionView filtering to use culture-safe `StringComparison.OrdinalIgnoreCase` with zero UI flicker.

## [1.1.0] - 2026-08-29
### Added
- Introduced the **Distiller / Manage Tests** tab.
- Added Spatial Clustering Grouping by top-level named element with customizable proximity slider.
- Added Re-Run Selected tests feature.
- Added Export Reviewed Viewpoints feature.
- Added dynamic `T-` naming prefix for non-Base Build sets.

## [1.0.0] - 2026-08-25
### Added
- Initial release of Automated Model Clash Runner.
- Automated discovery of `.nwc` discipline models.
- Automated creation of static selection sets.
- Automated pairwise clash matrix generation.
