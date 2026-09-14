using System.Net;

using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Application;

public sealed class OperationalEndpointsTests(TestServer testServer) : IClassFixture<TestServer>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task OperationalEndpoint_ReturnsOk(string path)
    {
        // Act
        var response = await testServer.Client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
