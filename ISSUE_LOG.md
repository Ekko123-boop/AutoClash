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

---

## Key Architectural Rules & Insights for Maintainers

1. **Navisworks Coordinates**:
   - `Point3D` coordinates returned by `ClashResult.Center`, `BoundingBox3D`, etc., are **always stored in meters internally**, regardless of display units set in `doc.Units`.
2. **Clash Grouping & Tree Mutations**:
   - Never move items within `ClashTest.Children` using ascending indices. Always sort descending by index first.
3. **Selection Linking**:
   - Always prefer `SelectionSource` pointers over explicit `ModelItemCollection` snapshots so tests remain dynamic across geometry reloads.
4. **WPF & .NET Framework 4.8 Builds**:
   - To avoid modern .NET SDK XAML compilation conflicts, use legacy MSBuild format for WPF add-ins compiled on machines with preview SDKs.
