using System.Text.Json;

using PaymentGateway.Api.Tests.HttpMocks;

using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PaymentGateway.Api.Tests.TestInfrastructure;

public sealed class HttpMockServer : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public string Url => _server.Url ??
        throw new InvalidOperationException("The HTTP mock server has not started.");

    public void AddMock(HttpMock mock)
    {
        var request = Request.Create()
            .UsingMethod(mock.HttpMethod)
            .WithPath(mock.RequestPath);

        if (mock.RequestBody is not null)
        {
            request.WithBody(new JsonPartialMatcher(JsonSerializer.Serialize(mock.RequestBody)));
        }

        var response = Response.Create()
            .WithStatusCode(mock.ResponseCode);

        if (mock.ResponseBody is not null)
        {
            response.WithBodyAsJson(mock.ResponseBody);
        }

        _server
            .Given(request)
            .RespondWith(response);
    }

    public int GetRequestCount(string httpMethod, string requestPath) =>
        _server.FindLogEntries(Request.Create()
                .UsingMethod(httpMethod)
                .WithPath(requestPath))
            .Count();

    public void Reset() => _server.Reset();

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
