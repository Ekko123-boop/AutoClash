using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
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

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
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

            // O(1) Pre-indexed hash set of existing test names across the document
            var existingTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            foreach (var manualSet in manualSets)
            {
                foreach (var model in models)
                {
                    currentCombination++;
                    if (model?.OriginalModelItem == null) continue;

                    string testName = _naming.GetClashTestName(model.DisplayName, manualSet.OriginalSavedItem.DisplayName);

                    progressCallback?.Invoke($"Running test: {testName} ({currentCombination}/{totalCombinations})", currentCombination, totalCombinations);

                    // O(1) Check if test already exists
                    if (existingTestNames.Contains(testName))
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
                        if (sourceA != null)
                        {
                            test.SelectionA.Selection.SelectionSources.Add(sourceA);
                        }

                        // Selection B: Direct Standard NWC Model File
                        var itemsB = new ModelItemCollection { model.OriginalModelItem };
                        test.SelectionB.Selection.CopyFrom(itemsB);

                        clashTests.TestsAddCopy(test);
                        existingTestNames.Add(testName);

                        var addedTest = clashTests.Tests.OfType<ClashTest>()
                            .LastOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                            ?? clashTests.Tests.LastOrDefault() as ClashTest;

                        if (addedTest != null)
                        {
                            clashTests.TestsRunTest(addedTest);
                            System.Threading.Thread.Sleep(30);
                            result.SuccessfulTests.Add(testName);
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

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunToolsTest(
            Document doc,
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
            var allSets = _searchSets.GetManualSearchSets(doc)
                .Where(s => !s.IsFolder && s.OriginalSavedItem != null)
                .ToList();

            // Index search sets by trimmed name for O(1) resolution
            var setsByName = new Dictionary<string, SearchSetNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allSets)
            {
                string trimmed = s.DisplayName?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !setsByName.ContainsKey(trimmed))
                {
                    setsByName[trimmed] = s;
                }
            }

            // O(1) Pre-indexed hash set of existing test names across the document
            var existingTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                // 1. Find corresponding selection set (matching targetSetCode, case-insensitive)
                SearchSetNode matchedSet;
                if (!setsByName.TryGetValue(targetSetCode, out matchedSet))
                {
                    // Secondary fallback: check if set name ends with target code
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

                // 2. Check if test already exists in Clash Detective (O(1) lookup)
                if (existingTestNames.Contains(testName))
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
                    if (sourceA != null)
                    {
                        test.SelectionA.Selection.SelectionSources.Add(sourceA);
                    }

                    // Selection B: Direct Selected NWC Model Node
                    var itemsB = new ModelItemCollection { model.OriginalModelItem };
                    test.SelectionB.Selection.CopyFrom(itemsB);

                    clashTests.TestsAddCopy(test);
                    existingTestNames.Add(testName);

                    var addedTest = clashTests.Tests.OfType<ClashTest>()
                        .LastOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                        ?? clashTests.Tests.LastOrDefault() as ClashTest;

                    if (addedTest != null)
                    {
                        clashTests.TestsRunTest(addedTest);
                        System.Threading.Thread.Sleep(30);
                        result.SuccessfulTests.Add(testName);
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

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunBaseBuildTest(
            Document doc,
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

            // O(1) Pre-indexed hash set of existing test names across the document
            var existingTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            int total = models.Count;
            int current = 0;

            foreach (var model in models)
            {
                current++;
                if (model?.OriginalModelItem == null) continue;

                string rawName = model.DisplayName;
                string testName = _naming.GetBaseBuildClashName(rawName);

                progressCallback?.Invoke($"Running base build test: {testName} ({current}/{total})", current, total);

                // Check if test already exists in Clash Detective (O(1) lookup)
                if (existingTestNames.Contains(testName))
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
                    if (sourceA != null)
                    {
                        test.SelectionA.Selection.SelectionSources.Add(sourceA);
                    }

                    // Selection B: Direct Selected NWC Model Node
                    var itemsB = new ModelItemCollection { model.OriginalModelItem };
                    test.SelectionB.Selection.CopyFrom(itemsB);

                    clashTests.TestsAddCopy(test);
                    existingTestNames.Add(testName);

                    var addedTest = clashTests.Tests.OfType<ClashTest>()
                        .LastOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                        ?? clashTests.Tests.LastOrDefault() as ClashTest;

                    if (addedTest != null)
                    {
                        clashTests.TestsRunTest(addedTest);
                        System.Threading.Thread.Sleep(30);
                        result.SuccessfulTests.Add(testName);
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

            return result;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public ExecutionResult RunConstructabilityTest(
            Document doc,
            List<ModelSourceNode> models,
            double clearanceTolerance = 0.3048,
            Action<string, int, int> progressCallback = null)
        {
            var result = new ExecutionResult();

            if (doc == null || doc.IsClear)
            {
                result.FailedTests.Add("Active document is not available or is empty.");
                return result;
            }

            if (models == null || models.Count == 0)
            {
                result.FailedTests.Add("No models selected for constructability test.");
                return result;
            }

            var documentClash = doc.GetClash();
            if (documentClash == null)
            {
                result.FailedTests.Add("Clash Detective is not available in this Navisworks edition.");
                return result;
            }

            progressCallback?.Invoke("Resolving POC Elements Search Set...", 1, 10);

            // 1. Auto-generate or get the "POC Elements" Selection Set
            var pocSet = _searchSets.GetOrCreatePocSearchSet(doc, result);
            if (pocSet == null)
            {
                string failMsg = "No elements containing 'POC' in their name were found in the document.";
                result.FailedTests.Add(failMsg);
                _logger.LogWarning(failMsg);
                return result;
            }

            var clashTests = documentClash.TestsData;
            string testName = _naming.GetConstructabilityClashName(models);

            // O(1) Pre-indexed hash set of existing test names across the document
            var existingTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            if (existingTestNames.Contains(testName))
            {
                result.SkippedTests.Add(testName);
                _logger.Log($"Skipped existing constructability clash test: {testName}");
                return result;
            }

            progressCallback?.Invoke($"Configuring test: {testName}...", 4, 10);

            try
            {
                var test = new ClashTest
                {
                    DisplayName = testName,
                    TestType = ClashTestType.Clearance,
                    Tolerance = clearanceTolerance
                };

                // Enable "Ignore items in same file" rule to prevent self-clashes
                EnableSameFileRule(test);

                // Selection A: POC Elements Search Set (strictly using SelectionSources per ISS-041)
                var sourceA = doc.SelectionSets.CreateSelectionSource(pocSet);
                if (sourceA != null)
                {
                    test.SelectionA.Selection.SelectionSources.Add(sourceA);
                }

                // Selection B: All selected models combined
                var itemsB = new ModelItemCollection();
                foreach (var m in models)
                {
                    if (m?.OriginalModelItem != null)
                    {
                        itemsB.Add(m.OriginalModelItem);
                    }
                }
                test.SelectionB.Selection.CopyFrom(itemsB);

                progressCallback?.Invoke($"Registering {testName} in Clash Detective...", 7, 10);

                clashTests.TestsAddCopy(test);
                existingTestNames.Add(testName);

                var addedTest = clashTests.Tests.OfType<ClashTest>()
                    .LastOrDefault(t => string.Equals(t.DisplayName, testName, StringComparison.OrdinalIgnoreCase))
                    ?? clashTests.Tests.LastOrDefault() as ClashTest;

                if (addedTest != null)
                {
                    EnableSameFileRule(addedTest);

                    progressCallback?.Invoke($"Executing {testName} (Clearance: {clearanceTolerance:F4}m)...", 9, 10);
                    clashTests.TestsRunTest(addedTest);
                    System.Threading.Thread.Sleep(30);
                    result.SuccessfulTests.Add(testName);
                    _logger.Log($"Successfully executed constructability clash test: {testName} [POC vs {models.Count} Models, Clearance: {clearanceTolerance:F4}m]");
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
                _logger.LogError($"Error executing constructability clash test '{testName}'", ex);
            }

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
