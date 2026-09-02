# Cypher Tools — Troubleshooting & Technical Diagnostics

## 1. Where are the log files located?
Diagnostic logs are automatically written to:
`%LOCALAPPDATA%\AutomatedClashRunner\Logs\session_YYYY-MM-DD.log`

If you encounter unexpected behavior, inspect this file to view detailed stack traces and warnings.

---

## 2. Navisworks 2024 Plugin Disappearance (CLR Major Version Mismatch)
### Symptom
Plugin appears in Navisworks 2023 but is completely invisible in Navisworks Manage 2024 (neither the Ribbon tab nor the Add-ins tab appears).
### Root Cause (Forensic Analysis)
When an assembly is compiled referencing `Autodesk.Navisworks.Api` v20.0 (Navisworks 2023), its assembly manifest binds to `Version=20.0.1382.63`. Navisworks Manage 2024 runs with `Version=21.0.0.0`. Because Navisworks does not include backward binding redirects in `Roamer.exe.config`, the .NET CLR throws:
```text
ReflectionTypeLoadException: LoaderException: Could not load file or assembly 'Autodesk.Navisworks.Api, Version=20.0.1382.63...'
```
Navisworks catches this internally during plugin discovery and silently drops the plugin.
### Solution
We compile dual runtime engines:
- **`Release2023`** targeting Navisworks 2020–2023 (`Version 20.0`)
- **`Release2024`** targeting Navisworks 2024–2026 (`Version 21.0`)
`Install_CypherTools.bat` (or `CypherTools_Installer.exe`) automatically deploys the matching version to each Navisworks installation.

---

## 3. Windows Smart App Control (SAC) Blocking Installer or AppData DLLs (0x800711C7)
### Symptom
- Windows Smart App Control blocks the `.exe` installer.
- Navisworks ignores `.dll` files placed in `%APPDATA%\Autodesk\ApplicationPlugins\`.
- PowerShell reflection test returns:
```text
Exception from HRESULT: 0x800711C7 (An Application Control policy has blocked this file)
```
### Root Cause
Windows 11 Smart App Control (SAC) enforces strict code integrity on newly compiled binaries running in user space (`%APPDATA%`, `%TEMP%`, `Downloads`) unless they are digitally signed with an EV certificate.
### Solution
`C:\Program Files\` is in Windows Defender / Smart App Control's trusted path whitelist. Running **`Install_CypherTools.bat`** with Administrator privileges places the binaries in `C:\Program Files\Autodesk\Navisworks Manage 2024\Plugins\CypherNavisTools\`, bypassing Smart App Control restrictions completely without requiring code signing.

---

## 4. Navisworks SelectionSourceCollection Crash
### Symptom
`System.AccessViolationException` when creating a clash test.
### Cause
In Navisworks Manage 2024+, instantiating `new SelectionSourceCollection()` triggers an uncatchable JIT access violation.
### Solution
We bypass the constructor and directly add `SelectionSource` items to the pre-instantiated collection on `ClashTest.SelectionA.Selection.SelectionSources.Add(source)`.

---

## 5. Coordinate System & Distiller Proximity
### Symptom
Clash grouping distances feel inaccurate or different depending on whether the project is in feet or millimeters.
### Cause
Navisworks internal coordinates (`Point3D.X, Y, Z`) are **always stored in meters**, regardless of the document display units (`doc.Units`).
### Solution
Our grouping engine converts the slider's foot value directly to meters using `maxProximityFt * 0.3048`, guaranteeing precise physical clustering across all models.

---

## 6. Base Build Selection Set Not Found
### Symptom
Clicking **Base Build** reports: `"No 'Base Build' (or 'BaseBuild') Selection Set found in the document."`
### Cause
The Base Build clash runner requires a Selection Set or Search Set named `Base Build` (or `BaseBuild`, case-insensitive) in the document.
### Solution
Create a Selection Set or Search Set named `Base Build` in Navisworks containing the base build elements. The addin will immediately detect it upon execution.

---

## 7. WPF XAML Local Type Compilation Blocked by Smart App Control (0x800711C7)
### Symptom
MSBuild fails during `MarkupCompilePass2` with `error MC2000: An Application Control policy has blocked this file. (Exception from HRESULT: 0x800711C7)`.
### Cause
When XAML files reference local types via `xmlns:views="clr-namespace:..."`, WPF triggers `MarkupCompilePass2` which attempts to load the temporary compiled assembly using `Assembly.LoadFrom`. On Windows 11 with Smart App Control, this triggers SAC policy error `0x800711C7`.
### Solution
Apply attached behaviors and custom behavior wiring programmatically in code-behind (`MainWindow.xaml.cs`) instead of XAML markup, keeping XAML free of local type reflection and preventing `MarkupCompilePass2` from attempting runtime loading.

---

## 8. Selection B Stale After Model Reload
### Symptom
After reloading an appended `.nwc` model, clash tests in Clash Detective say "No items in Selection B".
### Cause
Static item snapshots (`CopyFrom(ModelItemCollection)`) lose reference if the model node is re-parsed.
### Solution
Our tool creates a SelectionSet for the model and links it to `SelectionB` via `doc.SelectionSets.CreateSelectionSource(set)`, preserving dynamic linkage across model reloads.

---

## 9. Fast Installation Guide
1. Make sure all Navisworks instances are closed.
2. Run `CypherTools_Installer.exe` or right-click `Install_CypherTools.bat` and select **Run as Administrator**.
3. The installer detects all versions from 2020 to 2026 and deploys the appropriate engine automatically.
4. Launch Navisworks Manage.

