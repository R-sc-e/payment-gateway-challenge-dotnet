using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Moq;
using Moq.Protected;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Tests.Clients;

public sealed class AcquiringBankClientTests : IDisposable
{
    private const string AuthorizationCode = "authorization-code";

    private static readonly Uri BankBaseAddress = new("http://bank.test/");

    private static readonly BankPaymentRequest Request =
        new("2222405343248877", "04/2032", "GBP", 1050, "123");

    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly AcquiringBankClient _client;
    private HttpMethod? _lastMethod;
    private Uri? _lastUri;
    private string? _lastBody;

    public AcquiringBankClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = BankBaseAddress
        };
        _client = new AcquiringBankClient(_httpClient);
    }

    public static TheoryData<string> InvalidResponses => new()
    {
        string.Empty,
        "not-json",
        JsonSerializer.Serialize(new { }),
        JsonSerializer.Serialize(new { authorized = false }),
        JsonSerializer.Serialize(new { authorized = true, authorization_code = string.Empty })
    };

    [Theory]
    [InlineData(true, AuthorizationCode)]
    [InlineData(false, "")]
    public async Task ProcessPaymentAsync_ParsesSuccessfulResponse(
        bool authorized,
        string authorizationCode)
    {
        // Arrange
        RespondsWithJson(HttpStatusCode.OK, new
        {
            authorized,
            authorization_code = authorizationCode
        });

        // Act
        var response = await _client.ProcessPaymentAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(authorized, response.Authorized);
        Assert.Equal(authorizationCode, response.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessPaymentAsync_SendsExpectedMethodPathAndSnakeCaseJson()
    {
        // Arrange
        RespondsWithJson(HttpStatusCode.OK, new
        {
            authorized = true,
            authorization_code = AuthorizationCode
        });

        // Act
        await _client.ProcessPaymentAsync(Request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpMethod.Post, _lastMethod);
        Assert.Equal(new Uri(BankBaseAddress, "payments"), _lastUri);

        var actualBody = JsonNode.Parse(Assert.IsType<string>(_lastBody));
        var expectedBody = JsonSerializer.SerializeToNode(new
        {
            card_number = Request.CardNumber,
            expiry_date = Request.ExpiryDate,
            currency = Request.Currency,
            amount = Request.Amount,
            cvv = Request.Cvv
        });

        Assert.True(JsonNode.DeepEquals(expectedBody, actualBody));
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ProcessPaymentAsync_MapsNonSuccessResponseToUnavailable(HttpStatusCode status)
    {
        // Arrange
        RespondsWithJson(status, new { });

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            _client.ProcessPaymentAsync(Request, CancellationToken.None));

        // Assert
        Assert.Equal(AcquiringBankFailure.Unavailable, exception.Failure);
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task ProcessPaymentAsync_RejectsMalformedOrIncompleteResponse(string content)
    {
        // Arrange
        Responds(HttpStatusCode.OK, content);

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            _client.ProcessPaymentAsync(Request, CancellationToken.None));

        // Assert
        Assert.Equal(AcquiringBankFailure.InvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task ProcessPaymentAsync_MapsNetworkFailureToUnavailable()
    {
        // Arrange
        SetupHandler((_, _) => throw new HttpRequestException());

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            _client.ProcessPaymentAsync(Request, CancellationToken.None));

        // Assert
        Assert.Equal(AcquiringBankFailure.Unavailable, exception.Failure);
    }

    [Fact]
    public async Task ProcessPaymentAsync_MapsHttpClientTimeoutButNotCallerCancellation()
    {
        // Arrange
        SetupHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        _httpClient.Timeout = TimeSpan.FromMilliseconds(20);

        // Act
        var exception = await Assert.ThrowsAsync<AcquiringBankException>(() =>
            _client.ProcessPaymentAsync(Request, CancellationToken.None));

        // Assert
        Assert.Equal(AcquiringBankFailure.Timeout, exception.Failure);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.ProcessPaymentAsync(Request, cancellation.Token));
    }

    public void Dispose() => _httpClient.Dispose();

    private void Responds(HttpStatusCode status, string content) =>
        SetupHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json)
        }));

    private void RespondsWithJson<T>(HttpStatusCode status, T content) =>
        Responds(status, JsonSerializer.Serialize(content));

    private void SetupHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                _lastMethod = request.Method;
                _lastUri = request.RequestUri;
                _lastBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                return await handler(request, cancellationToken);
            });
    }
}
