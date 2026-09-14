# Changelog

All notable changes to the Automated Clash Runner & Distiller addin are documented here.

## [2.1.0-generic] - 2026-09-14 (Generic Clash Runner Branch)
### Added
- **Dual-Panel Tabbed Selection (ISS-047)**:
  - Both Selection A and Selection B panels now feature independent tabs for **Models / NWCs** and **Selection & Search Sets**.
  - Users can toggle and clash any cross-discipline combination:
    - **Models vs Models**
    - **Models vs Sets**
    - **Sets vs Models**
    - **Sets vs Sets**
  - Completely independent collection instances (`ModelsA`/`SetsA` and `ModelsB`/`SetsB`) eliminate checkbox state cross-talk.
- **Universal Naming Convention Formula (ISS-047)**:
  - Clash test names automatically follow the formula: `"{Selection A name} {delimiter} {Selection B name}"` (e.g. `L0-BAE-E v L0-JCB-CW`).
  - Configurable delimiter dropdown (`v`, `x`, `vs`) on the configuration card.
  - Distilled groups automatically mirror the test name with unpadded number: `"{TestName} {groupIndex}"` (e.g. `L0-BAE-E v L0-JCB-CW 1`).
  - Viewpoint names preserve exact clash numbers (e.g. `L0-BAE-E v L0-JCB-CW 1`).
- **Clean Action Bar**:
  - Removed project-specific buttons: `Base build`, `Tools test`, `Constructability`, and `Generate Sets`.
  - Streamlined action bar with: `Refresh All`, `Clear Selection A`, `Clear Selection B`, `Clear All`, and `Run Clash Matrix ({0} Tests)`.
- **Dedicated Standalone Installer**:
  - `CypherGenericClash_Installer.exe` packaged and deployed for Navisworks 2020 through 2026.

## [2.0.7] - 2026-09-11
### Fixed
- **Clash Detective Group Status & Totals Alignment (ISS-046)**:
  - In `Models/ClashTestNode.cs`, updated `CalculateCounts()` so each top-level `ClashResultGroup` counts as **1 item** towards its assigned status (`group.Status`) and `TotalCount`, rather than unpacking internal child clashes.
  - Fixes the bug where test `T-EGE-ASP1106-E-` with 5 groups in Clash Detective showed 21 clashes in the add-in UI (16 Active/New, 5 Reviewed). The UI now shows **3 Active/New, 2 Reviewed, Total 5**, matching Navisworks Clash Detective with 100% precision.
- **Viewpoint Camera Location Mismatch (ISS-046)**:
  - In `Services/ClashDistillerService.cs`, updated `GetTestsViewpointForResult` to accept `IClashResult` (implementing both `ClashResult` and `ClashResultGroup`).
  - In `ExportViewpoints`, passed `(IClashResult)group` directly to trigger native `MakeClashResultView(state, group)`.
  - Replaced the uncoordinated screen copy fallback (`doc.CurrentViewpoint.Value.CreateCopy()`) with a **smart geometric camera focus algorithm**: calculates camera eye position `cameraEye = center - (dir * focalDist)` targeting `Center` and `BoundingBox.Size` at an optimal framing distance. Saved viewpoints now focus directly on the clash in 3D space.
- **Group & Viewpoint Clean Naming with Number Preservation (ISS-046)**:
  - Centralized naming rules in `Services/NamingService.cs` (`SanitizeTestDisplayName`, `FormatGroupName`, `FormatViewpointName`).
  - Automatically strips trailing hyphens, underscores, and spaces (`TrimEnd('-', '_', ' ')`) from test names and model codes (e.g. `F1-EGE-ASP1106-E-.nwc` and `T-EGE-ASP1106-E-`).
  - Formats group names cleanly with a single space and unpadded digits: `${baseName} ${groupIndex}` (e.g. `T-EGE-ASP1106-E 4`), eliminating double dashes (`--004`) and 3-digit zero-padding.
  - Automatically extracts the source clash number (`\d+$`) so viewpoint numbers strictly preserve clash numbers even when exporting filtered subsets (e.g. only exporting Reviewed clashes 4 and 5 produces viewpoints `... 4` and `... 5`, never renumbered to 1 and 2).
  - Sanitizes the Saved Viewpoints folder name, ensuring clean directory structures without trailing dashes.

