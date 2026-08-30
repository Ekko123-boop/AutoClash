# Automated Clash Runner & Distiller — Comprehensive User Walkthrough

## 1. Tab 1: Generate Matrix Workflow

1. Open your master federated NWF or NWD model in **Autodesk Navisworks Manage** (2022 to 2026).
2. Click **Automated Clash Runner** on the Add-ins ribbon.
3. On the **Generate Matrix** tab:
   - **Left Card (Models / NWCs)**: Select the discipline `.nwc` models you wish to clash. Use the live search bar or the `All` / `None` buttons for fast batch selection.
   - **Right Card (Manual Search Sets)**: Select the manual search sets (e.g. `Base Build`, `MEP`, `Sign off Tools`) to clash against.
   - **Bottom Card (Configuration)**:
     - **Clash Test Type**: Choose `Clearance`, `Hard`, or `Duplicate`.
     - **Tolerance (m)**: Specify numeric tolerance in meters (e.g., `0` or `0.025`).
     - **Selection B Mode**: Standard Model Hierarchy (automatically targets the physical `.nwc` model node from the model tree).
4. Click the solid green **Run Clash Matrix (X Tests)** button.
5. Review the execution progress in the progress overlay.
6. The **Execution Summary** window will display the outcomes (color-coded ✓, ⚠, ✗) with single-click **Copy to Clipboard** and **Export to CSV**.

---

## 2. Tab 2: Distill Clashes Workflow

1. Switch to the **Distill Clashes** tab.
2. Review all clash tests currently loaded in Navisworks Clash Detective with live metric counts:
   - Columns: `Active/New`, `Reviewed`, `Approved`, `Resolved`, and `Total`.
3. Configure the **Distillation Settings**:
   - **Proximity Clustering Range Slider**: Adjust from **Focused** (tight spatial clustering) to **Global** (broad area grouping) between 1 ft and 150 ft.
4. Execute Distillation:
   - **Distill Selected Test(s)**: Distills only checked tests.
   - **Distill All Tests**: Single-click batch distillation of every clash test in the project into clean `{TestName}-{001, 002, ...}` groups organized by parent elements.
   - **Re-Run Selected**: Re-evaluates test geometry while preserving existing reviewed statuses.

---

## 3. Tab 3: Create Viewpoints Workflow

1. Switch to the **Create Viewpoints** tab.
2. Check the clash tests you want to export viewpoints for.
3. Configure **Viewpoint Settings**:
   - **Group by**: Dedicated Test Folder.
   - **Place in timestamped master folder**: Optional checkbox to group into a timestamped container folder.
   - **Include statuses**: Toggle button chips `[ New ] [ Active ] [ Reviewed ] [ Approved ] [ Resolved ]` to selectively filter which clash states are exported into viewpoints.
4. Click **Create Viewpoints for Selected Test(s)** or **Create Viewpoints for All Tests**.
5. The viewpoints are immediately created and organized inside the Navisworks **Saved Viewpoints** window.

---

## 4. Multi-Version Support & Installation

- **Supported Navisworks Releases**: 2022, 2023, 2024, 2025, and 2026.
- **Universal Installer**: Run `AutomatedClashRunner_Installer.exe` once. It deploys the `.bundle` package to `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle`.
- **Automatic Loading**: All installed Navisworks versions on the machine automatically detect and load the plugin on launch.
