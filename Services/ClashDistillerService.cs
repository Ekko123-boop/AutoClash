using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using AutomatedClashRunner.Common;
using AutomatedClashRunner.Services.Interfaces;
using AutomatedClashRunner.Utils;

namespace AutomatedClashRunner.Services
{
    public class ClashDistillerService : IClashDistillerService
    {
        private readonly ILoggerService _logger;
        private readonly INamingService _naming;

        public static ClashDistillerService Instance { get; } = new ClashDistillerService(LoggerService.Instance, NamingService.Instance);

        public ClashDistillerService(ILoggerService logger, INamingService naming = null)
        {
            _logger = logger ?? LoggerService.Instance;
            _naming = naming ?? NamingService.Instance;
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

        private struct VoxelCoord : IEquatable<VoxelCoord>
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;

            public VoxelCoord(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(VoxelCoord other) => X == other.X && Y == other.Y && Z == other.Z;

            public override bool Equals(object obj) => obj is VoxelCoord other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + X;
                    hash = hash * 31 + Y;
                    hash = hash * 31 + Z;
                    return hash;
                }
            }
        }

        private static void DoEvents() => DispatcherUtils.DoEvents();

        private static List<List<ClashResult>> ClusterResults(List<ClashResult> items, double maxDistMeters)
        {
            var clusters = new List<List<ClashResult>>();
            if (items == null || items.Count == 0) return clusters;

            if (items.Count == 1 || maxDistMeters <= 0.0001)
            {
                foreach (var item in items)
                {
                    clusters.Add(new List<ClashResult> { item });
                }
                return clusters;
            }

            double maxDistSq = maxDistMeters * maxDistMeters;
            double cellSize = maxDistMeters;

            // Grid mapping voxel cell -> list of clash results in that cell
            var grid = new Dictionary<VoxelCoord, List<ClashResult>>();
            // Maps each clash result to the cluster list it belongs to
            var resultToCluster = new Dictionary<ClashResult, List<ClashResult>>();

            foreach (var res in items)
            {
                var center = res.Center;
                if (center == null)
                {
                    var nullCluster = new List<ClashResult> { res };
                    clusters.Add(nullCluster);
                    resultToCluster[res] = nullCluster;
                    continue;
                }

                int gx = (int)Math.Floor(center.X / cellSize);
                int gy = (int)Math.Floor(center.Y / cellSize);
                int gz = (int)Math.Floor(center.Z / cellSize);

                List<ClashResult> matchedCluster = null;

                // Check 27 neighboring voxel cells [-1, 0, 1]^3
                for (int dx = -1; dx <= 1 && matchedCluster == null; dx++)
                {
                    for (int dy = -1; dy <= 1 && matchedCluster == null; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var neighborCoord = new VoxelCoord(gx + dx, gy + dy, gz + dz);
                            if (grid.TryGetValue(neighborCoord, out var candidates))
                            {
                                foreach (var cand in candidates)
                                {
                                    var c = cand.Center;
                                    if (c == null) continue;

                                    double dX = c.X - center.X;
                                    double dY = c.Y - center.Y;
                                    double dZ = c.Z - center.Z;
                                    double distSq = dX * dX + dY * dY + dZ * dZ;

                                    if (distSq <= maxDistSq)
                                    {
                                        if (resultToCluster.TryGetValue(cand, out var cluster))
                                        {
                                            matchedCluster = cluster;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (matchedCluster != null) break;
                        }
                    }
                }

                if (matchedCluster != null)
                {
                    matchedCluster.Add(res);
                    resultToCluster[res] = matchedCluster;
                }
                else
                {
                    var newCluster = new List<ClashResult> { res };
                    clusters.Add(newCluster);
                    resultToCluster[res] = newCluster;
                }

                var selfCoord = new VoxelCoord(gx, gy, gz);
                if (!grid.TryGetValue(selfCoord, out var cellList))
                {
                    cellList = new List<ClashResult>();
                    grid[selfCoord] = cellList;
                }
                cellList.Add(res);
            }

            return clusters;
        }

        public int GroupByElement(Document doc, IEnumerable<ClashTest> tests, double maxProximityFt, Action<string, int, int> progressCallback = null)
        {
            int groupsCreated = 0;
            if (doc == null || tests == null) return groupsCreated;

            var documentClash = doc.GetClash();
            if (documentClash == null) return groupsCreated;

            var clashData = documentClash.TestsData;

            // Navisworks internal coordinate system is ALWAYS in meters.
            // 1 foot = 0.3048 meters.
            double maxDistMeters = maxProximityFt * AppConstants.MetersPerFoot;

            var testList = tests.ToList();
            int totalTests = testList.Count;
            int testIndex = 0;

            foreach (var test in testList)
            {
                testIndex++;
                try
                {
                    progressCallback?.Invoke(
                        $"Analyzing test {testIndex} of {totalTests}: '{test.DisplayName}'...",
                        testIndex,
                        totalTests);
                    DoEvents();

                    var rawResults = test.Children.OfType<ClashResult>().ToList();
                    if (rawResults.Count == 0) continue;

                    // Group by top-level named ancestor in Selection A with memoization cache
                    var ancestorCache = new Dictionary<ModelItem, ModelItem>();
                    ModelItem GetMasterElement(ModelItem item)
                    {
                        if (item == null) return null;
                        if (ancestorCache.TryGetValue(item, out var cached)) return cached;

                        ModelItem master = null;
                        foreach (var node in item.AncestorsAndSelf)
                        {
                            if (ancestorCache.TryGetValue(node, out var ancCached))
                            {
                                master = ancCached;
                                break;
                            }

                            try
                            {
                                if (node.PropertyCategories.FindPropertyByDisplayName("Item", "Name") != null)
                                {
                                    master = node;
                                    break;
                                }
                            }
                            catch { }
                        }

                        master = master ?? item;
                        ancestorCache[item] = master;
                        return master;
                    }

                    var elementGroups = new Dictionary<ModelItem, List<ClashResult>>();
                    foreach (var res in rawResults)
                    {
                        if (res.Item1 == null) continue;

                        var masterElement = GetMasterElement(res.Item1);
                        if (!elementGroups.TryGetValue(masterElement, out var list))
                        {
                            list = new List<ClashResult>();
                            elementGroups[masterElement] = list;
                        }
                        list.Add(res);
                    }

                    // Compute all spatial clusters across all element groups
                    var allClusters = new List<List<ClashResult>>();
                    foreach (var kvp in elementGroups)
                    {
                        var items = kvp.Value;
                        if (items.Count == 0) continue;

                        var clusters = ClusterResults(items, maxDistMeters);
                        allClusters.AddRange(clusters);
                    }

                    if (allClusters.Count == 0) continue;

                    // Step 1: Pre-create all ClashResultGroup nodes in document
                    int groupIndex = 1;
                    var resToGroup = new Dictionary<ClashResult, ClashResultGroup>();

                    foreach (var cluster in allClusters)
                    {
                        if (cluster.Count == 0) continue;

                        string groupName = _naming.FormatGroupName(test.DisplayName, groupIndex);
                        var newGroup = new ClashResultGroup { DisplayName = groupName };

                        clashData.TestsAddCopy(test, newGroup);
                        var addedGroup = test.Children.LastOrDefault() as ClashResultGroup;

                        if (addedGroup != null)
                        {
                            foreach (var res in cluster)
                            {
                                resToGroup[res] = addedGroup;
                            }
                            groupsCreated++;
                            groupIndex++;
                        }
                    }

                    // Step 2: Move items in reverse index order in a single pass (O(N), ZERO IndexOf scans)
                    int totalMoves = resToGroup.Count;
                    int movedCount = 0;

                    for (int i = test.Children.Count - 1; i >= 0; i--)
                    {
                        if (test.Children[i] is ClashResult res && resToGroup.TryGetValue(res, out var targetGroup))
                        {
                            try
                            {
                                clashData.TestsMove(test, i, targetGroup, 0);
                                movedCount++;

                                if (movedCount % 25 == 0 || movedCount == totalMoves)
                                {
                                    progressCallback?.Invoke(
                                        $"Grouping '{test.DisplayName}': grouped {movedCount} of {totalMoves} clashes...",
                                        movedCount,
                                        totalMoves);
                                    DoEvents();
                                }
                            }
                            catch (Exception moveEx)
                            {
                                _logger.LogWarning($"Could not move clash result at index {i}: {moveEx.Message}");
                            }
                        }
                    }

                    _logger.Log($"Grouped test '{test.DisplayName}': {movedCount} clashes grouped into {groupIndex - 1} groups.");
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
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double dz = p1.Z - p2.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public int ExportReviewedViewpoints(Document doc, IEnumerable<ClashTest> tests)
        {
            return ExportViewpoints(doc, tests, includeNew: false, includeActive: false, includeReviewed: true, includeApproved: false, includeResolved: false);
        }

        public int ExportViewpoints(
            Document doc,
            IEnumerable<ClashTest> tests,
            bool includeNew,
            bool includeActive,
            bool includeReviewed,
            bool includeApproved,
            bool includeResolved,
            bool timestampedFolder = false,
            Action<string, int, int> progressCallback = null)
        {
            int viewpointsCreated = 0;
            if (doc == null || tests == null) return viewpointsCreated;

            var documentClash = doc.GetClash();
            if (documentClash == null) return viewpointsCreated;

            var clashData = documentClash.TestsData;
            var savedViewpoints = doc.SavedViewpoints;

            FolderItem targetRootFolder = null;
            if (timestampedFolder)
            {
                var tsFolder = new FolderItem { DisplayName = $"Clash Viewpoints ({DateTime.Now:yyyy-MM-dd HHmm})" };
                savedViewpoints.AddCopy(tsFolder);
                targetRootFolder = savedViewpoints.RootItem.Children.LastOrDefault() as FolderItem;
            }

            var testList = tests.ToList();
            int totalTests = testList.Count;
            int testIndex = 0;

            foreach (var test in testList)
            {
                testIndex++;
                try
                {
                    progressCallback?.Invoke(
                        $"Scanning viewpoints for test {testIndex} of {totalTests}: '{test.DisplayName}'...",
                        testIndex,
                        totalTests);
                    DoEvents();

                    var matchingGroups = test.Children.OfType<ClashResultGroup>()
                        .Where(g =>
                            (includeNew && g.Status == ClashResultStatus.New) ||
                            (includeActive && g.Status == ClashResultStatus.Active) ||
                            (includeReviewed && g.Status == ClashResultStatus.Reviewed) ||
                            (includeApproved && g.Status == ClashResultStatus.Approved) ||
                            (includeResolved && g.Status == ClashResultStatus.Resolved))
                        .ToList();

                    // If there are raw results matching and no groups
                    var matchingRaw = test.Children.OfType<ClashResult>()
                        .Where(r =>
                            (includeNew && r.Status == ClashResultStatus.New) ||
                            (includeActive && r.Status == ClashResultStatus.Active) ||
                            (includeReviewed && r.Status == ClashResultStatus.Reviewed) ||
                            (includeApproved && r.Status == ClashResultStatus.Approved) ||
                            (includeResolved && r.Status == ClashResultStatus.Resolved))
                        .ToList();

                    if (matchingGroups.Count == 0 && matchingRaw.Count == 0) continue;

                    // Create folder for the clash test (clean name without trailing delimiters)
                    string folderName = _naming.SanitizeTestDisplayName(test.DisplayName);
                    var folder = new FolderItem { DisplayName = folderName };
                    if (targetRootFolder != null)
                    {
                        savedViewpoints.AddCopy(targetRootFolder, folder);
                    }
                    else
                    {
                        savedViewpoints.AddCopy(folder);
                    }

                    FolderItem actualFolder = targetRootFolder != null
                        ? targetRootFolder.Children.LastOrDefault() as FolderItem
                        : savedViewpoints.RootItem.Children.LastOrDefault() as FolderItem;

                    if (actualFolder == null) continue;

                    int testCreated = 0;

                    // Process Groups
                    int gIdx = 1;
                    foreach (var group in matchingGroups)
                    {
                        var vp = GetTestsViewpointForResult(clashData, group);
                        if (vp != null)
                        {
                            string vpName = _naming.FormatViewpointName(test.DisplayName, group.DisplayName, gIdx);
                            var svp = new SavedViewpoint(vp) { DisplayName = vpName };
                            NativeClashRedlineHelper.CopyRedlinesAndComments(group, svp, _logger);
                            savedViewpoints.AddCopy(actualFolder, svp);
                            viewpointsCreated++;
                            testCreated++;

                            if (viewpointsCreated % 20 == 0)
                            {
                                progressCallback?.Invoke(
                                    $"Creating viewpoints for '{test.DisplayName}': {testCreated} exported...",
                                    testCreated,
                                    matchingGroups.Count + matchingRaw.Count);
                                DoEvents();
                            }
                        }
                        gIdx++;
                    }

                    // Process Raw Results (if any)
                    int rIdx = 1;
                    foreach (var raw in matchingRaw)
                    {
                        var vp = GetTestsViewpointForResult(clashData, raw);
                        if (vp != null)
                        {
                            string vpName = _naming.FormatViewpointName(test.DisplayName, raw.DisplayName, rIdx);
                            var svp = new SavedViewpoint(vp) { DisplayName = vpName };
                            NativeClashRedlineHelper.CopyRedlinesAndComments(raw, svp, _logger);
                            savedViewpoints.AddCopy(actualFolder, svp);
                            viewpointsCreated++;
                            testCreated++;

                            if (viewpointsCreated % 20 == 0)
                            {
                                progressCallback?.Invoke(
                                    $"Creating viewpoints for '{test.DisplayName}': {testCreated} exported...",
                                    testCreated,
                                    matchingGroups.Count + matchingRaw.Count);
                                DoEvents();
                            }
                        }
                        rIdx++;
                    }

                    _logger.Log($"Exported {testCreated} viewpoints for test: {test.DisplayName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error exporting viewpoints for test '{test.DisplayName}'", ex);
                }
            }

            return viewpointsCreated;
        }

        private Viewpoint GetTestsViewpointForResult(DocumentClashTests clashData, IClashResult result)
        {
            if (clashData == null || result == null) return null;

            // 0. If group itself has no saved viewpoint, check if any child has a saved viewpoint or redlines
            if (result is ClashResultGroup clashGroup && !clashGroup.HasSavedViewpoint && clashGroup.Children != null)
            {
                var childWithVp = clashGroup.Children.OfType<ClashResult>().FirstOrDefault(c => c.HasSavedViewpoint || c.HasRedlines);
                if (childWithVp != null)
                {
                    var childVp = GetTestsViewpointForResult(clashData, childWithVp);
                    if (childVp != null) return childVp;
                }
            }

            // 1. Primary: In Navisworks 2024+, TestsViewpointForResult exists on DocumentClashTests and takes IClashResult
            try
            {
                var method = typeof(DocumentClashTests).GetMethod("TestsViewpointForResult", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                if (method != null)
                {
                    var vp = method.Invoke(clashData, new object[] { result }) as Viewpoint;
                    if (vp != null) return vp;
                }
            }
            catch
            {
                // Fall through to geometric camera positioning
            }

            // 2. Focused geometric camera fallback (Navisworks 2023 or when native VP is null):
            // Extract exact 3D coordinates and bounding box from the clash or group
            try
            {
                Point3D center = Point3D.Origin;
                BoundingBox3D bbox = null;
                bool hasCoords = false;

                if (result is ClashResult res)
                {
                    bbox = res.BoundingBox;
                    center = res.Center;
                    hasCoords = true;
                }
                else if (result is ClashResultGroup grp)
                {
                    bbox = grp.BoundingBox;
                    if (grp.RepresentativeResult != null)
                    {
                        center = grp.RepresentativeResult.Center;
                        hasCoords = true;
                    }
                    else if (bbox != null)
                    {
                        center = bbox.Center;
                        hasCoords = true;
                    }
                }

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc?.CurrentViewpoint?.Value != null)
                {
                    var vp = doc.CurrentViewpoint.Value.CreateCopy();
                    if (hasCoords)
                    {
                        double radius = 3.0;
                        if (bbox != null)
                        {
                            var extents = bbox.Size;
                            radius = Math.Max(extents.Length * 0.5, 2.0);
                        }

                        // Standard comfortable isometric perspective direction
                        Vector3D dir = new Vector3D(1.0, 1.0, -0.7).Normalize();
                        double focalDist = Math.Max(radius * 3.0, 6.0); // 6 feet minimum distance
                        Vector3D offset = dir * focalDist;
                        Point3D cameraEye = new Point3D(center.X - offset.X, center.Y - offset.Y, center.Z - offset.Z);

                        vp.Position = cameraEye;
                        vp.PivotPoint = center;
                        vp.FocalDistance = focalDist;
                        vp.AlignDirection(dir);
                        vp.AlignUp(new Vector3D(0, 0, 1));
                    }
                    return vp;
                }
            }
            catch { }

            return null;
        }
    }
}