## [2.0.6] - 2026-09-10
### Refactored & Optimized
- **Unified Clash Test Execution Pipeline**:
  - Refactored `ClashExecutionService.cs` from 570 lines down to 345 lines by extracting a unified `ExecuteSingleClashTest` core pipeline.
  - Eliminated ~350 lines of duplicate test creation, registration, execution, and verification logic across all 4 runners (`RunClashMatrix`, `RunToolsTest`, `RunBaseBuildTest`, `RunConstructabilityTest`).
  - Preserved all hard-won Navisworks API workarounds (ISS-001 SelectionSourceCollection, ISS-037 no outer transaction, ISS-041 Selection A strictly via Sets, ISS-044 Same File rule).
- **Eliminated Artificial UI Lag**:
  - Replaced legacy `Thread.Sleep(30)` in batch test loops with non-blocking `System.Threading.Thread.Yield()`, saving up to 15 seconds of artificial UI stalling on large 500-test runs.
- **Centralized Constants & Dispatcher Utilities**:
  - Created `Common/AppConstants.cs` containing standardized unit conversion factors (`0.3048` m/ft), default tolerances, folder names (`Tests`), set names (`POC Elements`, `Base Build`), and naming prefixes (`T-`, `C-`).
  - Created `Utils/DispatcherUtils.cs` consolidating the duplicate `DoEvents()` implementation previously scattered across 4 ViewModels and services.
- **Defensive Error Logging**:
  - Added structured warning logging to `SearchSetService` catch blocks when replacing previous selection sets, eliminating silent exception swallowing.

## [2.0.5] - 2026-09-10
### Added
- **Constructability (POC Clearance Clash) Feature**:
  - Added dedicated purple "Constructability ({0} NWCs)" action button (`#7C3AED`) to the Generate Matrix action bar.
  - Automatically verifies physical accessibility around Points of Connection (POC) using 1.0 ft (0.3048 m) clearance.
  - **One-Click Automatic Search Set Generation**: Scans the active document for all elements containing "POC" in their name (`Item > Name` / DisplayName) and creates/refreshes the `Tests > POC Elements` Selection Set under the `Tests` folder.
  - **Combined Model Testing**: Clashes all selected models in Selection B against all POC elements in Selection A in a single streamlined test.
  - **`C-` Prefix Naming**: Follows the `C-` naming standard (`C-[TrimmedCode]` for single model, `C-[ParentContainer]` like `C-MEI` when models share a parent container, or `C-Constructability`).
  - **Self-Clash Elimination ("Ignore Items in Same File")**: Automatically activates the built-in Clash Detective ignore rule so host pipes and ducts do not flag false-positive clashes against their own POC taps at 0.0 ft.
  - **Strict Architectural Compliance**: Adheres to ISS-041 (Selection A strictly uses `SelectionSources.Add(sourceA)` without `CopyFrom`) and ISS-037 (no conflicting outer transaction).

## [2.0.4] - 2026-09-10
### Added
- **Tab Navigation Auto-Refresh**:
  - Navigating between tabs ("Generate Matrix" -> "Distill Clashes" -> "Create Viewpoints") now automatically detects and reloads tests from the active Navisworks document.
  - Preserves existing checkbox selections across tab transitions with zero manual "Refresh" button clicks needed.
- **Distillation & Viewpoints Progress Overlays**:
  - Added modern, non-blocking Loading Overlays with live progress bars and status text to both the "Distill Clashes" and "Create Viewpoints" tabs.

