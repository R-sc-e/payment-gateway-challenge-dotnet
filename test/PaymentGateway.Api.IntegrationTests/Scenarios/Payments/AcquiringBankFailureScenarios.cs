using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Tests.HttpMocks.AcquiringBank;
using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Scenarios.Payments;

public sealed class AcquiringBankFailureScenarios : IClassFixture<TestServer>
{
    private readonly TestServer _testServer;
    private readonly PostPaymentRequest _request;

    public AcquiringBankFailureScenarios(TestServer testServer)
    {
        _testServer = testServer;
        _testServer.AcquiringBankServer.Reset();
        _request = new PostPaymentRequest(
            "2222405343248877", 7, 2099, "GBP", 1050, "123");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task BankHttpFailure_ReturnsBadGatewayWithoutStoringPayment(
        HttpStatusCode bankStatusCode)
    {
        // Arrange
        var storedPaymentCount = _testServer.PaymentsRepository.Count;
        _testServer.AcquiringBankServer.AddMock(Payment.Failed(bankStatusCode));

        // Act
        var response = await _testServer.Client.PostAsJsonAsync("/api/payments", _request);
        var responseJson = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(responseJson);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "The acquiring bank is unavailable",
            problem.RootElement.GetProperty("title").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("acquiring bank returned", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            1,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
        Assert.Equal(storedPaymentCount, _testServer.PaymentsRepository.Count);
    }

    [Fact]
    public async Task InvalidBankResponse_ReturnsBadGatewayWithoutStoringPayment()
    {
        // Arrange
        var storedPaymentCount = _testServer.PaymentsRepository.Count;
        _testServer.AcquiringBankServer.AddMock(Payment.InvalidResponse());

        // Act
        var response = await _testServer.Client.PostAsJsonAsync("/api/payments", _request);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(
            1,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
        Assert.Equal(storedPaymentCount, _testServer.PaymentsRepository.Count);
    }
}
