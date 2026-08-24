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
                        string testName = NamingService.GetClashTestName(manualSet.OriginalSavedItem.DisplayName, trimmedCode);

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
                            
                            // 0 = false for Ignore settings in older/some APIs, but they are properties
                            // The ClashTest class does not expose Ignore rules directly through simple properties.
                            // To strictly disable them, they are false by default on new tests.

                            // Assign Selections
                            test.SelectionA.Selection.CopyFrom(new ModelItemCollection()); // Clear first
                            test.SelectionB.Selection.CopyFrom(new ModelItemCollection());

                            // We need to use SavedItem selection
                            // Autodesk.Navisworks.Api.Clash doesn't expose easy assign of SearchSets to ClashSelection 
                            // directly via API without using the COM API or selecting and then assigning.
                            // Actually, in .NET we can do:
                            doc.CurrentSelection.Clear();
                            var searchA = ((SelectionSet)manualSet.OriginalSavedItem).Search;
                            var searchB = generatedSet.Search;

                            test.SelectionA.Selection.CopyFrom(searchA.FindAll(doc, false));
                            test.SelectionB.Selection.CopyFrom(searchB.FindAll(doc, false));

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
