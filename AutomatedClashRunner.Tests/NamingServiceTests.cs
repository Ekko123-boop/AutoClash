using System;
using Xunit;
using FluentAssertions;
using AutomatedClashRunner.Services;

namespace AutomatedClashRunner.Tests
{
    public class NamingServiceTests
    {
        private readonly NamingService _namingService = NamingService.Instance;

        [Theory]
        [InlineData("F1-STS-HDLS202-MX.nwc", "STS-HDLS202-MX")]
        [InlineData("B1-ARC-WALLS-01.nwc", "ARC-WALLS-01")]
        [InlineData("L2-MEP-HVAC-DUCTS.nwd", "MEP-HVAC-DUCTS")]
        public void GetTrimmedModelCode_WithHyphenDelimiter_ReturnsCorrectCode(string input, string expected)
        {
            // Act
            string result = _namingService.GetTrimmedModelCode(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("F1_STS-HDLS202-MX.nwc", "STS-HDLS202-MX")]
        [InlineData("B2_ELEC_LIGHTING.nwd", "ELEC_LIGHTING")]
        public void GetTrimmedModelCode_WithUnderscoreDelimiter_ReturnsCorrectCode(string input, string expected)
        {
            // Act
            string result = _namingService.GetTrimmedModelCode(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GetTrimmedModelCode_WithoutDelimiter_ReturnsCleanFilenameWithoutExtension()
        {
            // Act
            string result = _namingService.GetTrimmedModelCode("StructuralModel.nwc");

            // Assert
            result.Should().Be("StructuralModel");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetTrimmedModelCode_WithNullOrWhitespace_ReturnsEmptyString(string input)
        {
            // Act
            string result = _namingService.GetTrimmedModelCode(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("F1-STS-HDLS202-MX.nwc", "Base Build", "STS-HDLS202-MX")]
        [InlineData("F1-STS-HDLS202-MX.nwc", "BaseBuild", "STS-HDLS202-MX")]
        [InlineData("F1-STS-HDLS202-MX.nwc", "base build", "STS-HDLS202-MX")]
        [InlineData("F1-STS-HDLS202-MX.nwc", "basebuild", "STS-HDLS202-MX")]
        public void GetClashTestName_BaseBuildSet_ReturnsTrimmedCodeWithoutPrefix(string modelName, string setName, string expected)
        {
            // Act
            string result = _namingService.GetClashTestName(modelName, setName);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("F1-STS-HDLS202-MX.nwc", "Mechanical", "T-STS-HDLS202-MX")]
        [InlineData("F1-STS-HDLS202-MX.nwc", "Electrical", "T-STS-HDLS202-MX")]
        [InlineData("F1-STS-HDLS202-MX.nwc", "STS-HDLS202-MX", "T-STS-HDLS202-MX")]
        public void GetClashTestName_StandardManualSet_PrependsTPrefix(string modelName, string setName, string expected)
        {
            // Act
            string result = _namingService.GetClashTestName(modelName, setName);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GetToolsTestClashName_StandardModel_PrependsTPrefix()
        {
            // Act
            string result = _namingService.GetToolsTestClashName("F1-STS-HDLS202-MX.nwc");

            // Assert
            result.Should().Be("T-STS-HDLS202-MX");
        }

        [Fact]
        public void GetToolsTestClashName_AlreadyPrefixedWithT_DoesNotDoublePrefix()
        {
            // Act
            string result = _namingService.GetToolsTestClashName("F1-T-STS-HDLS202-MX.nwc");

            // Assert
            result.Should().Be("T-STS-HDLS202-MX");
        }

        [Fact]
        public void GetBaseBuildClashName_ReturnsTrimmedCodeDirectly()
        {
            // Act
            string result = _namingService.GetBaseBuildClashName("F1-STS-HDLS202-MX.nwc");

            // Assert
            result.Should().Be("STS-HDLS202-MX");
        }

        [Fact]
        public void GetConstructabilityClashName_SingleModel_PrependsCPrefix()
        {
            // Act
            string result = _namingService.GetConstructabilityClashName("F1-STS-HDLS202-MX.nwc");

            // Assert
            result.Should().Be("C-STS-HDLS202-MX");
        }

        [Fact]
        public void GetConstructabilityClashName_AlreadyPrefixedWithC_DoesNotDoublePrefix()
        {
            // Act
            string result = _namingService.GetConstructabilityClashName("F1-C-STS-HDLS202-MX.nwc");

            // Assert
            result.Should().Be("C-STS-HDLS202-MX");
        }

        [Fact]
        public void GetConstructabilityClashName_MultipleModelsSharingParent_ReturnsCPrefixedParentName()
        {
            // Arrange
            var models = new System.Collections.Generic.List<AutomatedClashRunner.Models.ModelSourceNode>
            {
                new AutomatedClashRunner.Models.ModelSourceNode { DisplayName = "F1-HVAC.nwc", ParentContainerName = "MEI.nwd" },
                new AutomatedClashRunner.Models.ModelSourceNode { DisplayName = "F1-ELEC.nwc", ParentContainerName = "MEI.nwd" }
            };

            // Act
            string result = _namingService.GetConstructabilityClashName(models);

            // Assert
            result.Should().Be("C-MEI");
        }

        [Fact]
        public void GetConstructabilityClashName_MultipleModelsDifferentParents_ReturnsFallback()
        {
            // Arrange
            var models = new System.Collections.Generic.List<AutomatedClashRunner.Models.ModelSourceNode>
            {
                new AutomatedClashRunner.Models.ModelSourceNode { DisplayName = "F1-HVAC.nwc", ParentContainerName = "MEI.nwd" },
                new AutomatedClashRunner.Models.ModelSourceNode { DisplayName = "F1-STEEL.nwc", ParentContainerName = "STR.nwd" }
            };

            // Act
            string result = _namingService.GetConstructabilityClashName(models);

            // Assert
            result.Should().Be("C-Constructability");
        }

        [Fact]
        public void GetConstructabilityClashName_EmptyList_ReturnsFallback()
        {
            // Act
            string result = _namingService.GetConstructabilityClashName(new System.Collections.Generic.List<AutomatedClashRunner.Models.ModelSourceNode>());

            // Assert
            result.Should().Be("C-Constructability");
        }

        [Theory]
        [InlineData("F1-EGE-ASP1106-E-.nwc", "EGE-ASP1106-E")]
        [InlineData("F1_EGE-ASP1106-E_.nwc", "EGE-ASP1106-E")]
        [InlineData("MODEL-TEST---.nwd", "TEST")]
        public void GetTrimmedModelCode_WithTrailingDelimiters_TrimsTrailingHyphensAndUnderscores(string input, string expected)
        {
            // Act
            string result = _namingService.GetTrimmedModelCode(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("T-EGE-ASP1106-E-", "T-EGE-ASP1106-E")]
        [InlineData("EGE-ASP1106-E--", "EGE-ASP1106-E")]
        [InlineData("EGE-ASP1106-E_ ", "EGE-ASP1106-E")]
        [InlineData("  TEST-MODEL-  ", "TEST-MODEL")]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void SanitizeTestDisplayName_StripsTrailingDelimiters(string input, string expected)
        {
            // Act
            string result = _namingService.SanitizeTestDisplayName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void FormatGroupName_WithTrailingDashTestName_FormatsWithSpaceAndUnpaddedNumber()
        {
            // Act
            string result = _namingService.FormatGroupName("T-EGE-ASP1106-E-", 4);

            // Assert
            result.Should().Be("T-EGE-ASP1106-E 4");
        }

        [Fact]
        public void FormatGroupName_WithCleanTestName_FormatsWithSpaceAndUnpaddedNumber()
        {
            // Act
            string result = _namingService.FormatGroupName("EGE-ASP1106-E", 12);

            // Assert
            result.Should().Be("EGE-ASP1106-E 12");
        }

        [Fact]
        public void FormatViewpointName_FromLegacyDoubleDashGroup_KeepsExactClashNumber()
        {
            // Act
            string result = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "T-EGE-ASP1106-E--004");

            // Assert
            result.Should().Be("T-EGE-ASP1106-E 4");
        }

        [Fact]
        public void FormatViewpointName_FromCleanGroup_KeepsExactClashNumber()
        {
            // Act
            string result = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "T-EGE-ASP1106-E 5");

            // Assert
            result.Should().Be("T-EGE-ASP1106-E 5");
        }

        [Fact]
        public void FormatViewpointName_FromRawClash_KeepsExactClashNumber()
        {
            // Act
            string res1 = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "Clash 4");
            string res2 = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "Clash16");

            // Assert
            res1.Should().Be("T-EGE-ASP1106-E 4");
            res2.Should().Be("T-EGE-ASP1106-E 16");
        }

        [Fact]
        public void FormatViewpointName_FilteredSubset_PreservesIndividualClashNumbers()
        {
            // Simulating exporting only Reviewed clashes (clash 4 and clash 5)
            string vp4 = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "T-EGE-ASP1106-E--004", fallbackIndex: 1);
            string vp5 = _namingService.FormatViewpointName("T-EGE-ASP1106-E-", "T-EGE-ASP1106-E--005", fallbackIndex: 2);

            // Assert - must NOT renumber to 1 and 2!
            vp4.Should().Be("T-EGE-ASP1106-E 4");
            vp5.Should().Be("T-EGE-ASP1106-E 5");
        }

        [Theory]
        [InlineData("F1-L0-BAE-E.nwc", "L0-BAE-E")]
        [InlineData("Tests/L0-BAE-E-", "L0-BAE-E")]
        [InlineData("Folder\\SubFolder\\L0-JCB-CW", "L0-JCB-CW")]
        [InlineData("F1_L0-JCB-CW.nwd", "L0-JCB-CW")]
        [InlineData("Clearance Set", "Clearance Set")]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void SanitizeItemName_CleansPathsAndExtensions(string input, string expected)
        {
            string result = _namingService.SanitizeItemName(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("F1-L0-BAE-E.nwc", "F1-L0-JCB-CW.nwc", "v", "L0-BAE-E v L0-JCB-CW")]
        [InlineData("F1-L0-BAE-E.nwc", "F1-L0-JCB-CW.nwc", "x", "L0-BAE-E x L0-JCB-CW")]
        [InlineData("F1-L0-BAE-E.nwc", "F1-L0-JCB-CW.nwc", "vs", "L0-BAE-E vs L0-JCB-CW")]
        [InlineData("F1-L0-BAE-E.nwc", "Tests/Base Build", "v", "L0-BAE-E v Base Build")]
        [InlineData("Tests/Electrical", "Tests/Mechanical", "v", "Electrical v Mechanical")]
        [InlineData("L0-BAE-E", "L0-JCB-CW", "", "L0-BAE-E v L0-JCB-CW")]
        public void GetGenericClashTestName_GeneratesCleanCombinationNames(string itemA, string itemB, string delimiter, string expected)
        {
            string result = _namingService.GetGenericClashTestName(itemA, itemB, delimiter);
            result.Should().Be(expected);
        }
    }
}
