using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Scenarios.Payments;

public sealed class RetrievePaymentScenarios : IClassFixture<TestServer>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestServer _testServer;

    public RetrievePaymentScenarios(TestServer testServer)
    {
        _testServer = testServer;
        _testServer.AcquiringBankServer.Reset();
    }

    [Fact]
    public async Task StoredPayment_IsReturnedWithoutCallingBank()
    {
        // Arrange
        var payment = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Authorized, "8877", 7, 2099, "GBP", 1050);
        _testServer.PaymentsRepository.Add(payment);

        // Act
        var response = await _testServer.Client.GetAsync($"/api/payments/{payment.Id}");
        var retrievedPayment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(retrievedPayment);
        Assert.Equal(payment.Id, retrievedPayment.Id);
        Assert.Equal(payment.Status, retrievedPayment.Status);
        Assert.Equal(payment.CardNumberLastFour, retrievedPayment.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, retrievedPayment.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, retrievedPayment.ExpiryYear);
        Assert.Equal(payment.Currency, retrievedPayment.Currency);
        Assert.Equal(payment.Amount, retrievedPayment.Amount);
        Assert.Equal(
            0,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
    }

    [Fact]
    public async Task UnknownPayment_ReturnsNotFoundProblemDetails()
    {
        // Act
        var response = await _testServer.Client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task MalformedPaymentIdentifier_ReturnsNotFoundProblemDetails()
    {
        // Act
        var response = await _testServer.Client.GetAsync("/api/payments/not-a-guid");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }
}
