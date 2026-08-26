using System.IO;

namespace AutomatedClashRunner.Services
{
    public static class NamingService
    {
        public static string GetTrimmedModelCode(string rawFilename)
        {
            string name = Path.GetFileNameWithoutExtension(rawFilename);
            int firstDash = name.IndexOf('-');
            if (firstDash >= 0 && firstDash < name.Length - 1)
            {
                return name.Substring(firstDash + 1);
            }
            return name;
        }

        public static string GetClashTestName(string manualSetName, string trimmedModelCode)
        {
            return $"{manualSetName} vs {trimmedModelCode}";
        }
    }
}
