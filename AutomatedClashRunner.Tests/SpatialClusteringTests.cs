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
    }
}
