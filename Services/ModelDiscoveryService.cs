using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services
{
    public static class ModelDiscoveryService
    {
        public static List<ModelSourceNode> DiscoverModels()
        {
            var nodes = new List<ModelSourceNode>();
            var doc = Application.ActiveDocument;
            if (doc == null || doc.IsClear) return nodes;

            foreach (var model in doc.Models)
            {
                // We add the model itself if it's a file
                string fn = model.FileName ?? model.SourceFileName;
                bool addedModel = false;
                
                if (!string.IsNullOrEmpty(fn) && !fn.EndsWith(".nwd", System.StringComparison.OrdinalIgnoreCase))
                {
                    nodes.Add(new ModelSourceNode
                    {
                        DisplayName = System.IO.Path.GetFileName(fn),
                        SourceFilePath = model.SourceFileName,
                        IsDirectNwc = fn.EndsWith(".nwc", System.StringComparison.OrdinalIgnoreCase),
                        ParentContainerName = "Document Root",
                        OriginalModelItem = model.RootItem,
                        IsSelectable = true
                    });
                    addedModel = true;
                }

                // If it's an NWD or NWF, we recurse into it to find its actual files (up to depth 3)
                // If we already added the model (e.g. it was an NWC), we don't recurse into its geometry.
                if (!addedModel || fn.EndsWith(".nwd", System.StringComparison.OrdinalIgnoreCase))
                {
                    string parentName = !string.IsNullOrEmpty(fn) ? System.IO.Path.GetFileName(fn) : "Federated Model";
                    foreach (var child in model.RootItem.Children)
                    {
                        FindModelNodes(child, parentName, nodes, 0);
                    }
                }
            }
            return nodes;
        }

        private static void FindModelNodes(ModelItem item, string parentName, List<ModelSourceNode> nodes, int depth)
        {
            if (item == null || depth > 3) return; // Prevent infinite recursion or deep geometry parsing

            var srcFileProp = item.PropertyCategories.FindPropertyByDisplayName("Item", "Source File Name");
            var srcFileProp2 = item.PropertyCategories.FindPropertyByDisplayName("Item", "Source File");
            string srcFile = srcFileProp != null ? srcFileProp.Value.ToDisplayString() : (srcFileProp2 != null ? srcFileProp2.Value.ToDisplayString() : null);

            // A file in Navisworks usually has a ClassDisplayName containing "File" or has the Source File property.
            bool isFile = !string.IsNullOrEmpty(srcFile) || (item.ClassDisplayName != null && item.ClassDisplayName.Contains("File"));

            if (isFile)
            {
                string fn = !string.IsNullOrEmpty(srcFile) ? System.IO.Path.GetFileName(srcFile) : (item.DisplayName ?? "Unknown File");
                bool isNwc = fn.EndsWith(".nwc", System.StringComparison.OrdinalIgnoreCase);
                bool isNwd = fn.EndsWith(".nwd", System.StringComparison.OrdinalIgnoreCase);

                nodes.Add(new ModelSourceNode
                {
                    DisplayName = fn,
                    SourceFilePath = srcFile,
                    IsDirectNwc = isNwc,
                    ParentContainerName = parentName,
                    OriginalModelItem = item,
                    IsSelectable = true,
                    WarningMessage = string.IsNullOrEmpty(srcFile) ? "Identified by class, no source property" : null
                });

                // If it's a leaf file like NWC, RVT, DWG, do NOT recurse into its geometry!
                if (!isNwd) 
                {
                    return;
                }
                
                // If it IS an NWD, we DO recurse because it might contain more NWCs inside!
                parentName = fn;
            }

            foreach (var child in item.Children)
            {
                FindModelNodes(child, parentName, nodes, depth + 1);
            }
        }
    }
}
