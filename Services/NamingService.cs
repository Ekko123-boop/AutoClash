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
            
            // Delimiter '-' e.g. "F1-STS-HDLS202-MX" -> "STS-HDLS202-MX"
            int firstDash = name.IndexOf('-');
            if (firstDash >= 0 && firstDash < name.Length - 1)
            {
                return name.Substring(firstDash + 1).Trim();
            }

            // Delimiter '_' e.g. "F1_STS-HDLS202-MX" -> "STS-HDLS202-MX"
            int firstUnderscore = name.IndexOf('_');
            if (firstUnderscore >= 0 && firstUnderscore < name.Length - 1)
            {
                return name.Substring(firstUnderscore + 1).Trim();
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
    }
}
