using System.Net;

namespace PaymentGateway.Api.Tests.HttpMocks;

public sealed record HttpMock(
    string HttpMethod,
    string RequestPath,
    HttpStatusCode ResponseCode,
    object? ResponseBody = null,
    object? RequestBody = null);
