using System;
using System.IO;
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
                return name.Substring(splitIndex + 1).Trim();
            }

            return name;
        }

        public string GetClashTestName(string modelDisplayName, string manualSetName)
        {
            string trimmedCode = GetTrimmedModelCode(modelDisplayName);
            string manualName = manualSetName?.Trim() ?? string.Empty;

            // If manual search set is Base Build (or BaseBuild), test name is the trimmed model code.
            // Otherwise, prepend 'T-'.
            if (manualName.Equals("Base Build", StringComparison.OrdinalIgnoreCase) ||
                manualName.Equals("BaseBuild", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedCode;
            }
            else
            {
                return "T-" + trimmedCode;
            }
        }

        public string GetToolsTestClashName(string modelDisplayName)
        {
            string trimmedCode = GetTrimmedModelCode(modelDisplayName);
            if (trimmedCode.StartsWith("T-", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedCode;
            }
            return "T-" + trimmedCode;
        }

        public string GetBaseBuildClashName(string modelDisplayName)
        {
            // Base Build tests use the trimmed model code directly, no T- prefix
            return GetTrimmedModelCode(modelDisplayName);
        }
    }
}
