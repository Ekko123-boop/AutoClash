using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Services.Interfaces;

namespace AutomatedClashRunner.Services
{
    public class ClashDistillerService : IClashDistillerService
    {
        private readonly ILoggerService _logger;

        public static ClashDistillerService Instance { get; } = new ClashDistillerService(LoggerService.Instance);

        public ClashDistillerService(ILoggerService logger)
        {
            _logger = logger ?? LoggerService.Instance;
        }

        public void ReRunTests(Document doc, IEnumerable<ClashTest> tests)
        {
            if (doc == null || tests == null) return;

            var documentClash = doc.GetClash();
            if (documentClash == null) return;

            var clashData = documentClash.TestsData;
            foreach (var test in tests)
            {
                try
                {
                    clashData.TestsRunTest(test);
                    _logger.Log($"Re-ran clash test: {test.DisplayName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to re-run test '{test.DisplayName}'", ex);
                }
            }
        }

        public int GroupByElement(Document doc, IEnumerable<ClashTest> tests, double maxProximityFt)
        {
            int groupsCreated = 0;
            if (doc == null || tests == null) return groupsCreated;

            var documentClash = doc.GetClash();
            if (documentClash == null) return groupsCreated;

            var clashData = documentClash.TestsData;

            // Navisworks internal coordinate system is ALWAYS in meters.
            // 1 foot = 0.3048 meters.
            double maxDistMeters = maxProximityFt * 0.3048;

            foreach (var test in tests)
            {
                try
                {
                    var rawResults = test.Children.OfType<ClashResult>().ToList();
                    if (rawResults.Count == 0) continue;

                    // Group by top-level named ancestor in Selection A
                    var elementGroups = new Dictionary<ModelItem, List<ClashResult>>();

                    foreach (var res in rawResults)
                    {
                        if (res.Item1 == null) continue;

                        var masterElement = res.Item1.AncestorsAndSelf
                            .FirstOrDefault(x => x.PropertyCategories.FindPropertyByDisplayName("Item", "Name") != null) 
                            ?? res.Item1;

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

                        // Spatial clustering by distance threshold
                        var clusters = new List<List<ClashResult>>();
                        foreach (var res in items)
                        {
                            bool added = false;
                            foreach (var cluster in clusters)
                            {
                                if (cluster.Any(c => Distance(c.Center, res.Center) <= maxDistMeters))
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

                        foreach (var cluster in clusters)
                        {
                            string groupName = $"{test.DisplayName}-{groupIndex:D3}";
                            var newGroup = new ClashResultGroup { DisplayName = groupName };

                            clashData.TestsAddCopy(test, newGroup);
                            var addedGroup = test.Children.LastOrDefault() as ClashResultGroup;

                            if (addedGroup != null)
                            {
                                // Move in reverse index order to avoid index shifts
                                var moves = cluster
                                    .Select(res => new { Result = res, Index = test.Children.IndexOf(res) })
                                    .Where(x => x.Index >= 0)
                                    .OrderByDescending(x => x.Index)
                                    .ToList();

                                foreach (var m in moves)
                                {
                                    int currentIndex = test.Children.IndexOf(m.Result);
                                    if (currentIndex >= 0)
                                    {
                                        clashData.TestsMove(test, currentIndex, addedGroup, addedGroup.Children.Count);
                                    }
                                }

                                groupsCreated++;
                                groupIndex++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error grouping clashes for test '{test.DisplayName}'", ex);
                }
            }

            return groupsCreated;
        }

        private static double Distance(Point3D p1, Point3D p2)
        {
            if (p1 == null || p2 == null) return double.MaxValue;
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2));
        }

        public int ExportReviewedViewpoints(Document doc, IEnumerable<ClashTest> tests)
        {
            int viewpointsCreated = 0;
            if (doc == null || tests == null) return viewpointsCreated;

            var documentClash = doc.GetClash();
            if (documentClash == null) return viewpointsCreated;

            var clashData = documentClash.TestsData;
            var savedViewpoints = doc.SavedViewpoints;

            foreach (var test in tests)
            {
                try
                {
                    var reviewedGroups = test.Children.OfType<ClashResultGroup>()
                        .Where(g => g.Status == ClashResultStatus.Reviewed)
                        .ToList();

                    if (reviewedGroups.Count == 0) continue;

                    // Create folder for the clash test
                    var folder = new FolderItem { DisplayName = test.DisplayName };
                    savedViewpoints.AddCopy(folder);

                    var actualFolder = savedViewpoints.RootItem.Children.LastOrDefault() as FolderItem;
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

                    _logger.Log($"Exported viewpoints for reviewed groups in test: {test.DisplayName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error exporting viewpoints for test '{test.DisplayName}'", ex);
                }
            }

            return viewpointsCreated;
        }
    }
}
