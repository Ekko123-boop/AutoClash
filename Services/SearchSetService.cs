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

            // Versioning logic within Tests folder
            string finalName = baseName;
            int version = 2;
            if (testsFolder != null)
            {
                while (testsFolder.Children.Any(x => string.Equals(x.DisplayName, finalName, StringComparison.OrdinalIgnoreCase)))
                {
                    finalName = $"{baseName} ({version++})";
                }
            }

            // Create a Static Set directly referencing the discovered model item
            var modelColl = new ModelItemCollection { modelNode.OriginalModelItem };
            var newSet = new SelectionSet(modelColl) { DisplayName = finalName };
            
            doc.SelectionSets.AddCopy(newSet);

            // AddCopy always places the newly created item as the last child of RootItem
            var addedSet = doc.SelectionSets.RootItem.Children.LastOrDefault() as SelectionSet;

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

            // Versioning logic within Tests folder
            string finalName = baseName;
            int version = 2;
            if (testsFolder != null)
            {
                while (testsFolder.Children.Any(x => string.Equals(x.DisplayName, finalName, StringComparison.OrdinalIgnoreCase)))
                {
                    finalName = $"{baseName} ({version++})";
                }
            }

            // Create a Static Set directly referencing all sibling NWC model items
            var modelColl = new ModelItemCollection();
            foreach (var sibling in siblingNwcs)
            {
                if (sibling != null) modelColl.Add(sibling);
            }

            var newSet = new SelectionSet(modelColl) { DisplayName = finalName };
            doc.SelectionSets.AddCopy(newSet);

            // AddCopy always places the newly created item as the last child of RootItem
            var addedSet = doc.SelectionSets.RootItem.Children.LastOrDefault() as SelectionSet;

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
    }
}
