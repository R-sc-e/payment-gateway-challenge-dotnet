using System.Globalization;
using System.Net;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Configuration;

public sealed class AcquiringBankOptionsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(121)]
    public async Task Startup_RejectsInvalidTimeout(int timeoutSeconds)
    {
        // Arrange
        var testServer = new TestServer(new Dictionary<string, string?>
        {
            ["AcquiringBank:TimeoutSeconds"] =
                timeoutSeconds.ToString(CultureInfo.InvariantCulture)
        });

        try
        {
            // Act
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(
                testServer.InitializeAsync);

            // Assert
            Assert.Contains(
                exception.Failures,
                failure => failure.Contains(
                    nameof(AcquiringBankOptions.TimeoutSeconds),
                    StringComparison.Ordinal));
        }
        finally
        {
            await testServer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public async Task Startup_AcceptsTimeoutBoundaries(int timeoutSeconds)
    {
        // Arrange
        var testServer = new TestServer(new Dictionary<string, string?>
        {
            ["AcquiringBank:TimeoutSeconds"] =
                timeoutSeconds.ToString(CultureInfo.InvariantCulture)
        });

        try
        {
            await testServer.InitializeAsync();

            // Act
            var response = await testServer.Client.GetAsync("/health/live");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await testServer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Startup_RejectsNonHttpBankUrl()
    {
        // Arrange
        var testServer = new TestServer(new Dictionary<string, string?>
        {
            ["AcquiringBank:BaseUrl"] = "ftp://bank.test/"
        });

        try
        {
            // Act
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(
                testServer.InitializeAsync);

            // Assert
            Assert.Contains(
                exception.Failures,
                failure => failure.Contains("BaseUrl", StringComparison.Ordinal));
        }
        finally
        {
            await testServer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Startup_AcceptsHttpsBankUrl()
    {
        // Arrange
        var testServer = new TestServer(new Dictionary<string, string?>
        {
            ["AcquiringBank:BaseUrl"] = "https://bank.test/"
        });

        try
        {
            await testServer.InitializeAsync();

            // Act
            var response = await testServer.Client.GetAsync("/health/live");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await testServer.DisposeAsync();
        }
    }
}
