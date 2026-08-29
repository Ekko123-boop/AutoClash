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

        public static int GroupByElement(IEnumerable<ClashTest> tests, double maxProximityFt)
        {
            int groupsCreated = 0;
            var doc = Application.ActiveDocument;
            var clashData = doc.GetClash().TestsData;

            double conversionFactor = 1.0;
            switch(doc.Units)
            {
                case Units.Feet: conversionFactor = 1.0; break;
                case Units.Meters: conversionFactor = 0.3048; break;
                case Units.Millimeters: conversionFactor = 304.8; break;
                case Units.Centimeters: conversionFactor = 30.48; break;
                case Units.Inches: conversionFactor = 12.0; break;
                default: conversionFactor = 0.3048; break; 
            }
            double maxDistInternal = maxProximityFt * conversionFactor;

            foreach (var test in tests)
            {
                var rawResults = test.Children.OfType<ClashResult>().ToList();
                if (rawResults.Count == 0) continue;

                var elementGroups = new Dictionary<ModelItem, List<ClashResult>>();

                foreach (var res in rawResults)
                {
                    if (res.Item1 == null) continue;
                    
                    var masterElement = res.Item1.AncestorsAndSelf.FirstOrDefault(x => x.PropertyCategories.FindPropertyByDisplayName("Item", "Name") != null) ?? res.Item1;

                    if (!elementGroups.ContainsKey(masterElement))
                    {
                        elementGroups[masterElement] = new List<ClashResult>();
                    }
                    elementGroups[masterElement].Add(res);
                }

                int groupIndex = 1;
                foreach (var kvp in elementGroups)
                {
                    var items = kvp.Value;
                    if (items.Count == 0) continue;

                    var clusters = new List<List<ClashResult>>();
                    foreach(var res in items)
                    {
                        bool added = false;
                        foreach(var cluster in clusters)
                        {
                            if (cluster.Any(c => Distance(c.Center, res.Center) <= maxDistInternal))
                            {
                                cluster.Add(res);
                                added = true;
                                break;
                            }
                        }
                        if (!added)
                        {
                            clusters.Add(new List<ClashResult> { res });
                        }
                    }

                    foreach(var cluster in clusters)
                    {
                        string groupName = $"{test.DisplayName}-{groupIndex:D3}";
                        var newGroup = new ClashResultGroup { DisplayName = groupName };
                        
                        clashData.TestsAddCopy(test, newGroup);
                        var addedGroup = test.Children.Last() as ClashResultGroup;
                        
                        if (addedGroup != null)
                        {
                            foreach (var res in cluster)
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
            }
            return groupsCreated;
        }

        private static double Distance(Point3D p1, Point3D p2)
        {
            if (p1 == null || p2 == null) return double.MaxValue;
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2));
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
