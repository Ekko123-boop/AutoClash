# Changelog

All notable changes to the Automated Clash Runner & Distiller addin are documented here.

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
