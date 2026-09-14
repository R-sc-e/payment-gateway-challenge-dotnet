using System.Net;

using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Tests.HttpMocks.AcquiringBank;

public static class Payment
{
    public static HttpMock Authorized(PostPaymentRequest request) =>
        Successful(request, true);

    public static HttpMock Declined(PostPaymentRequest request) =>
        Successful(request, false);

    public static HttpMock Failed(HttpStatusCode statusCode) => new(
        HttpMethod.Post.Method,
        "/payments",
        statusCode);

    public static HttpMock InvalidResponse() => new(
        HttpMethod.Post.Method,
        "/payments",
        HttpStatusCode.OK,
        new
        {
            authorized = true,
            authorization_code = string.Empty
        });

    private static HttpMock Successful(PostPaymentRequest request, bool authorized) => new(
        HttpMethod.Post.Method,
        "/payments",
        HttpStatusCode.OK,
        new
        {
            authorized,
            authorization_code = authorized ? "opaque-authorization-code" : string.Empty
        },
        new
        {
            card_number = request.CardNumber,
            expiry_date = $"{request.ExpiryMonth:D2}/{request.ExpiryYear:D4}",
            currency = request.Currency,
            amount = request.Amount,
            cvv = request.Cvv
        });
}
