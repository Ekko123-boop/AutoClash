# Automated Clash Runner - Project Documentation

## Overview
Automated Clash Runner is an Autodesk Navisworks Manage 2024 add-in built in C# (.NET Framework 4.8). Its primary purpose is to automate the creation and execution of clash tests by taking manually predefined Selection Sets (e.g., "Base Build") and automatically clashing them against dynamically discovered .nwc federated model nodes (e.g., HVAC, Plumbing, Structural).

## Architecture & Components

### 1. App.cs (Entry Point)
- Inherits from AddInPlugin.
- Registered via [PluginAttribute] and [AddInPlugin(AddInLocation.AddIn)].
- Launches the WPF MainWindow UI.

### 2. MainWindow.xaml & MainViewModel.cs
- **MVVM Pattern**: The UI binds to MainViewModel.
- **Search Sets Tree**: Displays manual selection sets. Uses an IsSet (inverse of IsGroup) property to only render checkboxes on actual sets (hiding them on folders).
- **Models Tree**: Displays discovered federated model .nwc leaf nodes.
- **Validation**: Ensures at least one Set and one Model are selected before execution.

### 3. ModelDiscoveryService.cs
- Recursively scans Application.ActiveDocument.Models.
- **Core Logic**: Looks for nodes where DisplayName.EndsWith(".nwc") up to 3 levels deep.
- **Why**: Bypasses a Navisworks bug where Revit-exported .nwc files incorrectly report .rvt in their Source File Name properties.

### 4. SearchSetService.cs
- **Manual Sets**: Traverses doc.SelectionSets.RootItem to build a UI tree of folders and sets.
- **Generated Sets**: Creates a "Tests" folder (if it doesn't exist) and generates a Static Selection Set for every selected .nwc model.
- **Static Sets vs Search Sets**: Uses 
ew SelectionSet(new ModelItemCollection { originalNode }) instead of string-based property searches (Item > Name) to guarantee perfect accuracy, as string property indexing for NWCs in Navisworks is highly inconsistent.

### 5. ClashExecutionService.cs
- Iterates through a matrix of [Selected Manual Sets] x [Selected Models].
- **Naming Convention**: Trims the 'F1-' prefix and extensions via NamingService. Names the Clash Test strictly after the trimmed .nwc filename (e.g., STS-HDLS201-DR).
- **Selection A**: Binds the manual set to the Clash Test's Selection A. 
  - *Crucial Workaround*: Directly modifies 	est.SelectionA.Selection.SelectionSources.Add(sourceA) to force it to appear under the UI "Sets" tab. (Calling 
ew SelectionSourceCollection() directly causes an AccessViolationException in Navisworks 2024).
- **Selection B**: Binds the static generated model set to the Clash Test's Selection B by passing its ExplicitModelItems. Appears under the "Standard" geometry tab perfectly mapped.
- **Execution**: Uses doc.GetClash().TestsData.TestsRunTest() to execute the matrix.

## Deployment & Build
- **Target**: x64, 
et48-windows.
- **References**: Autodesk.Navisworks.Api, Autodesk.Navisworks.Clash. CopyLocal is strictly False.
- **Bundle Path**: %APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle
- **Note**: Navisworks must be completely closed when overwriting the DLL, as it locks loaded assemblies.

## Maintaining and Updating
If Navisworks geometry is updated (e.g., Subcontractor uploads a new .nwc and the user hits "Refresh" in Navisworks), the generated Static Sets will automatically track the geometry changes because they are bound by node memory reference, not by static coordinates or fragile string searches. Re-running the Clash Matrix will safely skip existing test names but executing "Update All" in Navisworks will accurately clash the newest geometry.

## Multi-Version Installer
The addin includes a self-contained, single-file installer executable (Installer.exe).
- **Support**: It natively supports Autodesk Navisworks Manage versions **2022 to 2025** by declaring <RuntimeRequirements SeriesMin="Nw19" SeriesMax="Nw22" /> inside the bundled PackageContents.xml.
- **How it works**: The .dll and PackageContents.xml are packaged into a .zip file embedded directly into the installer executable as a C# EmbeddedResource. When run, it extracts the bundle straight into the user's %APPDATA%\Autodesk\ApplicationPlugins\ folder. No third-party software (like InnoSetup) is required.
