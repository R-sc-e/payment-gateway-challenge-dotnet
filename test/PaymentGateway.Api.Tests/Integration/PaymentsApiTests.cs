using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Integration;

public sealed class PaymentsApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessesAndRetrievesPayment(bool authorized, PaymentStatus expectedStatus)
    {
        await using var factory = new PaymentGatewayFactory();
        factory.Bank.Handler = (_, _) => Task.FromResult(new BankPaymentResponse(
            authorized,
            authorized ? "opaque-authorization-code" : string.Empty));
        using var client = factory.CreateClient();
        var request = TestRequests.Valid();

        var postResponse = await client.PostAsJsonAsync("/api/payments", request);
        var payment = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(1, factory.Bank.CallCount);

        var getResponse = await client.GetAsync($"/api/payments/{payment.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(payment, retrieved);
    }

    [Fact]
    public async Task ResponseNeverExposesSensitiveOrBankOnlyValues()
    {
        await using var factory = new PaymentGatewayFactory();
        factory.Bank.Handler = (_, _) => Task.FromResult(
            new BankPaymentResponse(true, "secret-bank-code"));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", TestRequests.Valid(cvv: "987"));
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.DoesNotContain("2222405343248877", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-bank-code", json, StringComparison.Ordinal);
        Assert.Contains("8877", json, StringComparison.Ordinal);
        Assert.False(document.RootElement.TryGetProperty("cardNumber", out _));
        Assert.False(document.RootElement.TryGetProperty("cvv", out _));
        Assert.False(document.RootElement.TryGetProperty("authorizationCode", out _));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task RejectsInvalidRequestWithoutCallingBank(PostPaymentRequest request)
    {
        await using var factory = new PaymentGatewayFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", request);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Rejected", problem.RootElement.GetProperty("paymentStatus").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(0, factory.Bank.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"cardNumber\":\"2222405343248877\",\"expiryMonth\":7,\"expiryYear\":2031,\"currency\":\"GBP\",\"amount\":1.5,\"cvv\":\"123\"}")]
    [InlineData("{\"cardNumber\":\"2222405343248877\",\"expiryMonth\":7,\"expiryYear\":2031,\"currency\":\"GBP\",\"amount\":999999999999,\"cvv\":\"123\"}")]
    public async Task RejectsMissingMalformedOrNonIntegerJson(string json)
    {
        await using var factory = new PaymentGatewayFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/payments", content);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Rejected", problem.RootElement.GetProperty("paymentStatus").GetString());
        Assert.Equal(0, factory.Bank.CallCount);
    }

    [Theory]
    [InlineData(AcquiringBankFailure.Unavailable)]
    [InlineData(AcquiringBankFailure.Timeout)]
    [InlineData(AcquiringBankFailure.InvalidResponse)]
    public async Task MapsKnownBankFailuresToBadGateway(AcquiringBankFailure failure)
    {
        await using var factory = new PaymentGatewayFactory();
        factory.Bank.Handler = (_, _) => throw new AcquiringBankException(failure, "bank detail");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", TestRequests.Valid());
        var json = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("bank detail", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MapsUnexpectedFailureToSanitizedInternalServerError()
    {
        await using var factory = new PaymentGatewayFactory();
        factory.Bank.Handler = (_, _) => throw new InvalidOperationException("sensitive internal detail");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", TestRequests.Valid());
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("sensitive internal detail", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownOrMalformedIdentifier()
    {
        await using var factory = new PaymentGatewayFactory();
        using var client = factory.CreateClient();

        var unknown = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        using var problem = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        var malformed = await client.GetAsync("/api/payments/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task OperationalEndpointsAreAvailable(string path)
    {
        await using var factory = new PaymentGatewayFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<PostPaymentRequest> InvalidRequests => new()
    {
        TestRequests.Valid(cardNumber: "123"),
        TestRequests.Valid(expiryMonth: 13),
        TestRequests.Valid(expiryMonth: 5, expiryYear: 2030),
        TestRequests.Valid(currency: "gbp"),
        TestRequests.Valid(amount: 0),
        TestRequests.Valid(cvv: "12")
    };
}