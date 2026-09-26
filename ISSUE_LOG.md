# Automated Clash Runner & Distiller — Comprehensive Project Issue Log

This document records the full engineering history, bugs encountered, root cause analyses, and architectural solutions implemented across the lifecycle of the **Automated Clash Runner & Distiller** project.

---

## Issue Registry

| ID | Category | Component | Symptom | Root Cause | Resolution |
|---|---|---|---|---|---|
| **ISS-001** | Navisworks API | `ClashExecutionService` | `System.AccessViolationException` crashing host on test creation | Calling `new SelectionSourceCollection()` in .NET triggers uncatchable JIT memory violation in Navisworks Manage 2024+. | Directly mutate `test.SelectionA.Selection.SelectionSources.Add(source)` on the existing collection instead of instantiating a new collection. |
| **ISS-002** | Model Discovery | `ModelDiscoveryService` | Revit-exported `.nwc` models not found or empty scopes | Searching by property `Item > Source File Name` is fragile; Navisworks often aliases `.rvt` or drops file extensions in properties. | Traverse `doc.Models` hierarchy directly and identify `.nwc` nodes via `ModelItem.DisplayName.EndsWith(".nwc")`. |
| **ISS-003** | Dynamic Linking | `SearchSetService` | Model search criteria lost or failing across file renames | Generating XML query search sets via string queries breaks when categories differ between disciplines. | Create native **Static SelectionSets** referencing the physical discovered `ModelItem` node and file under a dedicated `Tests` folder. |
| **ISS-004** | Build Tooling | MSBuild / .NET SDK | `MC1000: Unknown build error, Could not find assembly System.Private.CoreLib` | Modern SDK-style `.csproj` projects in preview .NET SDKs (e.g. .NET 10.0.301) inject .NET Core runtime references into `net48-windows` WPF targets during XAML markup compilation. | Converted `.csproj` to MSBuild 15.0 legacy format (`Microsoft.CSharp.targets`), completely isolating the build from .NET Core CLI interference. |
| **ISS-005** | Units & Spatial Math | `ClashDistillerService` | Clash grouping proximity slider inaccurate on Imperial / Metric projects | Assumed `ClashResult.Center` returns coordinates in `doc.Units`. In reality, Navisworks internal coordinates are **always in meters**. | Removed the `doc.Units` switch. All distance calculations now convert the slider feet value directly to meters (`maxProximityFt * 0.3048`). |
| **ISS-006** | Data Integrity | `ClashDistillerService` | Clashes placed in wrong groups or `IndexOutOfRangeException` | Moving raw clash results into `ClashResultGroup` using `TestsMove` mutates `test.Children` during iteration, shifting the indices of subsequent items. | Collect all target moves and execute them in **reverse index order** (descending), ensuring preceding indices remain unchanged during mutation. |
| **ISS-007** | Clash Detective | `ClashExecutionService` | "No items in Selection B" after reloading an appended model | Selection B was populated via `CopyFrom(ModelItemCollection)` snapshot which becomes stale when geometry is re-parsed. | Created a static `SelectionSet` for the model and linked it to Selection B dynamically via `doc.SelectionSets.CreateSelectionSource(set)`. |
| **ISS-008** | Null Safety | `ClashExecutionService` | `NullReferenceException` when running clash tests | `TestsAddCopy` can occasionally return null if duplicate names or memory limits are hit. | Added explicit null verification on `addedTest` before invoking `TestsRunTest(addedTest)`. |
| **ISS-009** | Architecture | `MainViewModel` | UI freezing, untestable code, memory leaks | Monolithic ViewModel holding WPF `Window` reference, subscribing to anonymous event lambdas without unhooking, and calling `MessageBox.Show()`. | Refactored into separate `MatrixTabViewModel`, `DistillerTabViewModel`, and `SummaryViewModel` with injected `IDialogService`, `ILoggerService`, and unhooked event listeners. |
| **ISS-010** | UI Responsiveness | `MainViewModel` | UI list flicker and scroll jumps on every keystroke | `Filter()` method was calling `ObservableCollection.Clear()` and rebuilding collections on every key press. | Implemented `ICollectionView` filtering using `StringComparison.OrdinalIgnoreCase` with zero UI flicker and retained scroll state. |
| **ISS-011** | Packaging | `PackageContents.xml` | Add-in not loading on Navisworks 2025/2026 | `SeriesMax` was locked to `Nw22` (Navisworks 2024). | Updated manifest to `SeriesMin="Nw19" SeriesMax="Nw24"`, enabling seamless loading across Navisworks 2022 to 2026. |
| **ISS-012** | Distribution | `Installer` | End users unable to install without manual copy | Manual installation required navigating to hidden `%APPDATA%` folders. | Created self-extracting standalone `AutomatedClashRunner_Installer.exe` targeting `net48` that automatically extracts the bundle. |
| **ISS-013** | Workflow Preference | `ClashExecutionService` | Selection B using Search Sets instead of standard NWC model node | Search Sets for Selection B altered the user's legacy workflow where Selection B points directly to standard NWC model tree nodes. | Reverted Selection B to populate directly via `CopyFrom(ModelItemCollection)` referencing the physical NWC model node from `doc.Models`. |
| **ISS-014** | UI/UX & Metrics | `Views/MainWindow.xaml`, ViewModels | UI lacked breakdown metrics and modern BIM coordination aesthetics | Previous UI had basic list views without clash status breakdown (`Active/New`, `Reviewed`, `Approved`, `Resolved`, `Total`). | Overhauled UI into a modern 3-tab layout (**Generate Matrix**, **Distill**, **Viewpoints**) matching AEC industry standards with live metrics and proximity sliders. |
| **ISS-015** | WPF XAML Parsing | `Views/MainWindow.xaml` | `UIElementCollection` error / Blank tab headers | 1) Multi-byte non-ASCII characters corrupted XML tag bounds; 2) Custom `TabItem` template omitted `ContentSource="Header"`. | Sanitized XAML to standard ASCII markup, added `ContentSource="Header"` to `TabItem`, and added pre-deployment automated XAML validation via `XamlReader.Parse()`. |
| **ISS-017** | CLR Assembly Binding | `AutomatedClashRunner.csproj` | Plugin silently dropped on Navisworks Manage 2024 startup | Compiled against Navisworks 2023 API (`Version=20.0.1382.63`). Navisworks 2024 runs with `Version=21.0.0.0`. .NET CLR strong-name validation fails with `ReflectionTypeLoadException: LoaderException: Could not load file or assembly 'Autodesk.Navisworks.Api, Version=20.0...`. | Configured dual build targets: `Release2023` (Version 20.0) and `Release2024` (Version 21.0), compiling separate binaries for each major engine. |
| **ISS-018** | OS Security & WDAC | `Installer`, `%APPDATA%` | Windows Smart App Control (SAC) blocks `.exe` installer and AppData DLLs (`0x800711C7`) | Windows 11 SAC blocks unsigned binaries executed from user space (`%APPDATA%`, `Downloads`). | Created `Install_RimoTools.bat` which requests standard Windows UAC elevation and copies verified binaries to `C:\Program Files\Autodesk\Navisworks Manage 2024\Plugins\`, which is in the OS trusted path. |
| **ISS-019** | Ribbon UI Integration | `App.cs`, `en-US/RimoRibbon.xaml` | User requested dedicated "Rimo" Ribbon tab with 3 action buttons | Previous setup only implemented fallback `AddInPlugin` under Tool Add-ins tab. | Implemented official Autodesk `CommandHandlerPlugin` architecture with `[RibbonLayout("RimoRibbon.xaml")]` and `[RibbonTab("Rimo")]` binding to 3 discrete command IDs. |
| **ISS-020** | Feature Addition | `MatrixTabViewModel`, `NamingService` | 1-to-1 NWC to Selection Set automated pairing ("Tools test") | User had to manually configure search sets for every test. | Added "Tools test" button with automatic trimmed name matching (e.g. `F1-STS-HDLS202-MX.nwc` -> `STS-HDLS202-MX`) and automated test creation with `T-` prefix. |
| **ISS-021** | Feature Addition | `ClashExecutionService`, `MatrixTabViewModel` | Need automated clash tests against "Base Build" selection set without T- prefix | Users had to configure Base Build clashes manually or via full matrix product. | Added dedicated "Base Build" test runner button that automatically sets Selection A to the Base Build set, Selection B to the NWC model, and generates clean model code test names without `T-`. |
| **ISS-022** | Selection Sets | `SearchSetService`, `ModelDiscoveryService` | Need automated creation of selection sets containing sibling NWCs under parent NWD minus target NWC | Creating selection sets manually for each tool is tedious and prone to accidental self-clash inclusion. | Added "Generate Sets" engine: finds enclosing parent `.nwd` container, extracts all sibling `.nwc` items, excludes target NWC, and creates a static SelectionSet in `Tests` folder named after the trimmed model code. |
| **ISS-023** | WPF / MSBuild / SAC | `MainWindow.xaml`, `MainWindow.xaml.cs` | `error MC2000: Could not load file or assembly ... An Application Control policy has blocked this file. (0x800711C7)` | Declaring local types (`xmlns:views`) in XAML triggers `MarkupCompilePass2` which attempts to load unsigned temporary DLLs via CLR `Assembly.LoadFrom`, blocked by Windows 11 Smart App Control. | Attached behaviors and behaviors are applied programmatically in code-behind (`MainWindow.xaml.cs`) to keep XAML free of local assembly reflection during build. |
| **ISS-024** | UI Selection | `ShiftClickBehavior.cs` | Shift+Click range selection was not selecting intermediate items when clicking checkboxes | Click event was returning early on checkbox clicks without updating anchor index, and target state inverted the anchor state. | Refactored `ShiftClickBehavior` to track anchor indices across all row and checkbox clicks and apply the active selection state across the full index range. |
| **ISS-025** | UI DataBinding | `MainWindow.xaml`, `ItemContainerStyle` | Selecting rows visually highlighted them but checkboxes remained unchecked | `ListViewItem.IsSelected` container state was not bound TwoWay to `ISelectableItem.IsSelected`. | Implemented `MultiSelectListViewItemStyle` with `<Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}" />` and applied `SelectionMode="Extended"` across all 4 ListViews. |
| **ISS-026** | UI/UX Design | `Images/`, `MainWindow.xaml` | Ribbon and button icons lacked visual distinction and polish | Default raster icons were generic. | Generated high-DPI vector-rendered 32x32 and 16x16 ribbon PNG icons with anti-aliased gradients, and added high-contrast SVG vector glyphs to all WPF buttons. |
| **ISS-027** | Branding | Solution-wide | Project rebrand from "Rimo" to "Cypher" | Rebranding required across assemblies, ribbon tabs, command IDs, installers, and manifests. | Renamed assembly to `CypherNavisTools.dll`, ribbon tab to `Cypher`, installer to `CypherTools_Installer.exe`, bundle to `CypherNavisTools.bundle`, and updated all manifest references. |
| **ISS-028** | License & Auth | `App.cs`, `LicenseService.cs` | Silent exit on license denial when not revoked (missing lease, grace expiry, clock rollback) | Dialog only displayed if `IsRevoked` was true. Other denial states exited silently. | Dialog now always displays the descriptive `Message` explaining why access is denied (e.g. offline grace expired, internet required). |
| **ISS-029** | Storage Migration | `LicenseService.cs`, `LoggerService.cs` | Storage paths still pointing to legacy `AutomatedClashRunner` directories | Logs and lease data were written to legacy paths. | Updated directories to `CypherNavisTools` and implemented automatic migration of existing `.lease` files from the legacy folder. |
| **ISS-030** | Multi-Version | `ClashDistillerService.cs` | Viewpoints creation created 0 viewpoints on Navisworks 2023 | `TestsViewpointForResult` is a 2024+ API method; reflection returned null on 2023. | Implemented active camera fallback (`doc.CurrentViewpoint.Value.CreateCopy()`) so viewpoints generate properly on 2020-2023. |
| **ISS-031** | Clash Engine | `ClashExecutionService.cs`, `SearchSetService.cs` | Non-deterministic test lookup and duplicate `(2)`, `(3)` selection sets | `Tests.LastOrDefault()` failed if tests were grouped, and repeated set generation created endless duplicates. | Test lookup now filters by exact name; set generator overwrites previous sets with the same name in `Tests` folder in-place. |
| **ISS-032** | WPF Threading | `MatrixTabViewModel.cs` | UI freezing / "(Not Responding)" during large batch clash matrix runs | STA execution on UI thread starved the WPF render loop of time slices. | Implemented `DoEvents()` dispatcher render pump in all progress callbacks so progress bars and UI update smoothly. |
| **ISS-033** | Installer & Deploy | `Installer/Program.cs`, `Install_CypherTools.bat`, `build_all.ps1` | Ignored checked versions in installer list, duplicate plugin loading across bundle and user directory | Installer deployed to all versions regardless of check state, and dual deployment caused duplicate tabs in Navisworks. | Installer now respects checked versions filter, returns accurate success status, and build/install scripts purge duplicate user plugin folders. |
| **ISS-034** | License & UX | `LicenseService.cs`, `App.cs`, `MainViewModel.cs`, `ClashExecutionService.cs` | User wanted completely silent stealth operation for coworkers without trial warnings or countdowns | Prior implementation showed popups when offline lease was missing or grace period expired. | Eliminated all trial/lease warnings and offline blocks; default access is silently allowed online and offline; remote kill-switch operates quietly via Firebase (`enabled = false` / `global_kill = true`), saving local `.revoked` state only when explicitly triggered by administrator. |
| **ISS-035** | UI/UX | `MainWindow.xaml` | Proximity slider allowed 0 ft or oversized 2000 ft range | Default range 1-2000 ft was excessive for standard MEP coordination; 0 ft caused zero-radius clustering errors. | Constrained range to 1 - 300 ft with 10 ft snap ticks and 25 ft default. |
| **ISS-036** | Viewpoints | `ViewpointsTabViewModel.cs`, `ClashDistillerService.cs` | Viewpoints clutter root viewpoint tree without timestamping | Multiple runs generated viewpoints loose or unorganized. | Added optional grouped master folder stamped with current date and time (`Clash Viewpoints - yyyy-MM-dd HH.mm.ss`). |
| **ISS-037** | Clash Engine | `ClashExecutionService.cs` | Execution aborted on Navisworks 2023 when creating Base Build or Tools tests | Wrapping batch test runs in a parent `doc.BeginTransaction(...)` conflicted with Navisworks internal clash test execution transactions in 2023, causing transaction abort exceptions. | Removed enclosing transaction from `RunToolsTest` and `RunBaseBuildTest`; Navisworks handles test creation and execution atomicity per test internally. |
| **ISS-038** | Tree Traversal | `ClashTestNode.cs` | `NullReferenceException` during tree count calculation | Models or folders with null item collections caused crashes during recursive status aggregation. | Wrapped `CalculateCounts()` in null-coalescing guards and defensive try-catch blocks. |
| **ISS-039** | Multi-Version | `ClashDistillerService.cs` | Reflection failure accessing `TestsViewpointForResult` in 2023 | Method does not exist in Navisworks 2020-2023 API; unhandled invocation crashed viewpoint generation. | Implemented graceful fallback: queries active viewpoint camera via `doc.CurrentViewpoint.Value.CreateCopy()` when the 2024 API method is unavailable. |
| **ISS-040** | Stability / Host Fatal Crash | `App.cs`, `Installer/Program.cs`, `Install_CypherTools.bat` | Navisworks Manage 2023 & 2024 immediately crashed with `0xC0000005` Access Violation upon clicking any add-in button when an NWF/NWD model was open | 1) `App.LaunchApp()` set `WindowInteropHelper.Owner = Process.GetCurrentProcess().MainWindowHandle` which in `roamer.exe` returns an unmanaged worker/message window, deadlocking the STA message pump. 2) Duplicate plugin installations (`Program Files` + `ProgramData` + `AppData`) loaded conflicting ribbon command hooks into the MFC command table. 3) Catch block attempted fatal `window.Show()` retry after failed `ShowDialog()`. | 1) Replaced `Process.MainWindowHandle` with native MFC handle `Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle` guarded by try-catch. 2) Standardized deployment to a single authoritative bundle and purged duplicate standalone plugins from `Program Files` and `AppData`. 3) Set non-toggle button ribbon states to `IsChecked = false`. 4) Removed invalid `window.Show()` retry. |
| **ISS-041** | Selection Hierarchy | `ClashExecutionService.cs` | Selection A selected individual models instead of Sets in Clash Detective | Secondary call to `test.SelectionA.Selection.CopyFrom(itemsA)` converted the selection from dynamic Set mode to explicit Standard ModelItem mode. | Removed `CopyFrom(itemsA)` from all test runners (`RunClashMatrix`, `RunToolsTest`, `RunBaseBuildTest`). Selection A strictly uses `doc.SelectionSets.CreateSelectionSource(set)` added to `test.SelectionA.Selection.SelectionSources.Add(sourceA)` to bind to the Sets tree. Selection B strictly uses `CopyFrom(itemsB)` to bind to the model hierarchy. |
| **ISS-042** | Performance & Host Freezing | `ClashDistillerService.cs`, `DistillerTabViewModel.cs`, `MainWindow.xaml` | Distilling large clash tests caused prolonged freezing and Windows "Not Responding" spinning blue cursor | 1) Pairwise $O(N^2)$ euclidean distance checks with `Math.Pow` and `Math.Sqrt`. 2) Traversed deep COM item reflection (`PropertyCategories.FindPropertyByDisplayName`) for every clash. 3) Repeated $O(N)$ full-tree scans via `test.Children.IndexOf` in move loops. 4) Zero message pumping on STA thread during long move operations. 5) Distiller tab lacked a loading progress overlay. | 1) Implemented 3D Spatial Voxel Grid binning with squared distance ($O(N)$ amortized single-linkage clustering). 2) Added `Dictionary<ModelItem, ModelItem>` ancestor memoization cache. 3) Pre-created groups and moved items in a single backwards pass from `test.Children.Count - 1` down to `0` with zero `IndexOf` scans. 4) Added periodic `DoEvents()` WPF dispatcher pumping every 25 operations. 5) Added modern Loading Overlay with live progress bars to Distill and Viewpoints tabs. |
| **ISS-043** | UX / Navigation | `MainViewModel.cs` | Newly generated clash tests did not appear upon switching from "Generate Matrix" to "Distill Clashes" without clicking manual "Refresh Tests" button | `SelectedTabIndex` setter did not notify or invoke `DistillerTab.LoadTests()` or `ViewpointsTab.LoadTests()`. | Updated `SelectedTabIndex` setter in `MainViewModel` to automatically invoke `DistillerTab?.LoadTests()` when switching to tab 1 and `ViewpointsTab?.LoadTests()` when switching to tab 2, preserving existing test checkbox selections. |
| **ISS-044** | Feature / Constructability | `ClashExecutionService.cs`, `SearchSetService.cs`, `NamingService.cs`, `MainWindow.xaml` | Need automated clearance check to verify physical accessibility around Points of Connection (POC) without false-positive self-clashes | In MEP models, POC taps are attached to host ducts/pipes at distance 0.0 ft. Running standard clearance clash flags the host duct against its own tap. Also, manual POC set creation on new NWFs was tedious. | 1) Enabled "Ignore items in same file" rule on `ClashTest.IgnoreRules`, ignoring clashes where both items share the same NWC file. 2) Implemented one-click `GetOrCreatePocSearchSet` which automatically queries elements with "POC" in their name and creates `Tests > POC Elements`. 3) Clashes all selected models against POCs in a single combined test with `C-` prefix (e.g. `C-MEI`). 4) Selection A strictly binds to Sets tree via `SelectionSources.Add(sourceA)` per ISS-041. |
| **ISS-045** | Architecture & Performance | `ClashExecutionService.cs`, `DispatcherUtils.cs`, `AppConstants.cs` | Code duplication (~350 lines) across 4 clash test runners, artificial UI lag from `Thread.Sleep(30)` in inner loops, duplicate `DoEvents` implementations, and scattered hardcoded constants | 1) The 4 clash runners (`RunClashMatrix`, `RunToolsTest`, `RunBaseBuildTest`, `RunConstructabilityTest`) repeated ~75% identical test creation, registration, execution, and verification logic. 2) Legacy `Thread.Sleep(30)` added 15s of artificial lag on 500-test runs. 3) `DoEvents()` was copied across 4 classes. 4) Magic numbers (`0.3048`, `10*1024*1024`, `20`) lacked central definitions. | 1) Refactored `ClashExecutionService` to use a unified `ExecuteSingleClashTest` pipeline, reducing file size from 570 to 345 lines with zero code duplication while preserving all API workarounds (ISS-001, ISS-037, ISS-041, ISS-044). 2) Replaced blocking `Thread.Sleep(30)` with non-blocking `Thread.Yield()`. 3) Centralized dispatcher pump into `Utils/DispatcherUtils.cs`. 4) Created `Common/AppConstants.cs` for units, set names, and folder conventions. 5) Added structured warning logs to silent exception catches in `SearchSetService`. |
| **ISS-046** | Consistency & Viewpoints | `ClashTestNode.cs`, `ClashDistillerService.cs`, `NamingService.cs` | 1) Clash counts unpacked group children showing 21 clashes instead of 5 groups. 2) Exported viewpoints stamped user's active viewport screen instead of looking at the clash. 3) Group and viewpoint names had double hyphens and zero-padding (`T-EGE-ASP1106-E--004`). | 1) `CalculateCounts()` added `group.Children.Count` to group status counts rather than counting each group as 1 top-level row. 2) `ExportViewpoints` passed child `RepresentativeResult` instead of `(IClashResult)group`, and fell back to `doc.CurrentViewpoint.Value.CreateCopy()`. 3) Distiller string formatting did not strip trailing hyphens from test names and padded numbers. | 1) Count each top-level group as 1 item in `CalculateCounts()`, matching Clash Detective. 2) Pass `(IClashResult)group` to `TestsViewpointForResult` and implement smart geometric camera fallback focusing on `Center` and `BoundingBox`. 3) Implement `FormatGroupName` and `FormatViewpointName` in `NamingService` to strip trailing delimiters and format clean `${baseName} ${index}` (e.g. `EGE-ASP1106-E 4`) while strictly matching clash numbers. |
| **ISS-047** | Architecture & UI/UX | `MatrixTabViewModel.cs`, `MainWindow.xaml`, `NamingService.cs`, `ClashExecutionService.cs`, `Program.cs`, `build_all.ps1` | Hardcoded project-specific workflows (Tools Test, Base Build, Constructability, Generate Sets) and single fixed model-to-set layout restricted universal multi-project adoption | Previous layout restricted left panel to Models and right panel to Sets with hardcoded prefixes (`T-`, `C-`). Users could not run Model vs Model or Set vs Set cross-clashes, and project-specific buttons cluttered the UI. | 1) Transformed Selection A and Selection B panels into dual tabs (`Models` / `Sets`), supporting all 4 combinations (Model vs Model, Model vs Set, Set vs Model, Set vs Set). 2) Separated node collections (`ModelsA`/`SetsA` and `ModelsB`/`SetsB`) preventing checkbox cross-talk. 3) Implemented universal naming: `"{Selection A} {delimiter} {Selection B}"` (delimiter `v`, `x`, `vs`) with unpadded group naming `"{TestName} {groupIndex}"`. 4) Removed customized buttons; provided clean action bar with `Run Clash Matrix ({0} Tests)`. 5) Created dedicated standalone installer `CypherGenericClash_Installer.exe` and isolated Git branch `generic-clash-runner`. |
| **ISS-048** | Model & Sets Discovery / UI / Sorting | `ModelDiscoveryService.cs`, `SearchSetService.cs`, `ModelSourceNode.cs`, `MatrixTabViewModel.cs`, `MainWindow.xaml` | 1) Sets tab showed 0 sets. 2) Tab headers displayed misleading `Models (0)` when items were present. 3) UI displayed "Direct NWC" badges. 4) Tree only discovered `.nwc` files, omitting `.nwd` models, and lacked sorting by Name and Type. | 1) `SearchSetService` only traversed `RootItem.Children`, missing sets under `doc.SelectionSets.Value` or COM `state.SelectionSetsEx()`. 2) Tab headers bound to selected count instead of total count when unselected. 3) Hardcoded `"Direct NWC"` string in `DisplayType`. 4) `ModelDiscoveryService` strictly matched `.EndsWith(".nwc")` and lacked NWD handling and collection sorting. | 1) Implemented 3-tier sets retrieval (.NET RootItem -> .NET Value -> COM fallback) with Guid deduplication. 2) Discovered `.nwd` parent/standalone models alongside `.nwc` with distinct badge styling (blue for NWD, slate for NWC). 3) Replaced "Direct NWC" with clean `ModelType` badges. 4) Added `[Name ⇅]` / `[Type ⇅]` model sorting and `[Path ⇅]` set sorting with ascending/descending toggling. 5) Fixed tab header counts to show total items when unselected (`Models (47)`) and selection ratio when checked (`Models (3/47)`). |
| **ISS-049** | UI Branding & Polish | `MainWindow.xaml`, `App.cs`, `DynamicRibbonService.cs`, `DistillerTabViewModel.cs`, `ClashDistillerService.cs`, `RimoRibbon.xaml`, `CypherRibbon.xaml`, `AssemblyInfo.cs`, `MatrixTabViewModel.cs` | 1) Window title bar showed verbose "Automated Model Clash Runner & Distiller". 2) Header subtitle "Autodesk Navisworks Automated Coordination Suite" cluttered UI. 3) "Distill Clashes" tab name was confusing to users. 4) Default delimiter was "v" instead of user-preferred "x". | UI text was carried over from early development and did not match minimalist branding goals. "Distill" terminology confused users who preferred "Group". Default delimiter "v" conflicted with team naming convention that uses "x". | 1) Simplified window title and header to "Cypher Tools". 2) Removed subtitle entirely. 3) Renamed all user-facing "Distill/Distillation" text to "Group/Grouping" across tabs, buttons, ribbons, dialog messages, progress text, and log messages (8 files, ~25 lines). 4) Changed default delimiter from "v" to "x" and reordered array. Internal class/command names preserved. |
| **ISS-050** | UI & Filtering | `MainWindow.xaml`, `MatrixTabViewModel.cs`, `ViewModelAndSelectionTests.cs`, `build_all.ps1` | 1) Sets search bar watermark displayed verbose "by path". 2) Users risked accidentally selecting the wrong file type (mixing NWC and NWD). | 1) Watermark text hardcoded "Filter search sets by path...". 2) Models list lacked a quick file-type toggle filter to view/select only NWC or only NWD. | 1) Cleaned Sets watermark to "Filter search sets...". 2) Added Type Filter dropdown menu (`Type: All ▾`) with checkboxes for NWC Files and NWD Files in both Selection A and B Models toolbars. Unchecking a type immediately clears selections of that type and excludes them from clash test execution. |
| **ISS-051** | Deployment / Autoloader Manifest | `PackageContents.xml`, `Installer/Program.cs`, `Install_CypherTools.bat` | Add-in disappeared from Navisworks after running installer | 1) `PackageContents.xml` contained `AutodeskProduct="Navisworks"` which the Autodesk autoloader schema rejects (valid schema requires omitting it for Navisworks). 2) `Installer/Program.cs` purged the working `%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` whenever ProgramData deployment succeeded, but Navisworks was not loading from ProgramData due to manifest rejection. 3) Manifest lacked discrete per-year series definitions. | 1) Removed `AutodeskProduct="Navisworks"` and added discrete components for 2020-2026 (`Nw17`-`Nw23`) matching Autodesk SDK samples. 2) Fixed installer to always deploy to `%APPDATA%\Autodesk\ApplicationPlugins` and never purge it. 3) Rebuilt and verified bundle in `%APPDATA%`. |
| **ISS-052** | Clash Detective / Viewpoints | `ClashDistillerService.cs`, `NativeClashRedlineHelper.cs` | Exported clash viewpoints missing user-drawn redline markups (ellipses, clouds, text annotations) and review comments | `DocumentClashTests.TestsViewpointForResult` returns only the camera `Viewpoint`; `new SavedViewpoint(vp)` creates an empty viewpoint dropping `Redlines` (`LcOpRedlineList`) and `Comments` stored on native `LcOclTestIssue`. | Implemented `NativeClashRedlineHelper` dynamically binding `?GetRedlines@LcOclTestIssue`, `??4LcOpRedlineList` (`operator=`), and `?Merge@LcOpRedlineList` to copy all graphic markups and review comments from clash results/groups directly into `SavedViewpoint` before committing to the document. |
| **ISS-053** | Clash Grouping / Performance | `ClashDistillerService.cs`, `DistillerTabViewModel.cs`, `IClashDistillerService.cs` | Grouping clashes by element name/type created too many groups and was slow | `GroupByElement` searched ancestors for named elements and grouped by BOTH element and distance. | Completely removed element/type ancestor matching. Renamed method to `GroupByDistance`. Grouping now exclusively uses the $O(N)$ Spatial Voxel Hash Grid based solely on the distance slider, resulting in fewer, purely spatial groups and significantly faster processing. |
| **ISS-054** | Deployment / Ribbon UI | `build_all.ps1`, `Install_CypherTools.bat`, `PackageContents.xml` | Cypher ribbon tab vanished after build or install | Dual deployment to both `C:\ProgramData\Autodesk\ApplicationPlugins` and `%APPDATA%\Autodesk\ApplicationPlugins` (or presence of backup folders containing `PackageContents.xml` in ProgramData) caused duplicate assembly registration collision during Navisworks startup, causing MFC ribbon dispatcher to drop the tab. | Enforced strict Single-Target deployment to `%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle`. Updated `build_all.ps1`, `Install_CypherTools.bat`, and `Uninstall_CypherTools.bat` to proactively purge all ProgramData bundles and backup folders. Added unique `ProductCode` to manifest. |
| **ISS-055** | Clash Detective / Viewpoints | `ClashDistillerService.cs`, `NativeClashRedlineHelper.cs` | Redlines still missing on exported Saved Viewpoints | `savedViewpoints.AddCopy(targetFolder, svp)` performs a shallow clone copying only managed camera/comments and discarding unmanaged `LcOpRedlineList` attached to detached `svp`. Subsequent calls to `ReplaceViewRedlines` failed because `doc.State.Handle` is an `LcOwDocument*`, not `LcOpState*`. | Mutate the committed document-bound `SavedViewpoint` directly: immediately after `savedViewpoints.AddCopy()`, retrieve `boundSvp` from `targetFolder.Children` and invoke `NativeClashRedlineHelper.AttachRedlinesToSavedViewpoint(source, boundSvp, _logger)` which writes native redlines via `operator=` and `Merge` directly into the document-bound viewpoint. |
| **ISS-056** | UI Refinement & Rebranding | `MainWindow.xaml`, `CypherRibbon.xaml`, `App.cs`, `DynamicRibbonService.cs`, `MatrixTabViewModel.cs` | Redundant UI copy, confusing "Generate Matrix" terminology, cluttering Naming Formula label, and redundant explanatory text in Group Clashes tab | 1) Users found "Generate Matrix" technical and confusing compared to standard "Clash Test". 2) "Naming Formula: Selection A x Selection B" was visual clutter since users already select delimiter directly. 3) Group Clashes card had redundant "Proximity Clustering Range:" label, outdated "Clustering Strategy" bullet points (from pre-ISS-053), and verbose "Raw clashes within proximity distance..." copy. | 1) Renamed Tab 1 from "Generate Matrix" to "Clash Test". 2) Renamed primary action button to "Run Clash Test ({0} Tests)". 3) Updated Ribbon button and CommandHandler to "Clash Test". 4) Removed "Naming Formula" text and bindings from Tab 1 and confirmation dialog. 5) Removed "Proximity Clustering Range:" label, "Clustering Strategy" bullets, and "Raw clashes within proximity distance..." copy from Group Clashes tab. |
| **ISS-057** | Multi-Version CLR Binding & Deployment | `PackageContents.xml`, `AutomatedClashRunner.csproj`, `Installer/Program.cs`, `build_all.ps1`, `ClashDistillerService.cs` | Cypher tab missing in Navisworks 2026 despite working in Navisworks 2024 | 1) `PackageContents.xml` routed Navisworks 2026 (`Nw23`) to `./Contents/2024/CypherNavisTools.dll` compiled against API v21.0. Navisworks 2026 runs API v23.0; lack of cross-major binding redirect in `rroamer.exe.config` caused CLR `FileLoadException` on startup. 2) Navisworks 2026 removed `Viewpoint.PivotPoint` property. 3) `Installer/Program.cs` deployed dual bundle copies to both ProgramData and AppData, triggering ribbon dispatcher drop (ISS-054). | 1) Added dedicated `Release2025` (v22.0) and `Release2026` (v23.0) build targets to `.csproj` referencing strong-named assemblies. 2) Updated `PackageContents.xml` to route `Nw22` to `Contents/2025` and `Nw23` to `Contents/2026`. 3) Safely set `Viewpoint.PivotPoint` via reflection across all versions. 4) Purged ProgramData bundle deployment from `Installer/Program.cs` ensuring strict Single-Target AppData deployment. |
| **ISS-058** | Viewpoints Export / Native Memory Corruption & STA Deadlock | `ClashDistillerService.cs`, `NativeClashRedlineHelper.cs`, `ViewpointsTabViewModel.cs`, `DistillerTabViewModel.cs`, `MatrixTabViewModel.cs`, `DialogService.cs`, `MainWindow.xaml.cs` | Navisworks froze and crashed with "(Not Responding)" modal when exporting viewpoints | 1) `NativeClashRedlineHelper` attempted to copy redlines using unmanaged pointer offsets (`0x630`, `0x220`, and dynamic scans in `[0x180, 0x280]`) and invoked native `LcOpRedlineList::operator=` and `Merge` via `GetProcAddress`. It was also invoked on `boundSvp` directly inside the live document tree (`targetFolder.Children`), triggering heap corruption (`ntdll!RtlFreeHeap` / access violation) when Navisworks refreshed the Saved Viewpoints window. 2) In `ViewpointsTabViewModel`, `IsBusy` was kept `true` when calling `_dialogService.ShowInformation`, leaving the dark overlay frozen in the background. 3) `DialogService` called `MessageBox.Show` with no owner window, causing an STA message pump deadlock between unparented WPF modal dialog and Navisworks MFC main frame. | 1) Completely replaced native memory hacking in `ClashDistillerService.cs` with safe, pure-managed `CopyComments` (copying reviewer comments, authors, and timestamps to `svp.Comments`). 2) Removed document-bound mutation of `boundSvp` after insertion. 3) Safeguarded `NativeClashRedlineHelper` to disable all unmanaged memory mutations. 4) Refactored `ViewpointsTabViewModel`, `DistillerTabViewModel`, and `MatrixTabViewModel` to always clear `IsBusy = false` and pump `DoEvents()` before displaying completion dialogs. 5) Added `DialogService.ActiveWindow` and modal owner parenting to `MainWindow` across all message boxes. |


