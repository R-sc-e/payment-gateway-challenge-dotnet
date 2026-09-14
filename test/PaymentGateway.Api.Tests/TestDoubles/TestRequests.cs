using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal static class TestRequests
{
    public static PostPaymentRequest Valid(
        string? cardNumber = "2222405343248877",
        int expiryMonth = 7,
        int expiryYear = 2031,
        string? currency = "GBP",
        int amount = 1050,
        string? cvv = "123") =>
        new(cardNumber, expiryMonth, expiryYear, currency, amount, cvv);
}
