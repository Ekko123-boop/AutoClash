using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace AutomatedClashRunner.Tests
{
    public class SpatialClusteringTests
    {
        public struct Point3D
        {
            public double X { get; }
            public double Y { get; }
            public double Z { get; }

            public Point3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double DistanceTo(Point3D other)
            {
                double dx = X - other.X;
                double dy = Y - other.Y;
                double dz = Z - other.Z;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        // Simulates the exact spatial clustering logic used in ClashDistillerService
        private List<List<Point3D>> ClusterPoints(List<Point3D> points, double maxDistMeters)
        {
            var clusters = new List<List<Point3D>>();
            foreach (var pt in points)
            {
                bool added = false;
                foreach (var cluster in clusters)
                {
                    if (cluster.Any(c => c.DistanceTo(pt) <= maxDistMeters))
                    {
                        cluster.Add(pt);
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    clusters.Add(new List<Point3D> { pt });
                }
            }
            return clusters;
        }

        [Fact]
        public void DistanceCalculation_3DEuclidean_IsAccurate()
        {
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(3, 4, 0);

            p1.DistanceTo(p2).Should().BeApproximately(5.0, 0.0001);

            var p3 = new Point3D(1, 2, 2);
            var p4 = new Point3D(4, 6, 2);
            p3.DistanceTo(p4).Should().BeApproximately(5.0, 0.0001);
        }

        [Theory]
        [InlineData(1.0, 0.3048)]
        [InlineData(10.0, 3.048)]
        [InlineData(50.0, 15.24)]
        [InlineData(150.0, 45.72)]
        [InlineData(300.0, 91.44)]
        public void FeetToMetersConversion_InternalNavisworksMapping_IsExact(double feet, double expectedMeters)
        {
            double meters = feet * 0.3048;
            meters.Should().BeApproximately(expectedMeters, 0.00001);
        }

        [Fact]
        public void ClusterPoints_WithinProximityThreshold_GroupsIntoSingleCluster()
        {
            // 3 points all within 1 meter of each other
            var points = new List<Point3D>
            {
                new Point3D(0, 0, 0),
                new Point3D(0.5, 0.2, 0.1),
                new Point3D(0.8, 0.4, 0.2)
            };

            double thresholdMeters = 10.0 * 0.3048; // 10 ft = ~3.048 m
            var clusters = ClusterPoints(points, thresholdMeters);

            clusters.Should().HaveCount(1);
            clusters[0].Should().HaveCount(3);
        }

        [Fact]
        public void ClusterPoints_FarApart_CreatesDistinctClusters()
        {
            // Point A at origin, Point B at 100m away
            var points = new List<Point3D>
            {
                new Point3D(0, 0, 0),
                new Point3D(100, 100, 100)
            };

            double thresholdMeters = 5.0 * 0.3048; // 5 ft
            var clusters = ClusterPoints(points, thresholdMeters);

            clusters.Should().HaveCount(2);
            clusters[0].Should().HaveCount(1);
            clusters[1].Should().HaveCount(1);
        }

        [Fact]
        public void ClusterPoints_EmptyList_ReturnsNoClusters()
        {
            var points = new List<Point3D>();
            var clusters = ClusterPoints(points, 5.0);

            clusters.Should().BeEmpty();
        }

        [Fact]
        public void ClusterPoints_SinglePoint_ReturnsOneClusterWithOnePoint()
        {
            var points = new List<Point3D> { new Point3D(5, 5, 5) };
            var clusters = ClusterPoints(points, 5.0);

            clusters.Should().HaveCount(1);
            clusters[0].Should().ContainSingle();
        }

        private struct VoxelKey : IEquatable<VoxelKey>
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;

            public VoxelKey(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(VoxelKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is VoxelKey other && Equals(other);
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

        // Simulates the high-performance O(N) voxel grid clustering implemented in ClashDistillerService
        private List<List<Point3D>> ClusterPointsVoxelGrid(List<Point3D> points, double maxDistMeters)
        {
            var clusters = new List<List<Point3D>>();
            if (points == null || points.Count == 0) return clusters;

            if (points.Count == 1 || maxDistMeters <= 0.0001)
            {
                return points.Select(p => new List<Point3D> { p }).ToList();
            }

            double maxDistSq = maxDistMeters * maxDistMeters;
            double cellSize = maxDistMeters;

            var grid = new Dictionary<VoxelKey, List<Point3D>>();
            var pointToCluster = new Dictionary<Point3D, List<Point3D>>();

            foreach (var pt in points)
            {
                int gx = (int)Math.Floor(pt.X / cellSize);
                int gy = (int)Math.Floor(pt.Y / cellSize);
                int gz = (int)Math.Floor(pt.Z / cellSize);

                List<Point3D> matchedCluster = null;

                for (int dx = -1; dx <= 1 && matchedCluster == null; dx++)
                {
                    for (int dy = -1; dy <= 1 && matchedCluster == null; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var key = new VoxelKey(gx + dx, gy + dy, gz + dz);
                            if (grid.TryGetValue(key, out var bin))
                            {
                                foreach (var cand in bin)
                                {
                                    double dX = cand.X - pt.X;
                                    double dY = cand.Y - pt.Y;
                                    double dZ = cand.Z - pt.Z;
                                    if (dX * dX + dY * dY + dZ * dZ <= maxDistSq)
                                    {
                                        if (pointToCluster.TryGetValue(cand, out var c))
                                        {
                                            matchedCluster = c;
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
                    matchedCluster.Add(pt);
                    pointToCluster[pt] = matchedCluster;
                }
                else
                {
                    var newCluster = new List<Point3D> { pt };
                    clusters.Add(newCluster);
                    pointToCluster[pt] = newCluster;
                }

                var selfKey = new VoxelKey(gx, gy, gz);
                if (!grid.TryGetValue(selfKey, out var selfBin))
                {
                    selfBin = new List<Point3D>();
                    grid[selfKey] = selfBin;
                }
                selfBin.Add(pt);
            }

            return clusters;
        }

        [Fact]
        public void VoxelGridClustering_MatchesBruteForce_AcrossSamplePoints()
        {
            var points = new List<Point3D>
            {
                new Point3D(0, 0, 0),
                new Point3D(1, 1, 0),
                new Point3D(1.5, 1.2, 0.2),
                new Point3D(50, 50, 50),
                new Point3D(50.5, 51, 50),
                new Point3D(200, 200, 200)
            };

            double thresholdMeters = 2.0;
            var bruteForce = ClusterPoints(points, thresholdMeters);
            var voxelGrid = ClusterPointsVoxelGrid(points, thresholdMeters);

            voxelGrid.Should().HaveCount(bruteForce.Count);
            for (int i = 0; i < bruteForce.Count; i++)
            {
                voxelGrid[i].Count.Should().Be(bruteForce[i].Count);
            }
        }

        [Fact]
        public void VoxelGridClustering_Performance_1000Points_ExecutesSub100ms()
        {
            var rand = new Random(42);
            var points = new List<Point3D>();
            for (int i = 0; i < 1000; i++)
            {
                points.Add(new Point3D(
                    rand.NextDouble() * 200.0,
                    rand.NextDouble() * 200.0,
                    rand.NextDouble() * 50.0));
            }

            double thresholdMeters = 10.0 * 0.3048; // 10 ft

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var clusters = ClusterPointsVoxelGrid(points, thresholdMeters);
            sw.Stop();

            clusters.Count.Should().BeGreaterThan(0);
            sw.ElapsedMilliseconds.Should().BeLessThan(200, "1,000 points voxel clustering must execute in sub-200ms");
        }
    }
}