---

## Key Architectural Rules & Insights for Maintainers

1. **Navisworks Coordinates**:
   - `Point3D` coordinates returned by `ClashResult.Center`, `BoundingBox3D`, etc., are **always stored in meters internally**, regardless of display units set in `doc.Units`.
2. **Clash Grouping & Tree Mutations**:
   - Never move items within `ClashTest.Children` using ascending indices. Always sort descending by index first.
3. **Selection Linking**:
   - Selection A uses dynamic `SelectionSource` pointers from manual Search Sets.
   - Selection B directly selects the standard `.nwc` model node from `doc.Models` hierarchy.
4. **WPF & .NET Framework 4.8 Builds**:
   - To avoid modern .NET SDK XAML compilation conflicts, use legacy MSBuild format for WPF add-ins compiled on machines with preview SDKs.
   - Always ensure custom `TabItem` templates specify `<ContentPresenter ContentSource="Header" />`.
5. **Multi-Version Architecture & CLR Binding**:
   - Navisworks 2020-2023 uses `Autodesk.Navisworks.Api Version=20.0`.
   - Navisworks 2024-2026 uses `Autodesk.Navisworks.Api Version=21.0`.
   - Always compile dual engines (`Release2023` and `Release2024`) to avoid silent `ReflectionTypeLoadException` in host CLR.
