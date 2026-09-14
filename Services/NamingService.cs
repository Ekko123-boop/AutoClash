using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AutomatedClashRunner.Common;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class NamingService : INamingService
    {
        public static NamingService Instance { get; } = new NamingService();

        public string GetTrimmedModelCode(string rawFilename)
        {
            if (string.IsNullOrWhiteSpace(rawFilename)) return string.Empty;
            string name = Path.GetFileNameWithoutExtension(rawFilename).Trim();
            
            int firstDash = name.IndexOf('-');
            int firstUnderscore = name.IndexOf('_');

            // Find the earliest delimiter if both exist
            int splitIndex = -1;
            if (firstDash >= 0 && firstUnderscore >= 0)
            {
                splitIndex = Math.Min(firstDash, firstUnderscore);
            }
            else if (firstDash >= 0)
            {
                splitIndex = firstDash;
            }
            else if (firstUnderscore >= 0)
            {
                splitIndex = firstUnderscore;
            }

            if (splitIndex >= 0 && splitIndex < name.Length - 1)
            {
                return name.Substring(splitIndex + 1).Trim().TrimEnd('-', '_', ' ');
            }

            return name.TrimEnd('-', '_', ' ');
        }

        public string GetClashTestName(string modelDisplayName, string manualSetName)
        {
            string trimmedCode = GetTrimmedModelCode(modelDisplayName);
            string manualName = manualSetName?.Trim() ?? string.Empty;

            // If manual search set is Base Build (or BaseBuild), test name is the trimmed model code.
            // Otherwise, prepend 'T-'.
            if (manualName.Equals(AppConstants.BaseBuildSetName, StringComparison.OrdinalIgnoreCase) ||
                manualName.Equals(AppConstants.BaseBuildCompactSetName, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedCode;
            }
            else
            {
                return AppConstants.ToolsTestPrefix + trimmedCode;
            }
        }

        public string GetToolsTestClashName(string modelDisplayName)
        {
            string trimmedCode = GetTrimmedModelCode(modelDisplayName);
            if (trimmedCode.StartsWith(AppConstants.ToolsTestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedCode;
            }
            return AppConstants.ToolsTestPrefix + trimmedCode;
        }

        public string GetBaseBuildClashName(string modelDisplayName)
        {
            // Base Build tests use the trimmed model code directly, no T- prefix
            return GetTrimmedModelCode(modelDisplayName);
        }

        public string GetConstructabilityClashName(string modelDisplayName)
        {
            string trimmedCode = GetTrimmedModelCode(modelDisplayName);
            if (trimmedCode.StartsWith(AppConstants.ConstructabilityPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedCode;
            }
            return AppConstants.ConstructabilityPrefix + trimmedCode;
        }

        public string GetConstructabilityClashName(List<ModelSourceNode> models)
        {
            if (models == null || models.Count == 0)
            {
                return AppConstants.ConstructabilityPrefix + "Constructability";
            }

            if (models.Count == 1)
            {
                return GetConstructabilityClashName(models[0].DisplayName);
            }

            // If all selected models share the same parent container (e.g. "MEI" or "MEI.nwd")
            string firstParent = models[0].ParentContainerName;
            if (!string.IsNullOrWhiteSpace(firstParent) &&
                models.All(m => string.Equals(m.ParentContainerName, firstParent, StringComparison.OrdinalIgnoreCase)))
            {
                return GetConstructabilityClashName(firstParent);
            }

            return AppConstants.ConstructabilityPrefix + "Constructability";
        }

        public string SanitizeTestDisplayName(string testDisplayName)
        {
            if (string.IsNullOrWhiteSpace(testDisplayName)) return string.Empty;
            return testDisplayName.Trim().TrimEnd('-', '_', ' ');
        }

        public string SanitizeItemName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
            string clean = rawName.Trim();

            // If it's a full path or folder path (e.g. "Tests/L0-BAE-E" or "Folder\Set"), get the leaf name
            int lastSlash = Math.Max(clean.LastIndexOf('/'), clean.LastIndexOf('\\'));
            if (lastSlash >= 0 && lastSlash < clean.Length - 1)
            {
                clean = clean.Substring(lastSlash + 1).Trim();
            }

            // If it has a model file extension, pass through GetTrimmedModelCode
            if (clean.EndsWith(".nwc", StringComparison.OrdinalIgnoreCase) ||
                clean.EndsWith(".nwd", StringComparison.OrdinalIgnoreCase) ||
                clean.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ||
                clean.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
            {
                return GetTrimmedModelCode(clean);
            }

            return clean.TrimEnd('-', '_', ' ');
        }

        public string GetGenericClashTestName(string itemAName, string itemBName, string delimiter = "v")
        {
            string cleanA = SanitizeItemName(itemAName);
            string cleanB = SanitizeItemName(itemBName);
            string delim = string.IsNullOrWhiteSpace(delimiter) ? "v" : delimiter.Trim();
            return $"{cleanA} {delim} {cleanB}";
        }

        public string FormatGroupName(string testDisplayName, int groupIndex)
        {
            string baseName = SanitizeTestDisplayName(testDisplayName);
            return $"{baseName} {groupIndex}";
        }

        public string FormatViewpointName(string testDisplayName, string sourceItemDisplayName, int fallbackIndex = 0)
        {
            string baseName = SanitizeTestDisplayName(testDisplayName);
            string source = sourceItemDisplayName?.Trim() ?? string.Empty;

            // Extract trailing numeric index (e.g. from "T-EGE-ASP1106-E--004", "EGE-ASP1106-E 4", "Clash 4", "Clash4")
            var match = Regex.Match(source, @"\d+$");
            if (match.Success && int.TryParse(match.Value, out int clashNum))
            {
                return $"{baseName} {clashNum}";
            }

            if (fallbackIndex > 0)
            {
                return $"{baseName} {fallbackIndex}";
            }

            return !string.IsNullOrEmpty(source) ? source : baseName;
        }
    }
}
