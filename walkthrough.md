# Automated Clash Runner — Walkthrough Guide

## Matrix Generator Workflow

1. Open your master federated NWF or NWD model in **Autodesk Navisworks Manage**.
2. Click **Automated Clash Runner** on the Add-ins ribbon.
3. On the **Generate Matrix** tab:
   - **Left Panel (Models)**: Check the `.nwc` models you wish to clash. Use the search bar or the `☑ All` / `☐ None` buttons for quick filtering.
   - **Right Panel (Search Sets)**: Check the manual search sets (e.g. `Base Build`, `MEP`) you want to test against.
   - **Configuration Panel**: Choose your **Clash Type** (`Clearance`, `Hard`, `Duplicate`) and set your **Tolerance** in meters.
4. Click **Run Clash Tests (N)**.
5. Review the execution results in the **Execution Summary** window. You can copy the results or export them to a `.csv` file.

---

## Distiller & Test Management Workflow

1. Switch to the **Distiller / Manage Tests** tab.
2. View and filter all existing clash tests from Clash Detective.
3. Select the clash tests you want to manage:
   - **Re-Run Selected**: Refreshes tests with the latest geometry changes.
   - **Group by Element**: Adjust the **Max Group Proximity (ft)** slider/box, then click **Group by Element**. Raw clashes are automatically grouped and numbered by parent element and spatial proximity.
   - **Export 'Reviewed' Viewpoints**: Filter and export all reviewed clash groups directly to Navisworks **Saved Viewpoints** in organized test folders.
