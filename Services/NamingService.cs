using System.IO;

namespace AutomatedClashRunner.Services
{
    public static class NamingService
    {
        public static string GetTrimmedModelCode(string rawFilename)
        {
            string name = Path.GetFileNameWithoutExtension(rawFilename);
            var parts = name.Split('-');
            
            if (parts.Length >= 3)
            {
                var innerParts = new string[parts.Length - 2];
                System.Array.Copy(parts, 1, innerParts, 0, innerParts.Length);
                return string.Join("-", innerParts);
            }
            
            return name;
        }

        public static string GetClashTestName(string manualSetName, string trimmedModelCode)
        {
            return $"{manualSetName} vs {trimmedModelCode}";
        }
    }
}
