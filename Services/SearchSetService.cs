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
                    DisplayName = $"{item.DisplayName}", 
                    FullPath = currentPath, 
                    IsFolder = item.IsGroup, 
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
            string baseName = NamingService.GetTrimmedModelCode(modelNode.DisplayName);

            // Versioning logic
            string finalName = baseName;
            int version = 2;
            while (testsFolder.Children.Any(x => x.DisplayName == finalName))
            {
                finalName = $"{baseName} ({version++})";
            }

            // Create a Static Set pointing directly to the exact model node we discovered
            var modelColl = new ModelItemCollection();
            modelColl.Add(modelNode.OriginalModelItem);
            var newSet = new SelectionSet(modelColl) { DisplayName = finalName };
            doc.SelectionSets.AddCopy(newSet);
            
            // Re-find to return the added instance from RootItem where AddCopy places it
            var finalSet = doc.SelectionSets.RootItem.Children.LastOrDefault(x => x.DisplayName == finalName) as SelectionSet;
            
            // Try to move it to Tests folder if possible, otherwise leave in root
            try {
                if (finalSet != null) {
                    doc.SelectionSets.Move(finalSet.Parent, doc.SelectionSets.RootItem.Children.IndexOf(finalSet), testsFolder, testsFolder.Children.Count);
                }
            } catch { }

            result.GeneratedSets.Add($"Tests > {finalName}");
            return finalSet;
        }
    }
}