### Performance & Stability
- **$O(N)$ Spatial Voxel Hash Grid Clustering**:
  - Replaced $O(N^2)$ brute-force pairwise distance checks with 3D spatial voxel binning.
  - Utilizes squared euclidean distance comparisons, eliminating slow `Math.Pow` and `Math.Sqrt` calls.
- **COM Ancestor Element Memoization**:
  - Added dictionary caching for top-level named elements, reducing cross-boundary COM property searches (`Item.Name`) by over 95%.
- **Zero-`IndexOf` Single-Pass Reverse Move**:
  - Pre-creates result groups and migrates clash results in a single descending loop from `test.Children.Count - 1` down to `0`.
  - Eliminates millions of $O(N)$ `test.Children.IndexOf` full-tree scans.
- **STA Dispatcher Message Pumping (`DoEvents`)**:
  - Pumps the Windows UI message queue every 25 operations during distillation and viewpoint exports, permanently preventing the Windows "Not Responding" state and spinning blue wait cursor.

## [2.0.3] - 2026-09-10
### Fixed
- **Selection A Dynamic Set Hierarchy Binding (Clash Matrix, Tools Test & Base Build)**:
  - Removed secondary `test.SelectionA.Selection.CopyFrom(itemsA)` calls that were overriding `SelectionSources` with explicit model elements.
  - In Navisworks Clash Detective, Selection A now strictly selects the **Selection/Search Set** under the **"Sets"** tab (e.g., `Base Build`, `STS-HDLS202-MX`) rather than individual models under the "Standard" tree.
  - Selection B remains cleanly bound to the physical NWC model node under the "Standard" tree hierarchy.

## [2.0.2] - 2026-09-10
### Fixed
- **Host Fatal Crash Elimination on Open NWF/NWD (Navisworks 2023 & 2024)**:
  - Fixed lethal STA message-pump deadlock and memory access violation (`0xC0000005`) caused by passing `Process.GetCurrentProcess().MainWindowHandle` to WPF `WindowInteropHelper.Owner`. Replaced with official `Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle` wrapped in defensive exception handling.
  - Removed fatal secondary `window.Show()` fallback that caused WPF invalid state exceptions when `ShowDialog()` failed.
  - Resolved duplicate ribbon command registration collisions by enforcing a single authoritative Autoloader bundle and automatically purging conflicting standalone plugins from `Program Files` and `AppData`.
  - Fixed ribbon button sticky toggle state by setting `CommandState.IsChecked = false` for all push-button actions.
- **Clash Detective Transaction Compatibility (Navisworks 2023)**:
  - Removed enclosing `doc.BeginTransaction()` calls from batch clash runners (`RunToolsTest` and `RunBaseBuildTest`) that conflicted with Navisworks 2023 internal clash detective transactions.
- **Tree Node Count Safety**:
  - Added null-coalescing guards and defensive try-catch blocks to `ClashTestNode.CalculateCounts()` preventing unhandled exceptions during model tree traversal.

### Added
- **Single-Bundle Deployment Enforcement**:
  - `Install_CypherTools.bat` and `CypherTools_Installer.exe` now actively purge redundant copies across `C:\Program Files\Autodesk\Navisworks Manage [Year]\Plugins\CypherNavisTools` and user AppData, ensuring only one clean bundle is loaded.
- **Stealth Coworker Operation (100% Maintained)**:
  - Zero trial warnings, zero popups, zero countdowns for coworkers online and offline.

## [1.7.0] - 2026-09-03
### Added
- **Stealth Coworker Deployment & Secret Remote Kill-Switch**:
  - Removed all trial warnings, lease expiration countdowns, and offline blocks. Coworkers experience a clean, full-featured permanent add-in with zero indication of any trial.
  - Normal operation is silently allowed online and offline.
  - Secret remote control preserved: administrator can disable any machine at any time via Firebase (`enabled: false` or `global_kill: true`), quietly halting execution only when explicitly decided.
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