6. **Smart App Control & Deployment**:
   - Deploy directly into `C:\Program Files\Autodesk\Navisworks Manage [Year]\Plugins\CypherNavisTools` via elevated `.bat` script or `CypherTools_Installer.exe` to avoid Windows 11 Smart App Control (`0x800711C7`) blocks.
7. **WPF Window Owner in Navisworks (`roamer.exe`)**:
   - Never use `Process.GetCurrentProcess().MainWindowHandle`. Navisworks has multiple native threads and background worker windows. WPF modal ownership must always use `Autodesk.Navisworks.Api.Application.Gui?.MainWindow?.Handle` (an `IWin32Window` wrapping the main MFC frame).
8. **Single Plugin Deployment Rule**:
   - Never install `CypherNavisTools` simultaneously to `Program Files\Autodesk\Navisworks...\Plugins` AND `ApplicationPlugins\*.bundle`. Dual registration corrupts the native Ribbon command dispatch table and causes host access violations.
9. **Clash Detective Transaction Boundaries**:
   - Never wrap `clashTests.TestsAddCopy()` and `clashTests.TestsRunTest()` in an outer `doc.BeginTransaction()`. Navisworks manages its own internal undo/redo transactions during clash runs.
10. **Pre-Flight Testing with Active Models**:
    - Never certify an add-in release based solely on an empty Navisworks session. Guard clauses like `if (doc == null || doc.IsClear)` short-circuit execution. All pre-flight verification must be performed with an active `.nwf` or `.nwd` model open.

