using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
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
                var root = doc.SelectionSets.RootItem;
                TraverseSets(root, "", list, root);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error traversing search sets", ex);
            }

            return list;
        }

        private void TraverseSets(SavedItem item, string path, List<SearchSetNode> list, SavedItem rootItem)
        {
            if (item == null) return;

            string currentPath = string.IsNullOrEmpty(path) ? item.DisplayName : $"{path} > {item.DisplayName}";

            if (item != rootItem)
            {
                list.Add(new SearchSetNode
                {
                    DisplayName = item.DisplayName,
                    FullPath = currentPath,
                    IsFolder = item.IsGroup,
                    OriginalSavedItem = item
                });
            }

            if (item is FolderItem folder)
            {
                string childPath = (item == rootItem) ? "" : currentPath;
                foreach (var child in folder.Children)
                {
                    TraverseSets(child, childPath, list, rootItem);
                }
            }
            else if (item is GroupItem group)
            {
                string childPath = (item == rootItem) ? "" : currentPath;
                foreach (var child in group.Children)
                {
                    TraverseSets(child, childPath, list, rootItem);
                }
            }
        }

        public FolderItem EnsureTestsFolder(Document doc)
        {
            if (doc == null) return null;

            var rootChildren = doc.SelectionSets.RootItem.Children;
            var testsFolder = rootChildren.FirstOrDefault(x => x.DisplayName == "Tests" && x is FolderItem) as FolderItem;
            if (testsFolder == null)
            {
                var newFolder = new FolderItem { DisplayName = "Tests" };
                doc.SelectionSets.AddCopy(newFolder);
                testsFolder = doc.SelectionSets.RootItem.Children.LastOrDefault(x => x.DisplayName == "Tests" && x is FolderItem) as FolderItem;
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
                    try { doc.SelectionSets.Remove(testsFolder, existing); } catch { }
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
                    try { doc.SelectionSets.Remove(testsFolder, existing); } catch { }
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
                    string.Equals(x.DisplayName, "POC Elements", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DisplayName, "POC", StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    try { doc.SelectionSets.Remove(testsFolder, existing); } catch { }
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
                    SearchCondition.HasPropertyByDisplayName("Item", "Name").DisplayStringContains("POC"));
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
            string finalName = "POC Elements";
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
                item.DisplayName.IndexOf("POC", StringComparison.OrdinalIgnoreCase) >= 0)
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
