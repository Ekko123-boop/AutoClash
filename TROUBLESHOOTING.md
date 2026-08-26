# Known Issues & Troubleshooting History

This log tracks bugs encountered during the development and usage of the Automated Model Clash Runner, along with their root causes and fixes. Future developers can reference this to resolve regressions quickly.

### 1. WPF Crash: "Data at the root level is invalid" (StaticResourceHolder exception)
**Symptom**: Attempting to launch the plugin immediately threw a fatal XAML parsing exception related to a `StaticResource`.
**Cause**: A `BooleanToVisibilityConverter` was referenced in a CheckBox binding `Visibility="{Binding IsFolder, Converter={StaticResource BooleanToVisibilityConverter}}"` but the converter was never explicitly defined in the XAML `<Window.Resources>`.
**Fix**: Add the following to the top of `MainWindow.xaml`:
```xml
<Window.Resources>
    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
</Window.Resources>
```

### 2. Deployment Failure: "The process cannot access the file"
**Symptom**: Running `Copy-Item` to deploy the built `.dll` into the `%APPDATA%` bundle folder failed with an `IOException`.
**Cause**: The DLL is loaded into the Navisworks AppDomain when the application starts. Windows locks the file, preventing overwrites.
**Fix**: Navisworks MUST be fully closed before attempting to copy or deploy a new compiled DLL.

### 3. Missing Properties on ClashTest (CS0117)
**Symptom**: Compile errors stating `'ClashTest' does not contain a definition for 'MergeType'` and `'CompositeObjectClashing'`.
**Cause**: The Navisworks .NET API (`Autodesk.Navisworks.Api.Clash`) handles clash test construction slightly differently than the legacy COM API. Some properties are not directly exposed on the constructor or have been deprecated/moved.
**Fix**: Removed explicit assignments for `MergeType` and `CompositeObjectClashing`. Rely on Navisworks defaults or manage them via standard UI templates if strictly required.

### 4. UI Bug: Manual Search Sets Missing from List
**Symptom**: The UI only displayed one Search Set ("AS BUILT") but completely hid actual Folders (like "Base Build").
**Cause**: The WPF binding `Visibility="{Binding IsFolder, Converter={StaticResource BooleanToVisibilityConverter}}"` evaluated to `Collapsed` for Sets (which was correct for hiding the *CheckBox*), but because it was applied incorrectly, the inverse logic was needed. 
**Fix**: Created a dedicated `public bool IsSet => !IsFolder;` property in the ViewModel and bound visibility to that instead, ensuring checkboxes only appear on actual sets while keeping the folder names visible in the tree.

### 5. Model Discovery Bug: Nested `.nwc` Files Missing
**Symptom**: The UI showed top-level federated `.nwd` branches (e.g., `F1-MEI - A&B.nwd`) but failed to list the actual `.nwc` leaf files inside them.
**Cause**: The original discovery logic only iterated `doc.Models` and checked exactly one level deep (`model.RootItem.Children`). It failed to recurse into federated models.
**Fix**: Implemented a recursive `FindModelNodes` function in `ModelDiscoveryService.cs`. It now recursively drills into `item.Children` (up to a safe depth of 3) looking for items containing the `Source File Name` property or a `ClassDisplayName` of "File", correctly extracting `.nwc`, `.rvt`, and `.dwg` leaf nodes.

### 6. Missing Checkboxes for Static Selection Sets & UI Validation
**Symptom**: Some manual Search Sets (like 'AS BUILT') did not have a CheckBox in the UI. Also, if the user clicked 'Run' without selecting any manual sets, it generated the model Search Sets but silently skipped the clash matrix.
**Cause**: The UI evaluated IsFolder = (item is FolderItem). However, Navisworks groups folders and groups under the IsGroup property. Second, the code didn't validate if manualSets was empty before running, leading to an empty clash summary.
**Fix**: Changed IsFolder logic to use item.IsGroup instead of checking the FolderItem class directly. Added popup validation warnings in MainViewModel.cs if the user clicks Run with 0 sets selected. Fixed WPF string formatting on the Run button.

### 7. Clash Test Naming and Selection A "Sets" Tab Workaround
**Symptom**: The generated Search Set names included the 'F1-' prefix which the user didn't want, and Selection A in Clash Detective was selecting geometry nodes (under the "Standard" tab) instead of binding directly to the UI "Sets".
**Fix**: 
- **Naming**: Updated `NamingService.GetTrimmedModelCode()` to parse the string and strictly return the string segment after the first dash.
- **Selection A (Sets Tab)**: We originally attempted to bind Selection A directly to the "Sets" tab using `doc.SelectionSets.CreateSelectionSource()` and `new SelectionSourceCollection()`. However, Navisworks 2024 has a severe bug where instantiating `SelectionSourceCollection` in the .NET API causes a low-level `System.AccessViolationException` (memory corruption) at JIT compile time, causing the add-in to completely vanish from the ribbon on startup. The final workaround bypasses the constructor by directly modifying the pre-existing collection in memory: `test.SelectionA.Selection.SelectionSources.Add(sourceA)`. This successfully forces the Selection A to map to the Sets tab without crashing.

### 8. Selection B Incorrect Geometry (Empty Search Set Bug)
**Symptom**: After fixing Selection A, Selection B began highlighting the completely wrong `.nwc` node (defaulting to the first item in the model tree).
**Cause**: The generated Search Set for the `.nwc` models was relying on `SearchCondition.HasPropertyByDisplayName("Item", "Name")`. However, Navisworks' internal property indexing for `.nwc` files is inconsistent (sometimes using `Source File Name`, sometimes reporting `.rvt`, or omitting the extension). This caused the Search Set to return 0 items. When handed an empty scope, Navisworks silently defaults to selecting the top-most node in the tree.
**Fix**: Completely abandoned dynamically searching Navisworks by string properties. The generated sets were rewritten to be **Static Sets** initialized with the exact `ModelItem` memory reference that the `ModelDiscoveryService` already found: `new SelectionSet(new ModelItemCollection(new[] { modelNode.OriginalModelItem }))`. This completely bypasses property search failures and guarantees mathematically perfect model selection.
