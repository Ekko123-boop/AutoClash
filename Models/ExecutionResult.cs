using System.Collections.Generic;

namespace AutomatedClashRunner.Models
{
    public class ExecutionResult
    {
        public List<string> GeneratedSets { get; set; } = new List<string>();
        public List<string> SkippedSets { get; set; } = new List<string>();
        public List<string> SuccessfulTests { get; set; } = new List<string>();
        public List<string> SkippedTests { get; set; } = new List<string>();
        public List<string> FailedTests { get; set; } = new List<string>();
    }
}
