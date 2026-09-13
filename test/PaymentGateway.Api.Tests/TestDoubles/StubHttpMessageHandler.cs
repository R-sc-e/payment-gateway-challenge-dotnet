using System.Net;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public HttpMethod? LastMethod { get; private set; }

    public Uri? LastUri { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastUri = request.RequestUri;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return await Handler(request, cancellationToken);
    }
}