using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Application = Autodesk.Navisworks.Api.Application;

namespace AutomatedClashRunner.Services
{
    public static class ClashDistillerService
    {
        public static void ReRunTests(IEnumerable<ClashTest> tests)
        {
            var doc = Application.ActiveDocument;
            var clashData = doc.GetClash().TestsData;

            foreach (var test in tests)
            {
                clashData.TestsRunTest(test);
            }
        }

        public static int GroupByElement(IEnumerable<ClashTest> tests)
        {
            int groupsCreated = 0;
            var doc = Application.ActiveDocument;
            var clashData = doc.GetClash().TestsData;

            foreach (var test in tests)
            {
                // 1. Gather all ungrouped results
                var rawResults = test.Children.OfType<ClashResult>().ToList();
                if (rawResults.Count == 0) continue;

                // 2. Group them by their Item1 (Selection A) Top-most element
                var groups = new Dictionary<ModelItem, List<ClashResult>>();

                foreach (var res in rawResults)
                {
                    if (res.Item1 == null) continue;
                    
                    // We take the highest level ancestor or the item itself
                    var masterElement = res.Item1.AncestorsAndSelf.FirstOrDefault(x => x.PropertyCategories.FindPropertyByDisplayName(""Item"", ""Name"") != null) ?? res.Item1;

                    if (!groups.ContainsKey(masterElement))
                    {
                        groups[masterElement] = new List<ClashResult>();
                    }
                    groups[masterElement].Add(res);
                }

                // 3. Create groups and move them
                int groupIndex = 1;
                foreach (var kvp in groups)
                {
                    var itemsInGroup = kvp.Value;
                    if (itemsInGroup.Count == 0) continue;

                    string groupName = $""{test.DisplayName}-{groupIndex:D3}"";
                    var newGroup = new ClashResultGroup { DisplayName = groupName };
                    
                    // Add the empty group to the test
                    clashData.TestsAddCopy(test, newGroup);
                    
                    // The added group is now the LAST child of the test
                    var addedGroup = test.Children.Last() as ClashResultGroup;
                    
                    if (addedGroup != null)
                    {
                        // Move all raw results into the added group
                        foreach (var res in itemsInGroup)
                        {
                            int sourceIndex = test.Children.IndexOf(res);
                            if (sourceIndex >= 0)
                            {
                                clashData.TestsMove(test, sourceIndex, addedGroup, addedGroup.Children.Count);
                            }
                        }
                        groupsCreated++;
                        groupIndex++;
                    }
                }
            }
            return groupsCreated;
        }

        public static int ExportReviewedViewpoints(IEnumerable<ClashTest> tests)
        {
            int viewpointsCreated = 0;
            var doc = Application.ActiveDocument;
            var clashData = doc.GetClash().TestsData;
            var savedViewpoints = doc.SavedViewpoints;

            foreach (var test in tests)
            {
                var reviewedGroups = test.Children.OfType<ClashResultGroup>().Where(g => g.Status == ClashResultStatus.Reviewed).ToList();
                if (reviewedGroups.Count == 0) continue;

                // Create folder for the test
                var folder = new FolderItem { DisplayName = test.DisplayName };
                savedViewpoints.AddCopy(folder);
                
                // Get the actual inserted folder (last in root)
                var actualFolder = savedViewpoints.RootItem.Children.Last() as FolderItem;
                if (actualFolder == null) continue;

                foreach (var group in reviewedGroups)
                {
                    if (group.RepresentativeResult == null) continue;
                    
                    var vp = clashData.TestsViewpointForResult(group.RepresentativeResult);
                    if (vp != null)
                    {
                        var svp = new SavedViewpoint(vp) { DisplayName = group.DisplayName };
                        savedViewpoints.AddCopy(actualFolder, svp);
                        viewpointsCreated++;
                    }
                }
            }

            return viewpointsCreated;
        }
    }
}