---

## Deep Dive Post-Mortem: ISS-040 — Host Fatal Crash on Open NWF/NWD Model

### 1. Incident Overview & Diagnostic Behavior
- **Affected Versions**: Navisworks Manage 2023 (`roamer.exe` v20.0) and Navisworks Manage 2024 (`roamer.exe` v21.0).
- **Failure Signature**: Immediate process crash (`0xC0000005` Access Violation) without an unhandled .NET exception dialog.
- **The Critical Paradox**:
  - Clicking "Clash Matrix" when **no model was open** operated cleanly without crashing (displayed *"Please open a Navisworks document (.nwf or .nwd) before running the tool"*).
  - The moment an `.nwf` or `.nwd` document was loaded into the viewport, clicking *any* add-in button instantly terminated Navisworks.

---

### 2. Root Cause Analysis (The Lethal Trifecta)

#### Root Cause A: Invalid Native Window Handle Ownership (`Process.GetCurrentProcess().MainWindowHandle`)
In `App.cs`, modal window ownership had been refactored to:
```csharp
var helper = new WindowInteropHelper(window);
helper.Owner = Process.GetCurrentProcess().MainWindowHandle;
```
- **Why this is catastrophic in Navisworks**: `roamer.exe` is a complex unmanaged C++/MFC application hosting multiple threads and several off-screen helper/message windows. When a 3D document is loaded, DirectX/OpenGL graphics renderers and background spatial indexing services spin up background native threads. `Process.GetCurrentProcess().MainWindowHandle` queries the Win32 message pump and often resolves to an unmanaged background worker or hidden message-only window rather than the MFC main frame (`CMainFrame`).
- **The Mechanism of Death**:
  1. When WPF's `Window.ShowDialog()` executes, WPF calls Win32 `EnableWindow(helper.Owner, FALSE)` to disable the parent window and enter a modal message loop.
  2. Because the HWND belonged to an unmanaged background worker thread, `EnableWindow` deadlocked the Windows STA message dispatcher.
  3. The resulting cross-thread HWND state corruption caused an immediate hardware-level Memory Access Violation (`0xC0000005`) that bypassed all standard C# `catch (Exception ex)` blocks.
  4. Furthermore, if `ShowDialog()` faulted, the fallback handler attempted `window.Show()`. WPF strictly forbids calling `Show()` on a window that has already attempted to show, immediately throwing `InvalidOperationException` and terminating the host.

