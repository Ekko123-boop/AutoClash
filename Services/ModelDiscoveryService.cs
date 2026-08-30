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
    }
}
