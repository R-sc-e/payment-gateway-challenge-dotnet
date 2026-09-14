using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Tests.HttpMocks.AcquiringBank;
using PaymentGateway.Api.Tests.TestInfrastructure;

namespace PaymentGateway.Api.Tests.Scenarios.Payments;

public sealed class ProcessPaymentScenarios : IClassFixture<TestServer>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestServer _testServer;
    private readonly PostPaymentRequest _request;

    public ProcessPaymentScenarios(TestServer testServer)
    {
        _testServer = testServer;
        _testServer.AcquiringBankServer.Reset();
        _request = new PostPaymentRequest(
            "2222405343248877", 7, 2099, "GBP", 1050, "123");
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ValidPayment_IsSentToBankStoredAndRetrievable(
        bool authorized,
        PaymentStatus expectedStatus)
    {
        // Arrange
        var storedPaymentCount = _testServer.PaymentsRepository.Count;
        var bankResponse = authorized
            ? Payment.Authorized(_request)
            : Payment.Declined(_request);
        _testServer.AcquiringBankServer.AddMock(bankResponse);

        // Act
        var postResponse = await _testServer.Client.PostAsJsonAsync(
            "/api/payments",
            _request);
        var responseJson = await postResponse.Content.ReadAsStringAsync();
        var processedPayment = JsonSerializer.Deserialize<PaymentResponse>(
            responseJson,
            JsonOptions) ?? throw new InvalidOperationException("The API returned no payment.");
        var storedPayment = _testServer.PaymentsRepository.Get(processedPayment.Id);
        var getResponse = await _testServer.Client.GetAsync(
            $"/api/payments/{processedPayment.Id}");
        var retrievedPayment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(
            JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Equal(expectedStatus, processedPayment.Status);
        Assert.Equal("8877", processedPayment.CardNumberLastFour);
        Assert.Equal(_request.ExpiryMonth, processedPayment.ExpiryMonth);
        Assert.Equal(_request.ExpiryYear, processedPayment.ExpiryYear);
        Assert.Equal(_request.Currency, processedPayment.Currency);
        Assert.Equal(_request.Amount, processedPayment.Amount);

        Assert.DoesNotContain(_request.CardNumber!, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain(_request.Cvv!, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("opaque-authorization-code", responseJson, StringComparison.Ordinal);

        Assert.Equal(
            1,
            _testServer.AcquiringBankServer.GetRequestCount(HttpMethod.Post.Method, "/payments"));
        Assert.Equal(storedPaymentCount + 1, _testServer.PaymentsRepository.Count);
        Assert.NotNull(storedPayment);
        Assert.Equal(processedPayment.Id, storedPayment.Id);
        Assert.Equal(expectedStatus, storedPayment.Status);
        Assert.Equal("8877", storedPayment.CardNumberLastFour);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrievedPayment);
        Assert.Equal(processedPayment.Id, retrievedPayment.Id);
        Assert.Equal(processedPayment.Status, retrievedPayment.Status);
        Assert.Equal(processedPayment.CardNumberLastFour, retrievedPayment.CardNumberLastFour);
        Assert.Equal(processedPayment.ExpiryMonth, retrievedPayment.ExpiryMonth);
        Assert.Equal(processedPayment.ExpiryYear, retrievedPayment.ExpiryYear);
        Assert.Equal(processedPayment.Currency, retrievedPayment.Currency);
        Assert.Equal(processedPayment.Amount, retrievedPayment.Amount);
    }
}
