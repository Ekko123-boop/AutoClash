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
            if (!LicenseService.QuickValidate())
            {
                result.FailedTests.Add("License authorization expired or invalidated. Please connect to internet to refresh.");
                return result;
            }

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
                            var addedTest = clashTests.Tests.OfType<ClashTest>()
                                .FirstOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                                ?? clashTests.Tests.LastOrDefault() as ClashTest;

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

        public ExecutionResult RunToolsTest(
            Document doc,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = 0.0,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!LicenseService.QuickValidate())
            {
                result.FailedTests.Add("Cypher Tools execution disabled by administrator.");
                return result;
            }

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
            var allSets = _searchSets.GetManualSearchSets(doc)
                .Where(s => !s.IsFolder && s.OriginalSavedItem != null)
                .ToList();

            int total = models.Count;
            int current = 0;
            bool anySucceeded = false;

            using (var trans = doc.BeginTransaction("Automated Tools Clash Test Run"))
            {
                foreach (var model in models)
                {
                    current++;
                    if (model?.OriginalModelItem == null) continue;

                    string rawName = model.DisplayName;
                    string targetSetCode = _naming.GetTrimmedModelCode(rawName);
                    string testName = _naming.GetToolsTestClashName(rawName);

                    progressCallback?.Invoke($"Running tools test: {testName} ({current}/{total})", current, total);

                    // 1. Find corresponding selection set (matching targetSetCode, case-insensitive)
                    var matchedSet = allSets.FirstOrDefault(s => 
                        string.Equals(s.DisplayName?.Trim(), targetSetCode, StringComparison.OrdinalIgnoreCase));

                    // Secondary fallback: check if set name ends with target code
                    if (matchedSet == null)
                    {
                        matchedSet = allSets.FirstOrDefault(s => 
                            s.DisplayName != null && s.DisplayName.Trim().EndsWith(targetSetCode, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchedSet == null || matchedSet.OriginalSavedItem == null)
                    {
                        string failMsg = $"{rawName}: No matching Selection Set '{targetSetCode}' found in document.";
                        result.FailedTests.Add(failMsg);
                        _logger.LogWarning(failMsg);
                        continue;
                    }

                    // 2. Check if test already exists in Clash Detective
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

                        // Selection A: Corresponding Selection / Search Set
                        var sourceA = doc.SelectionSets.CreateSelectionSource(matchedSet.OriginalSavedItem);
                        test.SelectionA.Selection.SelectionSources.Add(sourceA);

                        // Selection B: Direct Selected NWC Model Node
                        var itemsB = new ModelItemCollection { model.OriginalModelItem };
                        test.SelectionB.Selection.CopyFrom(itemsB);

                        clashTests.TestsAddCopy(test);
                        var addedTest = clashTests.Tests.OfType<ClashTest>()
                            .FirstOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                            ?? clashTests.Tests.LastOrDefault() as ClashTest;

                        if (addedTest != null)
                        {
                            clashTests.TestsRunTest(addedTest);
                            result.SuccessfulTests.Add(testName);
                            anySucceeded = true;
                            _logger.Log($"Successfully executed tools clash test: {testName} [Set: {matchedSet.DisplayName} vs Model: {rawName}]");
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
                        _logger.LogError($"Error executing tools clash test '{testName}'", ex);
                    }
                }

                if (anySucceeded)
                {
                    trans.Commit();
                }
            }

            return result;
        }

        public ExecutionResult RunBaseBuildTest(
            Document doc,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = 0.0,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!LicenseService.QuickValidate())
            {
                result.FailedTests.Add("Cypher Tools execution disabled by administrator.");
                return result;
            }

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
            var allSets = _searchSets.GetManualSearchSets(doc)
                .Where(s => !s.IsFolder && s.OriginalSavedItem != null)
                .ToList();

            // Find the "Base Build" selection/search set
            var baseBuildSet = allSets.FirstOrDefault(s =>
                string.Equals(s.DisplayName?.Trim(), "Base Build", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.DisplayName?.Trim(), "BaseBuild", StringComparison.OrdinalIgnoreCase));

            // Secondary fallback: ends with "Base Build" or contains "Base Build"
            if (baseBuildSet == null)
            {
                baseBuildSet = allSets.FirstOrDefault(s =>
                    s.DisplayName != null && (
                        s.DisplayName.Trim().EndsWith("Base Build", StringComparison.OrdinalIgnoreCase) ||
                        s.DisplayName.Trim().EndsWith("BaseBuild", StringComparison.OrdinalIgnoreCase)));
            }

            if (baseBuildSet == null || baseBuildSet.OriginalSavedItem == null)
            {
                string failMsg = "No 'Base Build' (or 'BaseBuild') Selection Set found in the document.";
                result.FailedTests.Add(failMsg);
                _logger.LogWarning(failMsg);
                return result;
            }

            int total = models.Count;
            int current = 0;
            bool anySucceeded = false;

            using (var trans = doc.BeginTransaction("Automated Base Build Clash Test Run"))
            {
                foreach (var model in models)
                {
                    current++;
                    if (model?.OriginalModelItem == null) continue;

                    string rawName = model.DisplayName;
                    string testName = _naming.GetBaseBuildClashName(rawName);

                    progressCallback?.Invoke($"Running base build test: {testName} ({current}/{total})", current, total);

                    // Check if test already exists in Clash Detective
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

                        // Selection A: Base Build Selection / Search Set
                        var sourceA = doc.SelectionSets.CreateSelectionSource(baseBuildSet.OriginalSavedItem);
                        test.SelectionA.Selection.SelectionSources.Add(sourceA);

                        // Selection B: Direct Selected NWC Model Node
                        var itemsB = new ModelItemCollection { model.OriginalModelItem };
                        test.SelectionB.Selection.CopyFrom(itemsB);

                        clashTests.TestsAddCopy(test);
                        var addedTest = clashTests.Tests.OfType<ClashTest>()
                            .FirstOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                            ?? clashTests.Tests.LastOrDefault() as ClashTest;

                        if (addedTest != null)
                        {
                            clashTests.TestsRunTest(addedTest);
                            result.SuccessfulTests.Add(testName);
                            anySucceeded = true;
                            _logger.Log($"Successfully executed base build clash test: {testName} [Base Build vs Model: {rawName}]");
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
                        _logger.LogError($"Error executing base build clash test '{testName}'", ex);
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