#### Root Cause B: Dual & Triple Simultaneous Plugin Loading Collisions
- During installer testing, `CypherNavisTools` had been installed simultaneously to:
  1. `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` (Machine Autoloader)
  2. `%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` (User Autoloader)
  3. `C:\Program Files\Autodesk\Navisworks Manage 2024\Plugins\CypherNavisTools` (Standalone legacy plugin)
- Navisworks loaded all three copies on startup. Autodesk's `NwPluginManager` registered the exact same plugin GUIDs and command IDs (`Cypher_Matrix`, `Cypher_Distill`, `Cypher_Viewpoints`) three times into the native MFC command dispatch table.
- When any ribbon button was clicked, MFC dispatched into conflicting native memory function pointers, causing heap corruption and access violations.
- Crucially, when `build_all.ps1` compiled fresh DLLs into `%APPDATA%`, Navisworks gave priority to the stale, crashing DLLs sitting in `C:\Program Files` and `C:\ProgramData`, masking newly compiled fixes until the duplicates were purged.

#### Root Cause C: Clash Detective Transaction Collisions (Navisworks 2023)
- In `ClashExecutionService.cs`, batch test creation and execution were enclosed in `using (var trans = doc.BeginTransaction(...))`.
- While Navisworks 2024 tolerates certain nested document transactions, Navisworks 2023's Clash Detective engine (`doc.GetClash().TestsData.TestsRunTest()`) starts its own internal undo/redo transaction.
- When an inner transaction was opened inside an active user transaction, Navisworks 2023 threw a fatal transaction abort exception that destabilized the host session.

---

### 3. The Comprehensive Fix

#### Fix 1: Authoritative MFC Main Window Handle with Safe Try-Catch
In [`App.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/App.cs):
- Replaced `Process.GetCurrentProcess().MainWindowHandle` with the official Autodesk API MFC main frame pointer:
  `Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle`
- Wrapped the handle assignment in a dedicated `try-catch` block so even if the GUI handle is temporarily unreachable, the window still opens smoothly without crashing the host.
- Removed the invalid `window.Show()` retry.
- Set `state.IsChecked = false` in `CanExecuteCommand` to prevent ribbon buttons from locking in a pressed state.

```csharp
var window = new MainWindow();
try
{
    if (Autodesk.Navisworks.Api.Application.Gui?.MainWindow?.Handle != IntPtr.Zero)
    {
        var helper = new WindowInteropHelper(window);
        helper.Owner = Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle;
    }
}
catch (Exception ownerEx)
{
    LoggerService.LogWarningStatic($"Could not attach window owner handle: {ownerEx.Message}");
}

window.DataContext = new MainViewModel(() => window.Close(), initialTabIndex: targetTab);
window.ShowDialog();
```

#### Fix 2: Single Authoritative Bundle Architecture & Automated Duplicate Purge
In [`Installer/Program.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Installer/Program.cs) and [`Install_CypherTools.bat`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Install_CypherTools.bat):
- Standardized all deployments exclusively to the global Autoloader bundle:
  `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle`
