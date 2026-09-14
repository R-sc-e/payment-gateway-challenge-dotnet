using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Domain;

public sealed class PaymentModel
{
    public PaymentModel(
        Guid id,
        PaymentStatus status,
        string cardNumberLastFour,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount)
    {
        Id = id;
        Status = status;
        CardNumberLastFour = cardNumberLastFour;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; }

    public string CardNumberLastFour { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    public int Amount { get; }
}
