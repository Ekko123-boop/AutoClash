# Changelog

All notable changes to the Automated Clash Runner & Distiller addin are documented here.

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
