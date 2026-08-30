using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace AutomatedClashRunner.Services.Interfaces
{
    public interface IClashDistillerService
    {
        void ReRunTests(Document doc, IEnumerable<ClashTest> tests);
        int GroupByElement(Document doc, IEnumerable<ClashTest> tests, double maxProximityFt);
        int ExportReviewedViewpoints(Document doc, IEnumerable<ClashTest> tests);
        int ExportViewpoints(
            Document doc,
            IEnumerable<ClashTest> tests,
            bool includeNew,
            bool includeActive,
            bool includeReviewed,
            bool includeApproved,
            bool includeResolved,
            bool timestampedFolder = false);
    }
}
