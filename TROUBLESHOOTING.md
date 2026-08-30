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

## 5. Plugin Not Appearing in Navisworks
1. Ensure Autodesk Navisworks Manage is closed.
2. Run `AutomatedClashRunner_Installer.exe` or `.\build_all.ps1`.
3. Check `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle\PackageContents.xml` exists.
4. Launch Navisworks Manage and look under the **Add-ins / Tool Add-ins** tab.
