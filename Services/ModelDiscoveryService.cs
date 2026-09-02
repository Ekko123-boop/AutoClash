using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class ModelDiscoveryService : IModelDiscoveryService
    {
        private const int MaxRecursionDepth = 20;
        private readonly ILoggerService _logger;

        public static ModelDiscoveryService Instance { get; } = new ModelDiscoveryService(LoggerService.Instance);

        public ModelDiscoveryService(ILoggerService logger)
        {
            _logger = logger ?? LoggerService.Instance;
        }

        public List<ModelSourceNode> DiscoverModels(Document doc)
        {
            var nodes = new List<ModelSourceNode>();
            if (doc == null || doc.IsClear) return nodes;

            try
            {
                foreach (var model in doc.Models)
                {
                    FindModelNodes(model.RootItem, "Document Root", nodes, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error discovering models in document", ex);
            }

            return nodes;
        }

        private void FindModelNodes(ModelItem item, string parentName, List<ModelSourceNode> nodes, int depth)
        {
            if (item == null) return;

            if (depth > MaxRecursionDepth)
            {
                _logger.LogWarning($"Max recursion depth reached ({MaxRecursionDepth}) for model item: {item.DisplayName}");
                return;
            }

            string name = item.DisplayName;

            // Direct NWC models
            if (!string.IsNullOrEmpty(name) && name.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase))
            {
                nodes.Add(new ModelSourceNode
                {
                    DisplayName = name,
                    SourceFilePath = name,
                    IsDirectNwc = true,
                    ParentContainerName = parentName,
                    OriginalModelItem = item,
                    IsSelectable = true
                });

                // Do not recurse into NWC inner geometry
                return;
            }

            string newParent = !string.IsNullOrEmpty(name) ? name : parentName;
            foreach (var child in item.Children)
            {
                FindModelNodes(child, newParent, nodes, depth + 1);
            }
        }

        public List<ModelItem> GetSiblingNwcs(Document doc, ModelSourceNode targetNwc)
        {
            var siblings = new List<ModelItem>();
            if (doc == null || targetNwc?.OriginalModelItem == null) return siblings;

            try
            {
                // 1. Walk up to find the enclosing NWD container (or direct parent)
                ModelItem container = null;
                ModelItem current = targetNwc.OriginalModelItem.Parent;
                while (current != null)
                {
                    string dName = current.DisplayName ?? string.Empty;
                    if (dName.EndsWith(".nwd", StringComparison.OrdinalIgnoreCase))
                    {
                        container = current;
                        break;
                    }
                    current = current.Parent;
                }

                // If no .nwd container found in ancestry, use immediate parent
                if (container == null)
                {
                    container = targetNwc.OriginalModelItem.Parent;
                }

                if (container == null)
                {
                    _logger.LogWarning($"No parent container found for model item: {targetNwc.DisplayName}");
                    return siblings;
                }

                // 2. Collect all .nwc items under this container
                var allNwcs = new List<ModelItem>();
                CollectNwcItems(container, allNwcs, 0);

                // 3. Exclude the target NWC itself
                foreach (var item in allNwcs)
                {
                    if (item == targetNwc.OriginalModelItem) continue;
                    if (string.Equals(item.DisplayName, targetNwc.DisplayName, StringComparison.OrdinalIgnoreCase)) continue;
                    siblings.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error finding sibling NWCs for {targetNwc.DisplayName}", ex);
            }

            return siblings;
        }

        private void CollectNwcItems(ModelItem item, List<ModelItem> nwcItems, int depth)
        {
            if (item == null || depth > MaxRecursionDepth) return;

            string name = item.DisplayName;
            if (!string.IsNullOrEmpty(name) && name.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase))
            {
                nwcItems.Add(item);
                return;
            }

            foreach (var child in item.Children)
            {
                CollectNwcItems(child, nwcItems, depth + 1);
            }
        }
    }
}