- Built an automatic deep-cleaning routine into both `PerformInstall` and `PerformUninstall` that proactively scans and removes:
  - `C:\Program Files\Autodesk\Navisworks Manage [Year]\Plugins\{CypherNavisTools, CypherTools, RimoTools, AutomatedClashRunner}`
  - `%APPDATA%\Autodesk\ApplicationPlugins\{bundles}`
  - `%APPDATA%\Autodesk\Navisworks Manage [Year]\Plugins\{plugins}`
- Guarantees Navisworks loads exactly **one** plugin instance on startup.

#### Fix 3: Removed Enclosing Document Transactions
In [`Services/ClashExecutionService.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Services/ClashExecutionService.cs):
- Removed `using (var trans = doc.BeginTransaction(...))` from `RunToolsTest` and `RunBaseBuildTest`.
- Navisworks Clash Detective manages transaction boundaries per test internally, ensuring 100% stable execution on both Navisworks 2023 and 2024.

---

### 4. Permanent Architectural Rules & Checklist for Maintainers

| Area | Strictly Forbidden (DON'T) | Required Pattern (DO) |
|---|---|---|
| **WPF Modal Owner** | `Process.GetCurrentProcess().MainWindowHandle` | `Autodesk.Navisworks.Api.Application.Gui?.MainWindow?.Handle` inside `try-catch` |
| **Ribbon Buttons** | Leaving `state.IsChecked = true` on push buttons | Set `state.IsChecked = false` in `CanExecuteCommand` |
| **Plugin Deployment** | Deploying both to `Program Files` and `ApplicationPlugins` | Single bundle in `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` |
| **Clash Transactions** | Wrapping `TestsRunTest()` in `doc.BeginTransaction()` | Let Clash Detective manage its own per-test undo transaction |
| **Tree Traversal** | Accessing `.Children` or `.OriginalModelItem` without null checks | Null-coalesce and wrap recursive aggregations in defensive try-catch |
| **Release Testing** | Testing only on an empty Navisworks viewport | Always verify by opening a heavy `.nwf` or `.nwd` model and launching each tool |
| **Clash Status Counts** | Unpacking `group.Children.Count` into status metrics | Count each `ClashResultGroup` as 1 item, matching Clash Detective's status bar |
| **Viewpoint Generation** | Stamping `doc.CurrentViewpoint.Value.CreateCopy()` without target focus | Pass `(IClashResult)group` to `TestsViewpointForResult` and use geometric target focus |
| **Naming Format** | Hardcoding `${test.DisplayName}-${index:D3}` without trimming hyphens | Use `_naming.FormatGroupName` / `_naming.FormatViewpointName` (`${baseName} ${index}`) |
| **Sets Traversal** | Relying only on `doc.SelectionSets.RootItem.Children` | Implement 3-tier traversal (.NET RootItem -> .NET Value -> COM `SelectionSetsEx`) with Guid deduplication |
| **Model Tree Discovery** | Matching only `.EndsWith(".nwc")` | Discover `.nwd` container/standalone models (`ModelType = "NWD"`) and nested `.nwc` files (`ModelType = "NWC"`) |
| **Tab Header Counts** | Binding tab counts to selection counts when 0 are checked | Display total items when unselected (`Models (47)`), and selection ratio when checked (`Models (3/47)`) |


---

### 5. Detailed Breakdown: ISS-046 (Clash Counts, Viewpoint Location & Naming Consistency)

#### Context & Symptom
A coworker reported three critical defects:
1. **Status counts misrepresentation**: For test `T-EGE-ASP1106-E-` with 5 groups in Clash Detective (3 New, 2 Reviewed, Total 5), the add-in UI showed **16 Active/New, 5 Reviewed, Total 21**.
2. **Viewpoint camera location**: Exported saved viewpoints did not point at the clashing items; they stamped the user's active viewport camera instead.
3. **Naming & trailing dashes**: Group and viewpoint names were formatted as `T-EGE-ASP1106-E--004` (double dashes, 3-digit zero-padding) instead of clean format `T-EGE-ASP1106-E 4`. Furthermore, exporting a filtered subset of clashes risked desynchronizing viewpoint numbers from clash numbers.

#### Root Causes & Implementation
1. **Counting groups**: In [`ClashTestNode.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Models/ClashTestNode.cs), `groupCount = group.Children.Count` added raw children to status totals. Fixed by counting each top-level `ClashResultGroup` as 1 item.
2. **Viewpoint retrieval & geometric focus**: In [`ClashDistillerService.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Services/ClashDistillerService.cs), `ExportViewpoints` passed `group.RepresentativeResult` (often null or missing a viewpoint). Changed to pass `(IClashResult)group` directly to `TestsViewpointForResult`. In addition, replaced the uncoordinated screen copy fallback with a smart geometric focus algorithm calculating `cameraEye = center - (dir * focalDist)` targeting `center` and `bbox`.
3. **Clean naming & number preservation**: In [`NamingService.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Services/NamingService.cs), implemented `SanitizeTestDisplayName`, `FormatGroupName`, and `FormatViewpointName`. Stripped trailing hyphens/underscores/spaces (`TrimEnd('-', '_', ' ')`), formatted names with a single space and unpadded digits (e.g. `EGE-ASP1106-E 4`), and extracted the exact trailing digit from source clash items (`\d+$`) so viewpoint numbers strictly preserve clash numbers even when exporting filtered subsets.

---

### 6. Detailed Breakdown: ISS-048 (3-Tier Sets Traversal, NWD Discovery, Sorting & UI Polish)

#### Context & Symptoms
In the generic clash matrix interface on branch `generic-clash-runner`:
1. **Empty Selection Sets List**: When toggling to the Sets tab in Selection A or B, the panel reported `"No manual search sets found in document"` and tab header showed `Sets (0)` despite user sets existing in the document.
2. **Misleading Tab Headers**: Tab headers displayed `Models (0)` when items were present but none were checked, giving the impression that model loading failed.
3. **Outdated Badge Wording**: Model items displayed a badge reading `"Direct NWC"`, which was confusing and visually cluttered.
4. **Missing NWD File Detection**: Appended `.nwd` container files and standalone `.nwd` models were ignored by `ModelDiscoveryService`, which strictly looked for `.EndsWith(".nwc")`.
5. **Lack of Sorting Controls**: Users had no way to sort models alphabetically by name or group them by file type (`NWC` vs `NWD`), nor sort sets by path.

#### Root Causes
1. **Selection Sets Storage Variations in Navisworks**: `SearchSetService.GetManualSearchSets` relied solely on `doc.SelectionSets.RootItem.Children`. In certain Navisworks files (particularly appended NWFs or models where sets were created in specific legacy versions or via batch scripts), sets are placed directly in `doc.SelectionSets.Value` or only indexed via the COM API `state.SelectionSetsEx()`.
2. **Count Binding Defect**: Tab header properties `TabHeaderModelsA`, `TabHeaderSetsA`, etc., evaluated `SelectedModelsCountA` instead of `ModelsA.Count`. When nothing was selected, it displayed `Models (0)`.
3. **Hardcoded Model DisplayType**: `ModelSourceNode.DisplayType` hardcoded `"Direct NWC"` vs `"NWD Branch"`.
4. **Hardcoded NWC Filter**: `ModelDiscoveryService` strictly checked `item.DisplayName.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase)`. When an `.nwd` model was loaded or appended, it was treated as a generic node and skipped, dropping all top-level NWD models and their nested content.

#### Architectural Solutions & Implementation
1. **Resilient 3-Tier Sets Traversal with COM Fallback**:
   - In [`Services/SearchSetService.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Services/SearchSetService.cs), implemented a fallback hierarchy:
     - **Tier 1 (.NET Primary)**: Traversal of `doc.SelectionSets.RootItem.Children`.
     - **Tier 2 (.NET Fallback)**: Traversal of `doc.SelectionSets.Value` for legacy or non-hierarchical sets.
     - **Tier 3 (COM Fallback)**: COM `ComApiBridge.State.SelectionSetsEx()` traversal, resolving COM indices back to .NET `SavedItem` via `doc.SelectionSets.ResolveIndexPath(indices)`.
   - Added `HashSet<Guid>` deduplication to guarantee no duplicated set entries appear across tiers.
2. **NWD Model Discovery & Classification**:
   - In [`Services/ModelDiscoveryService.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Services/ModelDiscoveryService.cs), extended discovery to inspect `model.FileName`, `model.RootItem.DisplayName`, and `doc.CurrentFileName` using case-insensitive substring matching (`IndexOf(".nwd")`) to accommodate display name aliases and read-only flags.
   - Top-level `.nwd` files are added as selectable model nodes with `ModelType = "NWD"`, and any nested `.nwc` child files are discovered under them with `ModelType = "NWC"`.
   - In `FindModelNodes` and `GetSiblingNwcs`, container and model checks use resilient `IndexOf(".nwd")` / `IndexOf(".nwc")` matching.
