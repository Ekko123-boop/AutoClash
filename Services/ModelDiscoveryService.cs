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
                FindModelNodes(model.RootItem, "Document Root", nodes, 0);
            }
            return nodes;
        }

        private static void FindModelNodes(ModelItem item, string parentName, List<ModelSourceNode> nodes, int depth)
        {
            if (item == null || depth > 10) return;

            string name = item.DisplayName;

            // We only care about NWCs as requested by the user
            if (!string.IsNullOrEmpty(name) && name.EndsWith(".nwc", System.StringComparison.OrdinalIgnoreCase))
            {
                nodes.Add(new ModelSourceNode
                {
                    DisplayName = name,
                    SourceFilePath = name, // Storing Name here so SearchSetService can use it
                    IsDirectNwc = true,
                    ParentContainerName = parentName,
                    OriginalModelItem = item,
                    IsSelectable = true
                });

                // Do not recurse into NWC geometry
                return;
            }

            // Recurse deeper to find nested NWCs
            string newParent = !string.IsNullOrEmpty(name) ? name : parentName;
            foreach (var child in item.Children)
            {
                FindModelNodes(child, newParent, nodes, depth + 1);
            }
        }
    }
}
