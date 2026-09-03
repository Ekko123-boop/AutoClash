using System;
using Xunit;
using FluentAssertions;
using AutomatedClashRunner.Services;

namespace AutomatedClashRunner.Tests
{
    public class HardwareFingerprintAndLicenseTests
    {
        [Fact]
        public void GetMachineId_ReturnsNonEmptyStringStartingWithACR()
        {
            // Act
            string machineId = HardwareFingerprint.GetMachineId();

            // Assert
            machineId.Should().NotBeNullOrWhiteSpace();
            machineId.Should().StartWith("ACR-");
        }

        [Fact]
        public void GetMachineId_IsDeterministicAcrossCalls()
        {
            // Act
            string id1 = HardwareFingerprint.GetMachineId();
            string id2 = HardwareFingerprint.GetMachineId();

            // Assert
            id1.Should().Be(id2);
        }

        [Fact]
        public void StringProtection_UnmasksEndpointsAccurately()
        {
            // Act
            string endpoint = StringProtection.GetLicenseEndpoint();
            string salt = StringProtection.GetMasterSalt();

            // Assert
            endpoint.Should().NotBeNullOrWhiteSpace();
            endpoint.Should().StartWith("https://");
            endpoint.Should().Contain("firebaseio.com");

            salt.Should().NotBeNullOrWhiteSpace();
        }
    }
}
