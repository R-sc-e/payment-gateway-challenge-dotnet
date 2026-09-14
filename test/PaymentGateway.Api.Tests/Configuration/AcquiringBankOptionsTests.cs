using System.Globalization;
using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Configuration;

public sealed class AcquiringBankOptionsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(121)]
    public void RejectsInvalidTimeoutAtStartup(int timeoutSeconds)
    {
        using var factory = new PaymentGatewayFactory().WithWebHostBuilder(builder =>
            builder.UseSetting(
                "AcquiringBank:TimeoutSeconds",
                timeoutSeconds.ToString(CultureInfo.InvariantCulture)));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(nameof(AcquiringBankOptions.TimeoutSeconds), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public async Task AcceptsTimeoutBoundaries(int timeoutSeconds)
    {
        await using var factory = new PaymentGatewayFactory().WithWebHostBuilder(builder =>
            builder.UseSetting(
                "AcquiringBank:TimeoutSeconds",
                timeoutSeconds.ToString(CultureInfo.InvariantCulture)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void RejectsNonHttpBankUrlAtStartup()
    {
        using var factory = new PaymentGatewayFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("AcquiringBank:BaseUrl", "ftp://bank.test/"));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("BaseUrl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptsHttpsBankUrlAtStartup()
    {
        await using var factory = new PaymentGatewayFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("AcquiringBank:BaseUrl", "https://bank.test/"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
