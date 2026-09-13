using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Domain;

public sealed record Payment(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount);