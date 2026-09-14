using System.Net;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Clients;

public sealed class AcquiringBankClientTests
{
    private static readonly BankPaymentRequest Request =
        new("2222405343248877", "04/2032", "GBP", 1050, "123");

    [Theory]
    [InlineData(true, "authorization-code")]
    [InlineData(false, "")]
    public async Task ParsesSuccessfulResponses(bool authorized, string authorizationCode)
    {
        var handler = Responds(HttpStatusCode.OK, $$"""
            {"authorized":{{authorized.ToString().ToLowerInvariant()}},"authorization_code":"{{authorizationCode}}"}
            """);
        var client = CreateClient(handler);

        var response = await client.ProcessPaymentAsync(Request, CancellationToken.None);

        Assert.Equal(authorized, response.Authorized);
        Assert.Equal(authorizationCode, response.AuthorizationCode);
    }

    [Fact]
    public async Task SendsExpectedMethodPathAndSnakeCaseJson()
    {
        var handler = Responds(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"code"}""");
        var client = CreateClient(handler);

        await client.ProcessPaymentAsync(Request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("http://bank.test/payments", handler.LastUri?.ToString());
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.LastBody));
        Assert.Equal("2222405343248877", body.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("04/2032", body.RootElement.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal(1050, body.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("123", body.RootElement.GetProperty("cvv").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task MapsNonSuccessResponsesToUnavailable(HttpStatusCode status)
    {
        var client = CreateClient(Responds(status, "{}"));

        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.ProcessPaymentAsync(Request, CancellationToken.None));

        Assert.Equal(AcquiringBankFailure.Unavailable, exception.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"authorized\":false}")]
    [InlineData("{\"authorized\":true,\"authorization_code\":\"\"}")]
    public async Task RejectsMalformedOrIncompleteResponse(string content)
    {
        var client = CreateClient(Responds(HttpStatusCode.OK, content));

        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.ProcessPaymentAsync(Request, CancellationToken.None));

        Assert.Equal(AcquiringBankFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task MapsNetworkFailureToUnavailable()
    {
        var handler = new StubHttpMessageHandler
        {
            Handler = (_, _) => throw new HttpRequestException("network unavailable")
        };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.ProcessPaymentAsync(Request, CancellationToken.None));

        Assert.Equal(AcquiringBankFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task MapsHttpClientTimeoutButNotCallerCancellation()
    {
        var handler = new StubHttpMessageHandler
        {
            Handler = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        };
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://bank.test/"),
            Timeout = TimeSpan.FromMilliseconds(20)
        };
        var client = new AcquiringBankClient(httpClient);

        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.ProcessPaymentAsync(Request, CancellationToken.None));
        Assert.Equal(AcquiringBankFailure.Timeout, exception.Failure);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ProcessPaymentAsync(Request, cancellation.Token));
    }

    private static StubHttpMessageHandler Responds(HttpStatusCode status, string content) => new()
    {
        Handler = (_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        })
    };

    private static AcquiringBankClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") });
}