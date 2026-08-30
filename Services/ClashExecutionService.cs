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
                        if (model?.OriginalModelItem == null) continue;

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

                            // Selection B: Direct Standard NWC Model File
                            var itemsB = new ModelItemCollection { model.OriginalModelItem };
                            test.SelectionB.Selection.CopyFrom(itemsB);

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
