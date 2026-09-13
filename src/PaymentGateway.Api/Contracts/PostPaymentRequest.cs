namespace PaymentGateway.Api.Contracts;

public sealed record PostPaymentRequest(
    string? CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string? Currency,
    int Amount,
    string? Cvv);