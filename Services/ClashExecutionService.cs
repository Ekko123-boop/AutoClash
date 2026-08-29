using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Models;

namespace AutomatedClashRunner.Services
{
    public static class ClashExecutionService
    {
        public static ExecutionResult RunClashMatrix(List<SearchSetNode> manualSets, List<ModelSourceNode> models)
        {
            var result = new ExecutionResult();
            var doc = Application.ActiveDocument;
            
            var documentClash = doc.GetClash();
            if (documentClash == null)
            {
                result.FailedTests.Add("Clash Detective is not available.");
                return result;
            }

            var clashTests = documentClash.TestsData;
            var testsFolder = SearchSetService.EnsureTestsFolder();

            // Prepare generated sets
            var modelSetMap = new Dictionary<ModelSourceNode, SelectionSet>();
            foreach (var model in models)
            {
                var set = SearchSetService.GenerateModelSearchSet(model, testsFolder, result);
                if (set != null)
                {
                    modelSetMap[model] = set;
                }
            }

            using (var trans = doc.BeginTransaction("Automated Clash Run"))
            {
                foreach (var manualSet in manualSets)
                {
                    foreach (var model in models)
                    {
                        if (!modelSetMap.ContainsKey(model)) continue; // Skipped generation

                        var generatedSet = modelSetMap[model];
                        string trimmedCode = NamingService.GetTrimmedModelCode(model.DisplayName);
                        
                        // Prefix logic based on manual set name
                        string testName = trimmedCode;
                        string manualName = manualSet.OriginalSavedItem.DisplayName;
                        if (!manualName.Equals("Base Build", StringComparison.OrdinalIgnoreCase) &&
                            !manualName.Equals("BaseBuild", StringComparison.OrdinalIgnoreCase))
                        {
                            testName = "T-" + trimmedCode;
                        }

                        // Skip existing
                        bool exists = clashTests.Tests.Any(t => t.DisplayName.Equals(testName, StringComparison.OrdinalIgnoreCase));
                        if (exists)
                        {
                            result.SkippedTests.Add(testName);
                            continue;
                        }

                        try
                        {
                            var test = new ClashTest
                            {
                                DisplayName = testName,
                                TestType = ClashTestType.Clearance,
                                Tolerance = 0.0
                            };
                            
                            // Create SelectionSource from the Set
                            var sourceA = doc.SelectionSets.CreateSelectionSource(manualSet.OriginalSavedItem);
                            
                            // Directly add it to the existing SelectionSources collection (avoids 'new SelectionSourceCollection()' AccessViolation)
                            test.SelectionA.Selection.SelectionSources.Add(sourceA);

                            ModelItemCollection itemsB = null;
                            if (generatedSet.HasSearch) itemsB = generatedSet.Search.FindAll(doc, false);
                            else itemsB = generatedSet.ExplicitModelItems;
                            if (itemsB == null) itemsB = new ModelItemCollection();

                            test.SelectionB.Selection.CopyFrom(itemsB);

                            clashTests.TestsAddCopy(test);
                            var addedTest = clashTests.Tests.LastOrDefault() as ClashTest;
                            
                            clashTests.TestsRunTest(addedTest);

                            result.SuccessfulTests.Add(testName);
                        }
                        catch (Exception ex)
                        {
                            result.FailedTests.Add($"{testName}: {ex.Message}");
                        }
                    }
                }
                trans.Commit();
            }

            return result;
        }
    }
}
