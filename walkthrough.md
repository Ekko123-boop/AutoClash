# Cypher Tools — Comprehensive User & Architectural Walkthrough (v2.0.2)

Cypher Tools (`CypherNavisTools.dll`) is an enterprise-grade Autodesk Navisworks Manage add-in designed for BIM Coordinators and VDC Engineers. It automates clash matrix generation, 1-to-1 model-to-set test generation ("Tools test"), Base Build clash execution, dynamic sibling search-set creation, spatial clash grouping by element, and viewpoint generation for reviewed clashes.

Supports **Autodesk Navisworks Manage 2020 through 2026**.

---

## 1. Tab 1: Generate Matrix, Tools Test & Base Build

### Full Matrix Workflow
1. Open your master federated NWF or NWD model in **Autodesk Navisworks Manage**.
2. Click **Clash Matrix** on the **Cypher** ribbon tab.
3. On the **Generate Matrix** tab:
   - **Left Card (Models / NWCs)**: Select the discipline `.nwc` models you wish to clash. Use the live search bar or the `All` / `None` buttons for fast batch selection.
   - **Right Card (Manual Search Sets)**: Select the manual search sets (e.g., `Base Build`, `MEP`, `Sign off Tools`) to clash against.
   - **Configuration**: Choose Clash Test Type (`Clearance`, `Hard`, or `Duplicate`) and Tolerance in meters.
4. Click the green **Run Clash Matrix** button.

### Sibling Set Creation ("Generate Sets")
1. Select one or more `.nwc` models in the Models list.
2. Click **Generate Sets**.
3. For each selected NWC (e.g. `F1-STS-HDLS202-MX.nwc` under `F1-MEI - A&B.nwd`):
   - Automatically traverses up to the parent `.nwd` container.
   - Collects all sibling `.nwc` models under that container **excluding the selected NWC itself** (preventing self-clash errors).
   - Generates a static Selection Set named with the trimmed model code (e.g., `STS-HDLS202-MX`) under the `Tests` folder in Navisworks.
   - Refreshes the Sets list instantly. Outdated sets with the same name are replaced in-place without duplicate `(2)`, `(3)` proliferation.

### Tools Test (1-to-1 Automated Pairing)
1. Select your target `.nwc` models.
2. Click **Tools test ({0} NWCs)**.
3. Automatically pairs each NWC to its matching Selection Set by trimmed name (e.g., `F1-STS-HDLS202-MX.nwc` matches set `STS-HDLS202-MX`).
4. Sets Selection A to the Selection Set and Selection B to the model.
5. Automatically creates and executes clash tests prefixed with `T-` (e.g., `T-STS-HDLS202-MX`).

### Base Build Automated Clash Runner
1. Select the models to test against the base building.
2. Click **Base Build ({0} NWCs)**.
3. Automatically finds the document's `Base Build` (or `BaseBuild`) Selection/Search Set.
4. Sets Selection A to Base Build and Selection B to the model.
5. Generates tests using clean model codes without the `T-` prefix (e.g., `STS-HDLS201-DR`).

---

## 2. Tab 2: Distill Clashes Workflow

1. Switch to the **Distill Clashes** tab.
2. Review all clash tests currently loaded in Clash Detective with live breakdown metrics:
   - Columns: `Active/New`, `Reviewed`, `Approved`, `Resolved`, and `Total`.
3. Configure **Distillation Settings**:
   - **Proximity Slider**: Adjust from 1 ft to 300 ft with 10 ft snap increments (25 ft default). Coordinates are mapped directly to Navisworks internal meters (`feet * 0.3048`).
4. Execute:
   - **Distill Selected Test(s)**: Distills only checked tests.
   - **Distill All Tests**: Batch distills every test into clean `{TestName}-{001, 002, ...}` groups organized by parent elements in Selection A, moving items in descending index order to avoid mutation shifts.
   - **Re-Run Selected**: Re-evaluates test geometry while preserving existing reviewed statuses.

---

## 3. Tab 3: Create Viewpoints Workflow

1. Switch to the **Create Viewpoints** tab.
2. Check the clash tests you want to export viewpoints for.
3. Configure **Viewpoint Settings**:
   - **Group by**: Dedicated Test Folder.
   - **Place in timestamped master folder**: Optional container stamped with `yyyy-MM-dd HH.mm.ss`.
   - **Include statuses**: Toggle chips `[ New ] [ Active ] [ Reviewed ] [ Approved ] [ Resolved ]` to selectively export only desired clash states.
4. Click **Create Viewpoints for Selected Test(s)** or **Create Viewpoints for All Tests**.
5. Viewpoints generate into the **Saved Viewpoints** window. On Navisworks 2020–2023, an active camera fallback ensures full compatibility even where `TestsViewpointForResult` is unavailable.

---

## 4. Stability & Crash Resolution (v2.0.2 Post-Mortem)

In v2.0.2, a fatal `0xC0000005` Access Violation crash that occurred when an `.nwf` or `.nwd` model was open was completely eliminated:
- **WPF Modal Owner**: Replaced `Process.GetCurrentProcess().MainWindowHandle` (which resolved to off-screen worker threads in `roamer.exe`) with `Autodesk.Navisworks.Api.Application.Gui.MainWindow.Handle` inside a safe `try-catch`.
- **Single Authoritative Bundle**: Re-architected installers to deploy exclusively to `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle`, actively purging duplicate standalone plugins from `Program Files` and `AppData` to prevent ribbon hook collisions.
- **Clash Transactions**: Removed outer `doc.BeginTransaction()` calls around `TestsRunTest()` to avoid conflicting with Navisworks 2023's internal clash detective transactions.

---

## 5. 1-Click Installation & Deployment

### For End Users & Executives (`CypherTools_Installer.exe`)
- Sleek Windows GUI installer with auto-detection of all installed Navisworks versions (2020 through 2026).
- **Uninstall**: 1-click clean purge of all legacy plugins across Program Files and AppData.
- **Install / Update**: 1-click installation of the clean, verified v2.0.2 bundle to `C:\ProgramData\Autodesk\ApplicationPlugins\CypherNavisTools.bundle`.

### For Administrators (`Install_CypherTools.bat`)
- Auto-elevating UAC batch installer immune to Windows 11 Smart App Control (SAC).
- Automatically purges duplicate standalone plugins and stages the canonical bundle.
- Clean uninstallation via `Uninstall_CypherTools.bat`.