3. **Clean Badge Styling & Elimination of "Direct NWC"**:
   - In [`Models/ModelSourceNode.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Models/ModelSourceNode.cs), added explicit `ModelType` (`"NWC"`, `"NWD"`) and `IsNwd` properties.
   - Replaced `"Direct NWC"` with clean, modern badges: slate badge for `NWC`, blue badge for `NWD` with custom WPF border and foreground palettes in [`Views/MainWindow.xaml`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Views/MainWindow.xaml).
4. **Bidirectional Sorting**:
   - In [`ViewModels/MatrixTabViewModel.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/ViewModels/MatrixTabViewModel.cs), added `ToggleSortNameACommand`, `ToggleSortTypeACommand`, `ToggleSortSetsACommand` (and B equivalents).
   - Toggles ascending/descending states with dynamic button labels (`[Name ▲]`, `[Name ▼]`, `[Type ▲]`, etc.).
   - Model lists sort by `DisplayName` (Name) or `ModelType` then `DisplayName` (Type). Sets sort by `Path`.
5. **Accurate Tab Header Counts & Auto-Loading**:
   - When 0 items are checked, headers display `Models (47)` or `Sets (12)`. When items are checked, headers display `Models (3/47)` or `Sets (2/12)`.
   - Added auto-loading of selection sets when switching to the Sets tab if sets have not been populated yet.
