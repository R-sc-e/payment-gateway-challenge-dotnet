namespace PaymentGateway.Api.Contracts;

public sealed class PostPaymentRequest
{
    public PostPaymentRequest(
        string? cardNumber,
        int expiryMonth,
        int expiryYear,
        string? currency,
        int amount,
        string? cvv)
    {
        CardNumber = cardNumber;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
        Cvv = cvv;
    }

    public string? CardNumber { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string? Currency { get; }

    public int Amount { get; }

    public string? Cvv { get; }
}
