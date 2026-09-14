using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Scenarios.Payments;

public sealed class RejectPaymentScenarios : IClassFixture<TestServer>
{
    private readonly TestServer _testServer;

    public RejectPaymentScenarios(TestServer testServer)
    {
        _testServer = testServer;
        _testServer.AcquiringBankServer.Reset();
    }

    public static TheoryData<PostPaymentRequest, string> InvalidRequests => new()
    {
        { new PostPaymentRequest("123", 7, 2099, "GBP", 1050, "123"), "cardNumber" },
        { new PostPaymentRequest("2222405343248877\n", 7, 2099, "GBP", 1050, "123"), "cardNumber" },
        { new PostPaymentRequest("2222405343248877", 13, 2099, "GBP", 1050, "123"), "expiryMonth" },
        { new PostPaymentRequest("2222405343248877", 1, 2000, "GBP", 1050, "123"), "expiryYear" },
        { new PostPaymentRequest("2222405343248877", 7, 2099, "gbp", 1050, "123"), "currency" },
        { new PostPaymentRequest("2222405343248877", 7, 2099, "GBP", 0, "123"), "amount" },
        { new PostPaymentRequest("2222405343248877", 7, 2099, "GBP", 1050, "12"), "cvv" },
        { new PostPaymentRequest("2222405343248877", 7, 2099, "GBP", 1050, "123\n"), "cvv" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidPayment_IsRejectedBeforeBankAndStorage(
        PostPaymentRequest request,
        string expectedInvalidField)
    {
        // Arrange
        var storedPaymentCount = _testServer.PaymentsRepository.Count;

        // Act
        var response = await _testServer.Client.PostAsJsonAsync("/api/payments", request);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Rejected", problem.RootElement.GetProperty("paymentStatus").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.True(
            problem.RootElement
                .GetProperty("errors")
                .TryGetProperty(expectedInvalidField, out _));
        Assert.Equal(
            0,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
        Assert.Equal(storedPaymentCount, _testServer.PaymentsRepository.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"cardNumber\":\"2222405343248877\",\"expiryMonth\":7,\"expiryYear\":2099,\"currency\":\"GBP\",\"amount\":1.5,\"cvv\":\"123\"}")]
    [InlineData("{\"cardNumber\":\"2222405343248877\",\"expiryMonth\":7,\"expiryYear\":2099,\"currency\":\"GBP\",\"amount\":999999999999,\"cvv\":\"123\"}")]
    public async Task InvalidJson_IsRejectedBeforeBankAndStorage(string json)
    {
        // Arrange
        var storedPaymentCount = _testServer.PaymentsRepository.Count;
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _testServer.Client.PostAsync("/api/payments", content);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Rejected", problem.RootElement.GetProperty("paymentStatus").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(
            0,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
        Assert.Equal(storedPaymentCount, _testServer.PaymentsRepository.Count);
    }
}
