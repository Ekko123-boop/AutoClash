using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Common;
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

        private struct SingleClashTestConfig
        {
            public string TestName;
            public ClashTestType TestType;
            public double Tolerance;
            public SelectionSource SelectionSourceA;
            public ModelItemCollection ItemsB;
            public Action<ClashTest> ConfigureRules;
            public string LogDetail;
        }

        private bool TryInitializeClashExecution(
            Document doc,
            ExecutionResult result,
            out DocumentClashTests clashTests,
            out HashSet<string> existingTestNames)
        {
            clashTests = null;
            existingTestNames = null;

            if (doc == null || doc.IsClear)
            {
                result.FailedTests.Add("Active document is not available or is empty.");
                return false;
            }

            var documentClash = doc.GetClash();
            if (documentClash == null)
            {
                result.FailedTests.Add("Clash Detective is not available in this Navisworks edition.");
                return false;
            }

            clashTests = documentClash.TestsData;
            existingTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (clashTests?.Tests != null)
            {
                foreach (SavedItem item in clashTests.Tests)
                {
                    if (!string.IsNullOrEmpty(item?.DisplayName))
                    {
                        existingTestNames.Add(item.DisplayName);
                    }
                }
            }

            return true;
        }

        private void ExecuteSingleClashTest(
            Document doc,
            DocumentClashTests clashTests,
            HashSet<string> existingTestNames,
            SingleClashTestConfig config,
            ExecutionResult result)
        {
            // O(1) Check if test already exists in Clash Detective
            if (existingTestNames.Contains(config.TestName))
            {
                result.SkippedTests.Add(config.TestName);
                _logger.Log($"Skipped existing clash test: {config.TestName}");
                return;
            }

            try
            {
                var test = new ClashTest
                {
                    DisplayName = config.TestName,
                    TestType = config.TestType,
                    Tolerance = config.Tolerance
                };

                // Apply rules if configured (e.g. same file rule)
                config.ConfigureRules?.Invoke(test);

                // Selection A: Search/Selection Set (ISS-041: strictly via SelectionSources.Add)
                if (config.SelectionSourceA != null)
                {
                    test.SelectionA.Selection.SelectionSources.Add(config.SelectionSourceA);
                }

                // Selection B: Direct Standard NWC Model File(s) (ISS-041: CopyFrom)
                if (config.ItemsB != null && config.ItemsB.Count > 0)
                {
                    test.SelectionB.Selection.CopyFrom(config.ItemsB);
                }

                clashTests.TestsAddCopy(test);
                existingTestNames.Add(config.TestName);

                var addedTest = clashTests.Tests.OfType<ClashTest>()
                    .LastOrDefault(t => string.Equals(t.DisplayName, config.TestName, StringComparison.OrdinalIgnoreCase))
                    ?? clashTests.Tests.LastOrDefault() as ClashTest;

                if (addedTest != null)
                {
                    // Re-apply rules on added instance
                    config.ConfigureRules?.Invoke(addedTest);

                    clashTests.TestsRunTest(addedTest);
                    System.Threading.Thread.Yield(); // Non-blocking thread yield instead of Thread.Sleep(30)
                    result.SuccessfulTests.Add(config.TestName);
                    string detail = !string.IsNullOrEmpty(config.LogDetail) ? $" {config.LogDetail}" : string.Empty;
                    _logger.Log($"Successfully executed clash test: {config.TestName}{detail}");
                }
                else
                {
                    result.FailedTests.Add($"{config.TestName}: Failed to register test copy in Clash Detective.");
                    _logger.LogWarning($"Failed to register test copy for: {config.TestName}");
                }
            }
            catch (Exception ex)
            {
                result.FailedTests.Add($"{config.TestName}: {ex.Message}");
                _logger.LogError($"Error executing clash test '{config.TestName}'", ex);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunClashMatrix(
            Document doc,
            List<SearchSetNode> manualSets,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = AppConstants.DefaultToleranceMeters,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!TryInitializeClashExecution(doc, result, out var clashTests, out var existingTestNames))
                return result;

            int totalCombinations = manualSets.Count * models.Count;
            int currentCombination = 0;

            foreach (var manualSet in manualSets)
            {
                if (manualSet?.OriginalSavedItem == null) continue;
                var sourceA = doc.SelectionSets.CreateSelectionSource(manualSet.OriginalSavedItem);
                if (sourceA == null) continue;

                foreach (var model in models)
                {
                    currentCombination++;
                    if (model?.OriginalModelItem == null) continue;

                    string testName = _naming.GetClashTestName(model.DisplayName, manualSet.OriginalSavedItem.DisplayName);
                    progressCallback?.Invoke($"Running test: {testName} ({currentCombination}/{totalCombinations})", currentCombination, totalCombinations);

                    var config = new SingleClashTestConfig
                    {
                        TestName = testName,
                        TestType = testType,
                        Tolerance = tolerance,
                        SelectionSourceA = sourceA,
                        ItemsB = new ModelItemCollection { model.OriginalModelItem },
                        LogDetail = $"[Set: {manualSet.DisplayName} vs Model: {model.DisplayName}]"
                    };

                    ExecuteSingleClashTest(doc, clashTests, existingTestNames, config, result);
                }
            }

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunToolsTest(
            Document doc,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = AppConstants.DefaultToleranceMeters,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!TryInitializeClashExecution(doc, result, out var clashTests, out var existingTestNames))
                return result;

            var allSets = _searchSets.GetManualSearchSets(doc)
                .Where(s => !s.IsFolder && s.OriginalSavedItem != null)
                .ToList();

            var setsByName = new Dictionary<string, SearchSetNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allSets)
            {
                string trimmed = s.DisplayName?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !setsByName.ContainsKey(trimmed))
                {
                    setsByName[trimmed] = s;
                }
            }

            int total = models.Count;
            int current = 0;

            foreach (var model in models)
            {
                current++;
                if (model?.OriginalModelItem == null) continue;

                string rawName = model.DisplayName;
                string targetSetCode = _naming.GetTrimmedModelCode(rawName);
                string testName = _naming.GetToolsTestClashName(rawName);

                progressCallback?.Invoke($"Running tools test: {testName} ({current}/{total})", current, total);

                SearchSetNode matchedSet;
                if (!setsByName.TryGetValue(targetSetCode, out matchedSet))
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

                var sourceA = doc.SelectionSets.CreateSelectionSource(matchedSet.OriginalSavedItem);
                var config = new SingleClashTestConfig
                {
                    TestName = testName,
                    TestType = testType,
                    Tolerance = tolerance,
                    SelectionSourceA = sourceA,
                    ItemsB = new ModelItemCollection { model.OriginalModelItem },
                    LogDetail = $"[Set: {matchedSet.DisplayName} vs Model: {rawName}]"
                };

                ExecuteSingleClashTest(doc, clashTests, existingTestNames, config, result);
            }

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunBaseBuildTest(
            Document doc,
            List<ModelSourceNode> models,
            ClashTestType testType = ClashTestType.Clearance,
            double tolerance = AppConstants.DefaultToleranceMeters,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!TryInitializeClashExecution(doc, result, out var clashTests, out var existingTestNames))
                return result;

            var allSets = _searchSets.GetManualSearchSets(doc)
                .Where(s => !s.IsFolder && s.OriginalSavedItem != null)
                .ToList();

            var baseBuildSet = allSets.FirstOrDefault(s =>
                string.Equals(s.DisplayName?.Trim(), AppConstants.BaseBuildSetName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.DisplayName?.Trim(), AppConstants.BaseBuildCompactSetName, StringComparison.OrdinalIgnoreCase))
                ?? allSets.FirstOrDefault(s =>
                    s.DisplayName != null && (
                        s.DisplayName.Trim().EndsWith(AppConstants.BaseBuildSetName, StringComparison.OrdinalIgnoreCase) ||
                        s.DisplayName.Trim().EndsWith(AppConstants.BaseBuildCompactSetName, StringComparison.OrdinalIgnoreCase)));

            if (baseBuildSet == null || baseBuildSet.OriginalSavedItem == null)
            {
                string failMsg = "No 'Base Build' (or 'BaseBuild') Selection Set found in the document.";
                result.FailedTests.Add(failMsg);
                _logger.LogWarning(failMsg);
                return result;
            }

            var sourceA = doc.SelectionSets.CreateSelectionSource(baseBuildSet.OriginalSavedItem);
            int total = models.Count;
            int current = 0;

            foreach (var model in models)
            {
                current++;
                if (model?.OriginalModelItem == null) continue;

                string rawName = model.DisplayName;
                string testName = _naming.GetBaseBuildClashName(rawName);

                progressCallback?.Invoke($"Running base build test: {testName} ({current}/{total})", current, total);

                var config = new SingleClashTestConfig
                {
                    TestName = testName,
                    TestType = testType,
                    Tolerance = tolerance,
                    SelectionSourceA = sourceA,
                    ItemsB = new ModelItemCollection { model.OriginalModelItem },
                    LogDetail = $"[Base Build vs Model: {rawName}]"
                };

                ExecuteSingleClashTest(doc, clashTests, existingTestNames, config, result);
            }

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunConstructabilityTest(
            Document doc,
            List<ModelSourceNode> models,
            double clearanceTolerance = AppConstants.DefaultConstructabilityToleranceMeters,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();
            if (!TryInitializeClashExecution(doc, result, out var clashTests, out var existingTestNames))
                return result;

            if (models == null || models.Count == 0)
            {
                result.FailedTests.Add("No models selected for constructability test.");
                return result;
            }

            progressCallback?.Invoke("Resolving POC Elements Search Set...", 1, 10);

            var pocSet = _searchSets.GetOrCreatePocSearchSet(doc, result);
            if (pocSet == null)
            {
                string failMsg = "No elements containing 'POC' in their name were found in the document.";
                result.FailedTests.Add(failMsg);
                _logger.LogWarning(failMsg);
                return result;
            }

            string testName = _naming.GetConstructabilityClashName(models);
            progressCallback?.Invoke($"Configuring test: {testName}...", 4, 10);

            var sourceA = doc.SelectionSets.CreateSelectionSource(pocSet);
            var itemsB = new ModelItemCollection();
            foreach (var m in models)
            {
                if (m?.OriginalModelItem != null)
                {
                    itemsB.Add(m.OriginalModelItem);
                }
            }

            progressCallback?.Invoke($"Registering {testName} in Clash Detective...", 7, 10);

            var config = new SingleClashTestConfig
            {
                TestName = testName,
                TestType = ClashTestType.Clearance,
                Tolerance = clearanceTolerance,
                SelectionSourceA = sourceA,
                ItemsB = itemsB,
                ConfigureRules = EnableSameFileRule,
                LogDetail = $"[POC vs {models.Count} Models, Clearance: {clearanceTolerance:F4}m]"
            };

            ExecuteSingleClashTest(doc, clashTests, existingTestNames, config, result);

            progressCallback?.Invoke($"Completed {testName}", 10, 10);
            return result;
        }

        private void EnableSameFileRule(ClashTest clashTest)
        {
            if (clashTest?.IgnoreRules == null) return;
            try
            {
                foreach (Rule rule in clashTest.IgnoreRules)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.DisplayName) &&
                        rule.DisplayName.IndexOf("same file", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rule.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not set 'same file' ignore rule: {ex.Message}");
            }
        }
    }
}
