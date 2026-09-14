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
        string SanitizeTestDisplayName(string testDisplayName);
        string SanitizeItemName(string rawName);
        string GetGenericClashTestName(string itemAName, string itemBName, string delimiter = "v");
        string FormatGroupName(string testDisplayName, int groupIndex);
        string FormatViewpointName(string testDisplayName, string sourceItemDisplayName, int fallbackIndex = 0);
    }
}
