using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Common;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class SearchSetService : ISearchSetService
    {
        private readonly INamingService _naming;
        private readonly ILoggerService _logger;

        public static SearchSetService Instance { get; } = new SearchSetService(NamingService.Instance, LoggerService.Instance);

        public SearchSetService(INamingService naming, ILoggerService logger)
        {
            _naming = naming ?? NamingService.Instance;
            _logger = logger ?? LoggerService.Instance;
        }

        public List<SearchSetNode> GetManualSearchSets(Document doc)
        {
            var list = new List<SearchSetNode>();
            if (doc == null || doc.IsClear) return list;

            try
            {
                var visitedGuids = new HashSet<Guid>();

                // 1. Primary .NET traversal: doc.SelectionSets.RootItem.Children
                if (doc.SelectionSets?.RootItem != null && doc.SelectionSets.RootItem.Children != null)
                {
                    foreach (SavedItem child in doc.SelectionSets.RootItem.Children)
                    {
                        TraverseSets(child, "", list, visitedGuids);
                    }
                }

                // 2. Secondary fallback: doc.SelectionSets.Value
                if (doc.SelectionSets?.Value != null)
                {
                    foreach (SavedItem item in doc.SelectionSets.Value)
                    {
                        TraverseSets(item, "", list, visitedGuids);
                    }
                }

                // 3. Tertiary fallback via COM API if .NET traversal found 0 items
                if (list.Count == 0)
                {
                    TryLoadFromComApi(doc, list, visitedGuids);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error traversing search sets", ex);
            }

            _logger.Log($"Discovered {list.Count} selection/search sets ({list.Count(x => !x.IsFolder)} selectable sets, {list.Count(x => x.IsFolder)} folders) in document.");
            return list;
        }

        private void TraverseSets(SavedItem item, string path, List<SearchSetNode> list, HashSet<Guid> visitedGuids)
        {
            if (item == null) return;

            // Prevent circular or duplicate visits
            if (item.Guid != Guid.Empty && visitedGuids.Contains(item.Guid))
                return;

            string currentPath = string.IsNullOrEmpty(path) ? (item.DisplayName ?? "Unnamed") : $"{path} > {item.DisplayName ?? "Unnamed"}";

            if (item.Guid != Guid.Empty)
            {
                visitedGuids.Add(item.Guid);
            }

            list.Add(new SearchSetNode
            {
                DisplayName = item.DisplayName ?? "Unnamed",
                FullPath = currentPath,
                IsFolder = item.IsGroup,
                OriginalSavedItem = item
            });

            if (item is GroupItem group && group.Children != null)
            {
                foreach (SavedItem child in group.Children)
                {
                    TraverseSets(child, currentPath, list, visitedGuids);
                }
            }
        }

        private void TryLoadFromComApi(Document doc, List<SearchSetNode> list, HashSet<Guid> visitedGuids)
        {
            try
            {
                var state = Autodesk.Navisworks.Api.ComApi.ComApiBridge.State;
                if (state == null) return;

                var oSSExColl = state.SelectionSetsEx();
                if (oSSExColl == null || oSSExColl.Count == 0) return;

                _logger.Log($"Checking COM API selection sets ({oSSExColl.Count} root items)...");
                TraverseComCollection(doc, oSSExColl, new List<int>(), "", list, visitedGuids);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"COM API selection sets fallback warning: {ex.Message}");
            }
        }

        private void TraverseComCollection(
            Document doc,
            Autodesk.Navisworks.Api.Interop.ComApi.InwSelectionSetExColl coll,
            List<int> parentIndices,
            string path,
            List<SearchSetNode> list,
            HashSet<Guid> visitedGuids)
        {
            if (coll == null) return;

            for (int i = 1; i <= coll.Count; i++)
            {
                try
                {
                    var item = coll[i];
                    var currentIndices = new List<int>(parentIndices) { i - 1 };

                    SavedItem netItem = null;
                    try
                    {
                        netItem = doc.SelectionSets.ResolveIndexPath(currentIndices);
                    }
                    catch { }

                    if (item is Autodesk.Navisworks.Api.Interop.ComApi.InwSelectionSetFolder folder)
                    {
                        string folderName = folder.name ?? "Folder";
                        string currentPath = string.IsNullOrEmpty(path) ? folderName : $"{path} > {folderName}";

                        if (netItem != null && (netItem.Guid == Guid.Empty || !visitedGuids.Contains(netItem.Guid)))
                        {
                            if (netItem.Guid != Guid.Empty) visitedGuids.Add(netItem.Guid);
                            list.Add(new SearchSetNode
                            {
                                DisplayName = folderName,
                                FullPath = currentPath,
                                IsFolder = true,
                                OriginalSavedItem = netItem
                            });
                        }

                        var subColl = folder.SelectionSets();
                        if (subColl != null && subColl.Count > 0)
                        {
                            TraverseComCollection(doc, subColl, currentIndices, currentPath, list, visitedGuids);
                        }
                    }
                    else if (item is Autodesk.Navisworks.Api.Interop.ComApi.InwOpSelectionSet set)
                    {
                        string setName = set.name ?? "Set";
                        string currentPath = string.IsNullOrEmpty(path) ? setName : $"{path} > {setName}";

                        if (netItem != null && (netItem.Guid == Guid.Empty || !visitedGuids.Contains(netItem.Guid)))
                        {
                            if (netItem.Guid != Guid.Empty) visitedGuids.Add(netItem.Guid);
                            list.Add(new SearchSetNode
                            {
                                DisplayName = setName,
                                FullPath = currentPath,
                                IsFolder = false,
                                OriginalSavedItem = netItem
                            });
                        }
                    }
                }
                catch (Exception itemEx)
                {
                    _logger.LogWarning($"Error traversing COM set item at index {i}: {itemEx.Message}");
                }
            }
        }

        public FolderItem EnsureTestsFolder(Document doc)
        {
            if (doc == null) return null;

            var rootChildren = doc.SelectionSets.RootItem.Children;
            var testsFolder = rootChildren.FirstOrDefault(x => x.DisplayName == AppConstants.TestsFolderName && x is FolderItem) as FolderItem;
            if (testsFolder == null)
            {
                var newFolder = new FolderItem { DisplayName = AppConstants.TestsFolderName };
                doc.SelectionSets.AddCopy(newFolder);
                testsFolder = doc.SelectionSets.RootItem.Children.LastOrDefault(x => x.DisplayName == AppConstants.TestsFolderName && x is FolderItem) as FolderItem;
            }
            return testsFolder;
        }

        public SelectionSet GenerateModelSearchSet(Document doc, ModelSourceNode modelNode, FolderItem testsFolder, ExecutionResult result)
        {
            if (doc == null || modelNode?.OriginalModelItem == null) return null;

            string baseName = _naming.GetTrimmedModelCode(modelNode.DisplayName);

            // Clean up previous set with same name in Tests folder to prevent (2), (3) proliferation
            if (testsFolder != null)
            {
                var existing = testsFolder.Children.FirstOrDefault(x => string.Equals(x.DisplayName, baseName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    try { doc.SelectionSets.Remove(testsFolder, existing); }
                    catch (Exception ex) { _logger.LogWarning($"Could not remove previous set '{existing.DisplayName}': {ex.Message}"); }
                }
            }

            string finalName = baseName;

            // Create a Static Set directly referencing the discovered model item
            var modelColl = new ModelItemCollection { modelNode.OriginalModelItem };
            var newSet = new SelectionSet(modelColl) { DisplayName = finalName };
            
            doc.SelectionSets.AddCopy(newSet);

            // AddCopy places the newly created item as a child of RootItem
            var addedSet = doc.SelectionSets.RootItem.Children.OfType<SelectionSet>()
                .FirstOrDefault(s => string.Equals(s.DisplayName, finalName, StringComparison.OrdinalIgnoreCase))
                ?? doc.SelectionSets.RootItem.Children.LastOrDefault() as SelectionSet;

            // Move to Tests folder if available
            if (addedSet != null && testsFolder != null)
            {
                try
                {
                    int rootIndex = doc.SelectionSets.RootItem.Children.IndexOf(addedSet);
                    if (rootIndex >= 0)
                    {
                        doc.SelectionSets.Move(doc.SelectionSets.RootItem, rootIndex, testsFolder, testsFolder.Children.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to move SelectionSet '{finalName}' into 'Tests' folder: {ex.Message}");
                }
            }

            result?.GeneratedSets.Add($"Tests > {finalName}");
            return addedSet;
        }

        public SelectionSet GenerateSiblingSearchSet(
            Document doc,
            ModelSourceNode targetNwc,
            List<ModelItem> siblingNwcs,
            FolderItem testsFolder,
            ExecutionResult result)
        {
            if (doc == null || targetNwc?.OriginalModelItem == null || siblingNwcs == null || siblingNwcs.Count == 0)
                return null;

            string baseName = _naming.GetTrimmedModelCode(targetNwc.DisplayName);

            // Clean up previous set with same name in Tests folder to prevent (2), (3) proliferation
            if (testsFolder != null)
            {
                var existing = testsFolder.Children.FirstOrDefault(x => string.Equals(x.DisplayName, baseName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    try { doc.SelectionSets.Remove(testsFolder, existing); }
                    catch (Exception ex) { _logger.LogWarning($"Could not remove previous set '{existing.DisplayName}': {ex.Message}"); }
                }
            }

            string finalName = baseName;

            // Create a Static Set directly referencing all sibling NWC model items
            var modelColl = new ModelItemCollection();
            foreach (var sibling in siblingNwcs)
            {
                if (sibling != null) modelColl.Add(sibling);
            }

            var newSet = new SelectionSet(modelColl) { DisplayName = finalName };
            doc.SelectionSets.AddCopy(newSet);

            // AddCopy places the newly created item as a child of RootItem
            var addedSet = doc.SelectionSets.RootItem.Children.OfType<SelectionSet>()
                .FirstOrDefault(s => string.Equals(s.DisplayName, finalName, StringComparison.OrdinalIgnoreCase))
                ?? doc.SelectionSets.RootItem.Children.LastOrDefault() as SelectionSet;

            // Move to Tests folder if available
            if (addedSet != null && testsFolder != null)
            {
                try
                {
                    int rootIndex = doc.SelectionSets.RootItem.Children.IndexOf(addedSet);
                    if (rootIndex >= 0)
                    {
                        doc.SelectionSets.Move(doc.SelectionSets.RootItem, rootIndex, testsFolder, testsFolder.Children.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to move SelectionSet '{finalName}' into 'Tests' folder: {ex.Message}");
                }
            }

            string setNamePath = testsFolder != null ? $"Tests > {finalName}" : finalName;
            result?.GeneratedSets.Add($"{setNamePath} ({siblingNwcs.Count} models)");
            _logger.Log($"Generated sibling Selection Set '{setNamePath}' containing {siblingNwcs.Count} sibling NWCs (excluding {targetNwc.DisplayName}).");

            return addedSet;
        }

        public SavedItem GetOrCreatePocSearchSet(Document doc, ExecutionResult result = null)
        {
            if (doc == null || doc.IsClear) return null;

            var testsFolder = EnsureTestsFolder(doc);

            // 1. Purge any previous "POC Elements" set in Tests folder to prevent duplicate proliferation (ISS-031)
            if (testsFolder != null)
            {
                var existing = testsFolder.Children.FirstOrDefault(x =>
                    string.Equals(x.DisplayName, AppConstants.PocSearchSetName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DisplayName, AppConstants.PocKeyword, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    try { doc.SelectionSets.Remove(testsFolder, existing); }
                    catch (Exception ex) { _logger.LogWarning($"Could not remove previous set '{existing.DisplayName}': {ex.Message}"); }
                }
            }

            // 2. Discover all elements having "POC" in their name
            var pocItems = new ModelItemCollection();

            // Try native search query first
            try
            {
                var search = new Search();
                search.Selection.SelectAll();
                search.SearchConditions.Add(
                    SearchCondition.HasPropertyByDisplayName("Item", "Name").DisplayStringContains(AppConstants.PocKeyword));
                var found = search.FindAll(doc, false);
                if (found != null && found.Count > 0)
                {
                    pocItems.AddRange(found);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Native Search query for 'POC' threw: {ex.Message}. Falling back to hierarchy traversal.");
            }

            // Fallback to recursive hierarchy traversal if Search returned 0 items
            if (pocItems.Count == 0 && doc.Models != null)
            {
                foreach (Model m in doc.Models)
                {
                    if (m?.RootItem != null)
                    {
                        CollectPocItems(m.RootItem, pocItems);
                    }
                }
            }

            if (pocItems.Count == 0)
            {
                _logger.LogWarning("No elements containing 'POC' in their name were found in the document.");
                return null;
            }

            // 3. Create static SelectionSet named "POC Elements"
            string finalName = AppConstants.PocSearchSetName;
            var newSet = new SelectionSet(pocItems) { DisplayName = finalName };
            doc.SelectionSets.AddCopy(newSet);

            var addedSet = doc.SelectionSets.RootItem.Children.OfType<SelectionSet>()
                .FirstOrDefault(s => string.Equals(s.DisplayName, finalName, StringComparison.OrdinalIgnoreCase))
                ?? doc.SelectionSets.RootItem.Children.LastOrDefault() as SelectionSet;

            // Move to Tests folder
            if (addedSet != null && testsFolder != null)
            {
                try
                {
                    int rootIndex = doc.SelectionSets.RootItem.Children.IndexOf(addedSet);
                    if (rootIndex >= 0)
                    {
                        doc.SelectionSets.Move(doc.SelectionSets.RootItem, rootIndex, testsFolder, testsFolder.Children.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to move SelectionSet '{finalName}' into 'Tests' folder: {ex.Message}");
                }
            }

            string setNamePath = testsFolder != null ? $"Tests > {finalName}" : finalName;
            result?.GeneratedSets.Add($"{setNamePath} ({pocItems.Count} POC items)");
            _logger.Log($"Generated Selection Set '{setNamePath}' containing {pocItems.Count} POC elements.");

            return addedSet;
        }

        private void CollectPocItems(ModelItem item, ModelItemCollection collection)
        {
            if (item == null) return;

            if (!string.IsNullOrEmpty(item.DisplayName) &&
                item.DisplayName.IndexOf(AppConstants.PocKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                collection.Add(item);
            }

            foreach (ModelItem child in item.Children)
            {
                CollectPocItems(child, collection);
            }
        }
    }
}
