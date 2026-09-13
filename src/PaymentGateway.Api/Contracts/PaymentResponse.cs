using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Contracts;

public sealed record PaymentResponse(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount)
{
    internal static PaymentResponse From(Payment payment) => new(
        payment.Id,
        payment.Status,
        payment.CardNumberLastFour,
        payment.ExpiryMonth,
        payment.ExpiryYear,
        payment.Currency,
        payment.Amount);
}