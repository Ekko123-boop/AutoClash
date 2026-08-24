# Automated Model Clash Runner for Navisworks 2024

## Project Overview
The **Automated Model Clash Runner** is a production-grade Autodesk Navisworks Manage 2024 Add-in designed to drastically reduce the time spent manually setting up clash detection matrices. 

In complex BIM coordination workflows, BIM Managers often have a standard set of manual Search Sets (e.g., "AS BUILT", "Clearance Zones", "Sign off Tools") that need to be clashed against dozens of continuously updated federated models or NWC branches.

This tool automates the process by allowing the user to select specific model files (NWCs/NWDs) and manual Search Sets. The plugin dynamically generates the required model Search Sets, maps them against the manual sets, and executes the clash tests in the Navisworks Clash Detective with strict settings (e.g., `Tolerance = 0.000m`, `Clearance`).

## Key Features
- **Intelligent Model Discovery**: Recursively traverses federated NWDs to find nested `.nwc`, `.rvt`, and `.dwg` leaf files by reading the `Source File Name` properties.
- **Dynamic Set Generation**: Automatically creates and versions Search Sets for the selected models inside a dedicated `Tests` folder in the Navisworks Sets pane.
- **Clash Matrix Automation**: Cross-multiplies selected Models × selected Manual Sets, skipping tests that already exist.
- **Summary Reporting**: Provides an immediate post-execution summary dialog detailing exactly which tests succeeded, which were skipped, and any warnings.

## Architecture
- **Framework**: .NET Framework 4.8 (x64)
- **UI**: WPF (Windows Presentation Foundation) with MVVM architecture.
- **Navisworks API**: Utilizes `Autodesk.Navisworks.Api` for model traversal and Set generation, and `Autodesk.Navisworks.Api.Clash` for executing clash tests programmatically.

## Installation & Usage
1. Build the solution in Visual Studio 2022 (`Release` mode).
2. Copy the `AutomatedClashRunner.bundle` to your Autodesk plugins directory:
   `%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle`
3. Launch Navisworks Manage 2024, click the "Automated Clash Runner" ribbon button, select your models and sets, and click "Run Clash Tests".