6. **Robust Installer Deployment (Admin ProgramData Overwrite)**:
   - In [`Installer/Program.cs`](file:///c:/Users/Rimo/Downloads/ACC/UCSC/Project%20Files/02%20-%20Models/02%20-%20Navisworks/AutomatedClashRunner/Installer/Program.cs), enhanced `IsNavisworksRunning()` to check both `roamer` and `navisworks` case-insensitively across all processes.
   - Updated `CopyDirectory` to strip `ReadOnly` attributes on destination files before overwriting.
   - Guaranteed that ProgramData deployment proceeds directly to `CopyDirectory` overwrite even if `Directory.Delete` encounters locked or protected files.

---

### ISS-052: Preserving & Exporting Redline Markups (Ellipses, Clouds, Text Annotations) and Comments in Saved Viewpoints

#### Symptoms
When a BIM coordinator reviews clashes in Navisworks Manage Clash Detective:
1. They select a clash result or clash group, mark its status as `"Reviewed"`, and draw graphic redlines (e.g. revision ellipses, clouds, text annotations, arrows).
2. When using the add-in's **"Create Viewpoints"** tab to export viewpoints into the Saved Viewpoints folder for "Reviewed" clashes, the viewpoints are created with the correct camera perspective and clash location, but **all redline markups (ellipses, text) and comments are completely missing**.

#### Root Cause Analysis
1. **Camera vs. Saved Viewpoint Separation in Navisworks API**:
   - In the public .NET API, `DocumentClashTests.TestsViewpointForResult()` returns an `Autodesk.Navisworks.Api.Viewpoint` object.
   - A `Viewpoint` represents **only 3D camera geometry** (`Position`, `Direction`, `UpVector`, `FocalDistance`, `Projection`). It does **not** contain redline markups or comments.
2. **Empty SavedViewpoint Instantiation**:
   - `ClashDistillerService.ExportViewpoints()` previously called `new SavedViewpoint(vp) { DisplayName = vpName }`.
   - This constructor creates an empty `SavedViewpoint` whose `Redlines` collection (`LcOpRedlineList`) is empty and whose `Comments` collection is empty.
3. **Where Navisworks Stores Clash Redlines**:
   - Reverse engineering of `lcclash.dll` and `lcodclash.dll` revealed that `ClashResult` (`LcOclTestResult`) and `ClashResultGroup` (`LcOclTestResultGroup`) both inherit from the native C++ class `LcOclTestIssue`.
   - `LcOclTestIssue` stores its `LcOpRedlineList` at memory offset `+0x198` of its native pointer, exposed by the exported symbol:
     ```cpp
     const LcOpRedlineList& LcOclTestIssue::GetRedlines() const;
     // Decorated: ?GetRedlines@LcOclTestIssue@@QEBAAEBVLcOpRedlineList@@XZ
     ```
   - In `lcodyplugin.dll`, Navisworks natively copies redlines into new viewpoints using the C++ assignment operator:
     ```cpp
     LcOpRedlineList& LcOpRedlineList::operator=(const LcOpRedlineList&);
     // Decorated: ??4LcOpRedlineList@@QEAAAEAV0@AEBV0@@Z
     void LcOpRedlineList::Merge(LcOpRedlineList*);
     // Decorated: ?Merge@LcOpRedlineList@@QEAAXPEAV1@@Z
     ```
   - Because the add-in never extracted the native `LcOpRedlineList` or invoked the assignment/merge functions, markups were never copied into the newly created `SavedViewpoint`.

#### Architectural Solutions & Implementation
1. **`NativeClashRedlineHelper` (`Services/NativeClashRedlineHelper.cs`)**:
   - Created a thread-safe helper that dynamically binds to `lcodclash.dll` and `lcodyplugin.dll` (already in-process in Navisworks).
   - Resolves native delegates for `GetRedlines`, `AssignRedlines` (`operator=`), and `MergeRedlines`.
   - Employs an offset `+0x198` fallback if `GetProcAddress` returns null.
   - Extracts native `IntPtr` from `NativeHandle.Handle` on clash items and `SavedViewpoint.EditRedlines().Handle` on destination viewpoints.
2. **Support for Group & Child Clash Markups**:
   - When exporting a `ClashResultGroup`:
     - Checks if the group itself has redlines (`grp.HasRedlines`).
     - Also iterates `grp.Children.OfType<ClashResult>()`: if a coordinator reviewed a specific child clash in the group (`child.HasRedlines`), child redlines and comments are automatically extracted and merged into the exported group viewpoint.
   - When exporting a raw `ClashResult`:
     - If `raw.HasRedlines`, copies all redlines directly.
3. **Review Comment Preservation**:
   - Copies review comments (`Author`, `Body`, `Status`) from `result.Comments` and child comments into `svp.Comments`, preventing duplicate entries.
4. **Enhanced Camera Perspective for Groups**:
   - In `GetTestsViewpointForResult`: If a `ClashResultGroup` does not have a saved camera viewpoint (`!clashGroup.HasSavedViewpoint`), the method checks if any child clash has `HasSavedViewpoint || HasRedlines` and uses that camera perspective, ensuring the camera aligns perfectly with where the coordinator drew their annotations.
5. **Fail-Safe Exception Handling**:
   - All pointer operations are wrapped in rigorous exception blocks. If native redline extraction fails for any reason, the add-in logs a warning and creates the clean viewpoint camera without crashing the host.

---

### ISS-057: Navisworks 2026 Add-in Loading Failure (Missing Ribbon Tab) & Cross-Version CLR Binding

#### Context & Symptom
After installing Cypher Tools:
- On **Navisworks Manage 2024**, the add-in loaded and functioned properly.
- On **Navisworks Manage 2026**, the "Cypher" ribbon tab was completely absent. No error dialog was displayed to the user on startup.

#### Root Cause Analysis
1. **Strong-Named Assembly Versioning & Lack of Host Binding Redirects**:
   - Autodesk Navisworks assemblies (Autodesk.Navisworks.Api, Autodesk.Navisworks.Clash, Autodesk.Navisworks.ComApi, Autodesk.Navisworks.Interop.ComApi) are strong-named with PublicKeyToken=d85e58fa5af9b484.
   - Each major release increments assembly versions:
     - 2023 (Nw20): Version=20.0.0.0
     - 2024 (Nw21): Version=21.0.0.0
     - 2025 (Nw22): Version=22.0.0.0
     - 2026 (Nw23): Version=23.0.0.0
   - `roamer.exe.config` in Navisworks Manage 2026 redirects internal minor builds (23.0.0.0 - 23.0.9999.9999) to 23.0.0.0, but **does not** redirect older major versions (21.0.0.0 -> 23.0.0.0).
   - PackageContents.xml routed Navisworks 2026 (Nw23) to ./Contents/2024/CypherNavisTools.dll.
   - Because Contents/2024/CypherNavisTools.dll was compiled against Version=21.0.0.0, the .NET CLR threw System.IO.FileLoadException: Could not load file or assembly 'Autodesk.Navisworks.Api, Version=21.0.0.0', and Navisworks silently dropped the add-in.
2. **Breaking API Change in Navisworks 2026 (Viewpoint.PivotPoint)**:
   - In Navisworks 2025 and 2026, Autodesk removed the PivotPoint property from Autodesk.Navisworks.Api.Viewpoint.
   - Attempting to compile against the 2026 API failed with error CS1061: 'Viewpoint' does not contain a definition for 'PivotPoint'.
3. **Installer ProgramData Collision Risk (ISS-054 Relapse)**:
   - Installer/Program.cs had reintroduced code deploying the bundle to C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle in addition to %APPDATA%.
   - Dual-location deployment causes duplicate GUID registration collisions in Navisworks, causing the MFC ribbon dispatcher to drop the ribbon tab.

#### Architectural Solutions & Implementation
1. **Four-Target Multi-Version Architecture**:
   - Acquired official strong-named reference assemblies for 2025 (lib/2025, 22.0.0.0) and 2026 (lib/2026, 23.0.0.0).
   - Configured dedicated build configurations in AutomatedClashRunner.csproj:
     - Release2023: Output bin\Release\2023\, references lib/2023
     - Release2024: Output bin\Release\2024\, references lib/2024
     - Release2025: Output bin\Release\2025\, references lib/2025
     - Release2026: Output bin\Release\2026\, references lib/2026
2. **Autoloader Manifest Alignment**:
   - Updated PackageContents.xml:
     - Series Nw22 (2025) -> ./Contents/2025/CypherNavisTools.dll
     - Series Nw23 (2026+) -> ./Contents/2026/CypherNavisTools.dll
3. **Safe Cross-Version Reflection for PivotPoint**:
   - In ClashDistillerService.cs, wrapped Viewpoint.PivotPoint in safe reflection to work across 2020-2026.
4. **Purged ProgramData Deployment from Standalone Installer**:
   - Removed duplicate ProgramData copying in Installer/Program.cs.
   - Added proactive purge of ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle.
   - Enforced strict Single-Target deployment to %APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle.
5. **Updated Build Scripts & Packaging**:
   - Updated build_all.ps1 and Install_CypherTools.bat to compile, stage, package, and deploy all 4 engines (2023, 2024, 2025, 2026).
   - Recompiled modern standalone CypherGenericClash_Installer.exe containing the complete 4-version payload.

---

### ISS-058: Access Violation Crash During "Export Viewpoints" (Unsafe Native Pointer Operation & STA UI Deadlock)

#### Context & Symptom
When exporting clash viewpoints in Navisworks Manage 2024, the application abruptly terminated or threw an unhandled `AccessViolationException` (`0xc0000005`) during redline / comment processing. Additionally, modal progress dialogs occasionally hung the UI thread.

#### Root Cause Analysis
1. **Unsafe Native Memory Invocations in `NativeClashRedlineHelper`**:
   - `NativeClashRedlineHelper` attempted to invoke unexported C++ member operators (`operator=` and `Merge`) at hardcoded struct offsets (`0x630` and `0x220`) in `lcodclash.dll` and `lcodyplugin.dll`.
   - Internal Navisworks object layouts differ between patch levels and runtime configurations. Dereferencing these memory offsets directly caused memory access violations (`AccessViolationException`), which are non-catchable SEH exceptions in modern .NET Framework / .NET Core runtimes unless explicitly permitted, crashing the host `roamer.exe`.
2. **STA UI Thread Reentrancy Deadlock**:
   - Long-running export operations on the STA thread bound `IsBusy = true` without pumping the Windows message queue prior to launching modal `MessageBox` confirmation dialogs, occasionally leaving modal dialog owners in an orphaned state.

#### Architectural Solutions & Implementation
1. **Managed Comment Preservation (`ClashDistillerService.cs`)**:
   - Replaced all unsafe native pointer manipulation and memory offsets with 100% safe managed comment synchronization via `Autodesk.Navisworks.Api.Comment` APIs.
   - Preserves all review comments (`Author`, `Body`, `Status`) from both raw clashes and group children without any native C++ pointer dereferences.
2. **UI Thread Pump & Safe Window Ownership**:
   - Added `IsBusy = false; DoEvents();` prior to modal prompt invocations in `ViewpointsTabViewModel`, `DistillerTabViewModel`, and `MatrixTabViewModel`.
   - Attached `DialogService.ActiveWindow = MainWindow` to guarantee correct owner window chaining for modal dialogs.
3. **Unit Test Verification**:
   - All 81 unit tests passing under `dotnet test`.

---

### ISS-059: Installer Multi-Engine Package & Elevated ProgramData Deployment Architecture

#### Context & Symptom
- Users reported that Navisworks 2026 was not displaying the Cypher tab, and viewpoint export continued to fail even after the DLL fix was compiled.
- Investigation revealed that an old stale bundle from 3:07 PM (`C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle`) remained on disk, owned by `BUILTIN\Administrators`. Non-elevated scripts could not delete or overwrite it (`Access is denied`), so Navisworks prioritized loading the stale bundle over `%APPDATA%`.

#### Root Cause Analysis
1. **The Stale `ProgramData` Bundle Trap**:
   - Navisworks loads bundles from `C:\ProgramData\Autodesk\ApplicationPlugins` with machine-wide priority before or alongside `%APPDATA%`.
   - The stale bundle in `ProgramData` contained an older `PackageContents.xml` that mapped Navisworks 2026 (`Nw23`) to `./Contents/2024/CypherNavisTools.dll` (.NET Framework 4.8), which .NET 8 (CoreCLR) rejected, dropping the ribbon tab silently.
   - Furthermore, Navisworks 2024 loaded the 3:07 PM DLL from `ProgramData`, bypassing the newly fixed DLL in `%APPDATA%`.
2. **Lack of Administrator Privilege Handling in Non-Elevated Scripts**:
   - Standard user scripts and command prompts lack permissions to delete or overwrite administrator-owned directories in `C:\ProgramData`.

#### Architectural Solutions & Implementation
1. **UAC Elevated Standalone Installer (`CypherGenericClash_Installer.exe`)**:
   - Embedded `app.manifest` with `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` ensuring full administrative rights upon launch.
   - Implemented `SafeDeleteDirectory` with recursive `FileAttributes.Normal` clearing to cleanly purge locked or read-only files.
2. **Authoritative Single-Target Machine-Wide Deployment**:
   - Primary Target: Deploys all 4 engines (2023, 2024, 2025, 2026) directly to `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` (machine-wide for all users).
   - Single-Target Rule: Automatically purges `%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle` and legacy bundles to strictly eliminate duplicate bundle collisions.
   - Non-Elevated Fallback: If `ProgramData` is not writable, gracefully deploys to `%APPDATA%`.
3. **Modern Standalone Distribution**:
   - Compiled modern standalone `CypherGenericClash_Installer.exe` (308 KB) with updated UI header: `⚡ CYPHER GENERIC CLASH SETUP`.
   - Distributed to:
     1. `CypherGenericClash\CypherGenericClash_Installer.exe`
     2. Workspace root: `CypherGenericClash_Installer.exe`
     3. Downloads folder: `C:\Users\Rimo\Downloads\CypherGenericClash_Installer.exe`

---

### ISS-060: Architectural Separation of Add-in Suites (Generic vs Fab Coexistence)

#### Context & Requirement
The user requested separating the add-in into two distinct physical directories and standalone installers:
1. **Fab Edition (`CypherFabTools`)**: Specialized for Semiconductor and Tool Install coordination, containing Tools Test (`T-`), Base Build (`B-`), and Constructability POC clearance tests (`C-`), tailored for coworkers.
2. **Generic Edition (`CypherGenericClash`)**: Universal Clash Test matrix, simplified Grouping, and Viewpoint Export, tailored for executives and general BIM coordination.

#### Architectural Separation & Side-by-Side Coexistence
1. **Independent Directories & Git Branches**:
   - `CypherGenericClash/` (branch: `generic-clash-runner`)
   - `CypherFabTools/` (branch: `master`)
2. **Isolated Assemblies & Identifiers**:
   - Generic: `CypherGenericClash.dll`, Plugin ID `CypherGenericRibbon` / `CypherGenericAddin`, Ribbon Tab `Cypher Clash` (`Id="CypherGeneric_Tab"`, KeyTip: `CG`).
   - Fab: `CypherFabTools.dll`, Plugin ID `CypherFabRibbon` / `CypherFabAddin`, Ribbon Tab `Cypher Fab` (`Id="CypherFab_Tab"`, KeyTip: `CF`).
3. **Distinct Autoloader Bundles**:
   - Generic: `CypherGenericClash.bundle` (ProductCode: `{E89B1002-3CD2-45F4-A0C3-8991D4C3E481}`)
   - Fab: `CypherFabTools.bundle` (ProductCode: `{D74A1B23-7F89-49DE-9A4B-2B76C1234567}`)
4. **Standalone Installers**:
   - Generic: `CypherGenericClash_Installer.exe` (workspace root & Downloads)
   - Fab: `CypherFabTools_Installer.exe` (workspace root & Downloads)
   - Zero-Collision Purge Logic: Each installer exclusively purges legacy bundles (`CypherNavisTools`, `RimoTools`, etc.) and updates its own bundle; neither installer touches or removes the sibling add-in. Both run side by side seamlessly in Navisworks Manage (2020-2026).


