using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface IClashExecutionService
    {
        ExecutionResult RunClashMatrix(
            Document doc,
            List<SearchSetNode> manualSets,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = 0.0,
            Action<string, int, int> progressCallback = null);
    }
}
