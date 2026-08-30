using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Models;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class ClashExecutionService : IClashExecutionService
    {
        private readonly ISearchSetService _searchSets;
        private readonly INamingService _naming;
        private readonly ILoggerService _logger;

        public static ClashExecutionService Instance { get; } = new ClashExecutionService(
            SearchSetService.Instance, NamingService.Instance, LoggerService.Instance);

        public ClashExecutionService(ISearchSetService searchSets, INamingService naming, ILoggerService logger)
        {
            _searchSets = searchSets ?? SearchSetService.Instance;
            _naming = naming ?? NamingService.Instance;
            _logger = logger ?? LoggerService.Instance;
        }

        public ExecutionResult RunClashMatrix(
            Document doc,
            List<SearchSetNode> manualSets,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = 0.0,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (doc == null || doc.IsClear)
            {
                result.FailedTests.Add("Active document is not available or is empty.");
                return result;
            }

            var documentClash = doc.GetClash();
            if (documentClash == null)
            {
                result.FailedTests.Add("Clash Detective is not available in this Navisworks edition.");
                return result;
            }

            var clashTests = documentClash.TestsData;
            var testsFolder = _searchSets.EnsureTestsFolder(doc);

            // Step 1: Generate or retrieve static selection sets for the selected models
            var modelSetMap = new Dictionary<ModelSourceNode, SelectionSet>();
            int modelIdx = 0;
            foreach (var model in models)
            {
                modelIdx++;
                progressCallback?.Invoke($"Generating search set for {model.DisplayName}...", modelIdx, models.Count);

                var set = _searchSets.GenerateModelSearchSet(doc, model, testsFolder, result);
                if (set != null)
                {
                    modelSetMap[model] = set;
                }
            }

            int totalCombinations = manualSets.Count * models.Count;
            int currentCombination = 0;
            bool anySucceeded = false;

            using (var trans = doc.BeginTransaction("Automated Clash Matrix Run"))
            {
                foreach (var manualSet in manualSets)
                {
                    foreach (var model in models)
                    {
                        currentCombination++;
                        if (!modelSetMap.ContainsKey(model)) continue;

                        var generatedSet = modelSetMap[model];
                        string testName = _naming.GetClashTestName(model.DisplayName, manualSet.OriginalSavedItem.DisplayName);

                        progressCallback?.Invoke($"Running test: {testName} ({currentCombination}/{totalCombinations})", currentCombination, totalCombinations);

                        // Check if test already exists
                        bool exists = clashTests.Tests.Any(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase));
                        if (exists)
                        {
                            result.SkippedTests.Add(testName);
                            _logger.Log($"Skipped existing clash test: {testName}");
                            continue;
                        }

                        try
                        {
                            var test = new ClashTest
                            {
                                DisplayName = testName,
                                TestType = testType,
                                Tolerance = tolerance
                            };

                            // Selection A: Search/Selection Set
                            var sourceA = doc.SelectionSets.CreateSelectionSource(manualSet.OriginalSavedItem);
                            test.SelectionA.Selection.SelectionSources.Add(sourceA);

                            // Selection B: Generated Model Set (Dynamic link)
                            var sourceB = doc.SelectionSets.CreateSelectionSource(generatedSet);
                            test.SelectionB.Selection.SelectionSources.Add(sourceB);

                            clashTests.TestsAddCopy(test);
                            var addedTest = clashTests.Tests.LastOrDefault() as ClashTest;

                            if (addedTest != null)
                            {
                                clashTests.TestsRunTest(addedTest);
                                result.SuccessfulTests.Add(testName);
                                anySucceeded = true;
                                _logger.Log($"Successfully executed clash test: {testName}");
                            }
                            else
                            {
                                result.FailedTests.Add($"{testName}: Failed to register test copy in Clash Detective.");
                                _logger.LogWarning($"Failed to register test copy for: {testName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FailedTests.Add($"{testName}: {ex.Message}");
                            _logger.LogError($"Error executing clash test '{testName}'", ex);
                        }
                    }
                }

                if (anySucceeded)
                {
                    trans.Commit();
                }
            }

            return result;
        }
    }
}
