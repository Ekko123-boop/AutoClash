using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface ISearchSetService
    {
        List<SearchSetNode> GetManualSearchSets(Document doc);
        FolderItem EnsureTestsFolder(Document doc);
        SelectionSet GenerateModelSearchSet(Document doc, ModelSourceNode modelNode, FolderItem testsFolder, ExecutionResult result);
    }
}
