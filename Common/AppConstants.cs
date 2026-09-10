namespace AutomatedClashRunner.Common
{
    public static class AppConstants
    {
        // Units & Tolerances (Navisworks internal database is always meters)
        public const double MetersPerFoot = 0.3048;
        public const double DefaultToleranceMeters = 0.0;
        public const double DefaultConstructabilityToleranceMeters = 0.3048; // 1.0 ft

        // Set & Folder Names
        public const string TestsFolderName = "Tests";
        public const string PocSearchSetName = "POC Elements";
        public const string PocKeyword = "POC";
        public const string BaseBuildSetName = "Base Build";
        public const string BaseBuildCompactSetName = "BaseBuild";

        // Clash Test Naming Prefixes
        public const string ToolsTestPrefix = "T-";
        public const string ConstructabilityPrefix = "C-";

        // Traversal Limits
        public const int MaxModelRecursionDepth = 20;

        // Logging
        public const long LogFileMaxSizeBytes = 10 * 1024 * 1024; // 10 MB

        // Proximity Clustering Defaults (in feet)
        public const double DefaultGroupingProximityFt = 10.0;
        public const double MinGroupingProximityFt = 1.0;
        public const double MaxGroupingProximityFt = 300.0;
    }
}
