using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services
{
    public static class SearchSetService
    {
        public static List<SearchSetNode> GetManualSearchSets()
        {
            var list = new List<SearchSetNode>();
            var doc = Application.ActiveDocument;
            if (doc == null || doc.IsClear) return list;

            TraverseSets(doc.SelectionSets.RootItem, "", list);
            return list;
        }

        private static void TraverseSets(SavedItem item, string path, List<SearchSetNode> list)
        {
            string currentPath = string.IsNullOrEmpty(path) ? item.DisplayName : $"{path} > {item.DisplayName}";

            if (item != Application.ActiveDocument.SelectionSets.RootItem)
            {
                list.Add(new SearchSetNode { 
                    DisplayName = $"{item.DisplayName} [{item.GetType().Name}]", 
                    FullPath = currentPath, 
                    IsFolder = (item is FolderItem), 
                    OriginalSavedItem = item 
                });
            }

            if (item is FolderItem folder)
            {
                string childPath = item == Application.ActiveDocument.SelectionSets.RootItem ? "" : currentPath;
                foreach (var child in folder.Children)
                {
                    TraverseSets(child, childPath, list);
                }
            }
            else if (item is GroupItem group)
            {
                string childPath = item == Application.ActiveDocument.SelectionSets.RootItem ? "" : currentPath;
                foreach (var child in group.Children)
                {
                    TraverseSets(child, childPath, list);
                }
            }
        }

        public static FolderItem EnsureTestsFolder()
        {
            var doc = Application.ActiveDocument;
            var testsFolder = doc.SelectionSets.RootItem.Children.FirstOrDefault(x => x.DisplayName == "Tests" && x is FolderItem) as FolderItem;
            if (testsFolder == null)
            {
                testsFolder = new FolderItem { DisplayName = "Tests" };
                doc.SelectionSets.AddCopy(testsFolder);
                testsFolder = doc.SelectionSets.RootItem.Children.FirstOrDefault(x => x.DisplayName == "Tests" && x is FolderItem) as FolderItem;
            }
            return testsFolder;
        }

        public static SelectionSet GenerateModelSearchSet(ModelSourceNode modelNode, FolderItem testsFolder, ExecutionResult result)
        {
            var doc = Application.ActiveDocument;
            string baseName = System.IO.Path.GetFileNameWithoutExtension(modelNode.DisplayName);

            // Create Search condition
            Search search = new Search();
            search.Selection.SelectAll();
            
            if (!string.IsNullOrEmpty(modelNode.SourceFilePath))
            {
                search.SearchConditions.Add(SearchCondition.HasPropertyByDisplayName("Item", "Name").EqualValue(VariantData.FromDisplayString(modelNode.SourceFilePath)));
            }
            else
            {
                result.SkippedSets.Add($"Could not reliably identify criteria for {modelNode.DisplayName}");
                return null;
            }

            // Versioning logic
            string finalName = baseName;
            int version = 2;
            while (testsFolder.Children.Any(x => x.DisplayName == finalName))
            {
                finalName = $"{baseName} ({version++})";
            }

            var newSet = new SelectionSet(search) { DisplayName = finalName };
            doc.SelectionSets.AddCopy(newSet);
            
            // Re-find to return the added instance
            var saved = doc.SelectionSets.RootItem.Children.FirstOrDefault(x => x.DisplayName == "Tests" && x is FolderItem) as FolderItem;
            var finalSet = saved.Children.LastOrDefault(x => x.DisplayName == finalName) as SelectionSet;
            
            result.GeneratedSets.Add($"Tests > {finalName}");
            return finalSet;
        }
    }
}
