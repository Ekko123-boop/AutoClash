namespace AutomatedClashRunner.Services.Interfaces
{
    public interface INamingService
    {
        string GetTrimmedModelCode(string rawFilename);
        string GetClashTestName(string modelDisplayName, string manualSetName);
        string GetToolsTestClashName(string modelDisplayName);
    }
}
