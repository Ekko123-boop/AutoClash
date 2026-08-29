# Automated Clash Runner - Project Walkthrough

## 1. Project Goal
The goal of this project was to automate the highly tedious process of creating clash tests in Navisworks Manage. Instead of manually creating dozens of tests and selecting geometry each time, the user can now:
1. Select their predefined manual Search Sets (e.g., Base Build, In Progress Tools).
2. Select the federated subcontractor models they want to clash against.
3. Click "Run Clash Matrix" to instantly generate all the necessary Clash Tests, assign the selections perfectly, and run the clashes.

## 2. Key Challenges & API Bugs Overcome

### The .rvt Ghost Property Bug
**The Problem**: When searching Navisworks for .nwc files, the internal Source File Name property often incorrectly reported the original .rvt Revit file instead.
**The Solution**: We abandoned string-based internal property searches and built a recursive tree-walker (ModelDiscoveryService.cs) that scans the physical nodes in the Navisworks Selection Tree using their exact DisplayName.

### The Static Set UI Checkbox Bug
**The Problem**: The WPF UI was hiding checkboxes for Manual Sets if they weren't strictly of type FolderItem. This caused standard Static Sets to be unselectable.
**The Solution**: We updated the MVVM binding to rely on Navisworks' IsGroup property. We also added validation to prevent silent failures if the user clicked "Run" with 0 sets selected.

### The "Sets" Tab AccessViolationException
**The Problem**: The user required Selection A to be formally bound to the "Sets" tab in the Clash Detective. Attempting to do this via the documented .NET API (
ew SelectionSourceCollection()) triggered a fatal memory corruption error (AccessViolationException) that crashed the entire plugin loader.
**The Solution**: We found an undocumented backdoor: by skipping the constructor and directly adding the SelectionSource to the pre-instantiated memory collection (	est.SelectionA.Selection.SelectionSources.Add()), we achieved the exact result safely.

### The Empty Scope Selection Bug
**The Problem**: When generating Sets for the .nwc models, the API's unreliable string properties caused the scope to return empty. Navisworks responded by silently selecting the top-most node in the tree for Selection B (which was incorrect).
**The Solution**: We rewrote the .nwc generated sets to be **Static Sets** initialized with the exact memory reference of the ModelItem node we discovered during the tree walk. This bypassed all string searches and guaranteed mathematically perfect selection.

## 3. How to Use
1. Open your NWD federated model in Navisworks Manage 2024.
2. Go to the **Tool Add-ins 1** tab on the Ribbon and click **Automated Clash Runner**.
3. Check the manual sets you want to clash on the left.
4. Check the models you want to clash against on the right.
5. Click **Run Clash Matrix**.
6. The plugin will create a "Tests" folder in your Sets window, generate Static Sets for your models, and create a perfectly mapped Clash Test for each combination.

## 4. Distribution (Multi-Version Installer)
To distribute this addin to other team members:
1. Locate the AutomatedClashRunner_Installer.exe file.
2. Send this single .exe file to anyone.
3. They just double-click it. It will automatically install the plugin into their Autodesk %APPDATA% folder.
4. **Supported Versions**: It is automatically configured to work out-of-the-box on Navisworks Manage **2022, 2023, 2024, and 2025**.

### Dynamic Clash Naming Rule
To prevent duplicate test names when clashing a single model against multiple different manual sets, the system automatically checks the name of the manual set you selected:
- If clashing against **Base Build**, it uses the exact trimmed model name (e.g., STS-HDLS201-DR).
- If clashing against **anything else** (e.g., AS BUILT), it intelligently injects a T- prefix to differentiate the test (e.g., T-STS-HDLS201-DR).
