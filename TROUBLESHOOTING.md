# Automated Clash Runner — Troubleshooting & FAQ

## 1. Where are the log files located?
Diagnostic logs are automatically written to:
`%LOCALAPPDATA%\AutomatedClashRunner\Logs\session_YYYY-MM-DD.log`

If you encounter unexpected behavior, inspect this file to view detailed stack traces and warnings.

---

## 2. Navisworks SelectionSourceCollection Crash
### Symptom
`System.AccessViolationException` when creating a clash test.
### Cause
In Navisworks Manage 2024+, instantiating `new SelectionSourceCollection()` triggers an uncatchable JIT access violation.
### Solution
We bypass the constructor and directly add `SelectionSource` items to the pre-instantiated collection on `ClashTest.SelectionA.Selection.SelectionSources.Add(source)`.

---

## 3. Coordinate System & Distiller Proximity
### Symptom
Clash grouping distances feel inaccurate or different depending on whether the project is in feet or millimeters.
### Cause
Navisworks internal coordinates (`Point3D.X, Y, Z`) are **always stored in meters**, regardless of the document display units (`doc.Units`).
### Solution
Our grouping engine converts the slider's foot value directly to meters using `maxProximityFt * 0.3048`, guaranteeing precise physical clustering across all models.

---

## 4. Selection B Stale After Model Reload
### Symptom
After reloading an appended `.nwc` model, clash tests in Clash Detective say "No items in Selection B".
### Cause
Static item snapshots (`CopyFrom(ModelItemCollection)`) lose reference if the model node is re-parsed.
### Solution
Our tool creates a SelectionSet for the model and links it to `SelectionB` via `doc.SelectionSets.CreateSelectionSource(set)`, preserving dynamic linkage across model reloads.

---

## 5. Plugin Not Appearing in Navisworks (Missing DLL Dependencies)
### Symptom
Plugin completely fails to appear in the Navisworks Add-ins tab, despite being placed in the correct `ApplicationPlugins` folder.
### Cause
If the project uses third-party NuGet packages (like `System.Drawing.Common`), compiling the project might not automatically copy these dependencies into the bundle. When Navisworks tries to load the main plugin, the CLR checks for dependencies, fails to find them, and throws a silent `FileNotFoundException`.
### Solution
Ensure `build_all.ps1` or the installer explicitly bundles all output `*.dll` files alongside the main plugin file in the `.bundle` folder.

---

## 6. Plugin Fails to Load in Older Navisworks Versions (Assembly Binding)
### Symptom
Plugin loads successfully on the developer's machine (e.g., Navisworks 2024), but silently fails to appear on a user's machine running an older version (e.g., Navisworks 2023).
### Cause
By default, MSBuild sets `SpecificVersion=True` for referenced Navisworks assemblies. If compiled against 2024 API (v21.0.0.0), the CLR demands this exact version and will instantly reject loading into 2023 (v20.0.0.0).
### Solution
In the `.csproj`, explicitly add `<SpecificVersion>False</SpecificVersion>` to all Autodesk dependencies. Furthermore, always compile against the API of the **oldest** Navisworks version you intend to support (e.g., Navisworks 2022/2023).

---

## 7. Backward Compatibility Crashes (MissingMethodException)
### Symptom
Plugin completely fails to load in older Navisworks, or crashes midway, with a `MissingMethodException`.
### Cause
Newer Navisworks APIs introduce methods that do not exist in older versions. For example, `DocumentClashTests.TestsViewpointForResult()` was added in Navisworks 2024. If your plugin calls this, the JIT compiler will fail to load the plugin on 2023 because the method signature doesn't exist in the host engine.
### Solution
Use `System.Reflection` to dynamically invoke newer methods inside a `try/catch` block, allowing the plugin to gracefully bypass the functionality on older engines while retaining it on newer ones.

---

## 8. Basic Installation Issues
1. Ensure Autodesk Navisworks Manage is closed.
2. Run `AutomatedClashRunner_Installer.exe` (Run as Administrator if necessary).
3. Check `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle\PackageContents.xml` exists.
4. Launch Navisworks Manage and look under the **Add-ins / Tool Add-ins** tab.
