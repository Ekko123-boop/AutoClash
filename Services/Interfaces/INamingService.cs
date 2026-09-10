namespace AutomatedClashRunner.Services.Interfaces
{
    public interface INamingService
    {
        string GetTrimmedModelCode(string rawFilename);
        string GetClashTestName(string modelDisplayName, string manualSetName);
        string GetToolsTestClashName(string modelDisplayName);
        string GetBaseBuildClashName(string modelDisplayName);
        string GetConstructabilityClashName(string modelDisplayName);
        string GetConstructabilityClashName(System.Collections.Generic.List<AutomatedClashRunner.Models.ModelSourceNode> models);
    }
}
