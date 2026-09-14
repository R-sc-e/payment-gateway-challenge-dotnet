using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Integration;

public sealed class AcquiringBankClientApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessesBankResponseAndRetrievesStoredPayment(
        bool authorized,
        PaymentStatus expectedStatus)
    {
        await using var factory = new BankClientPaymentGatewayFactory();
        factory.BankTransport.Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    authorized,
                    authorization_code = authorized ? "opaque-code" : string.Empty
                }),
                Encoding.UTF8,
                "application/json")
        });
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/payments", TestRequests.Valid());
        var payment = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal(HttpMethod.Post, factory.BankTransport.LastMethod);
        Assert.Equal(new Uri("http://bank.test/payments"), factory.BankTransport.LastUri);

        using var bankRequest = JsonDocument.Parse(Assert.IsType<string>(factory.BankTransport.LastBody));
        Assert.Equal("2222405343248877", bankRequest.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("07/2031", bankRequest.RootElement.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", bankRequest.RootElement.GetProperty("currency").GetString());
        Assert.Equal(1050, bankRequest.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("123", bankRequest.RootElement.GetProperty("cvv").GetString());

        var getResponse = await client.GetAsync($"/api/payments/{payment.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(payment.Id, retrieved.Id);
        Assert.Equal(expectedStatus, retrieved.Status);
    }

    [Fact]
    public async Task MapsBankHttpFailureToBadGateway()
    {
        await using var factory = new BankClientPaymentGatewayFactory();
        factory.BankTransport.Handler = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments", TestRequests.Valid());
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }
}
