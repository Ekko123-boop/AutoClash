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
    }
}
