using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Clients;

public sealed class BankPaymentRequest
{
    public BankPaymentRequest(
        string cardNumber,
        string expiryDate,
        string currency,
        int amount,
        string cvv)
    {
        CardNumber = cardNumber;
        ExpiryDate = expiryDate;
        Currency = currency;
        Amount = amount;
        Cvv = cvv;
    }

    [JsonPropertyName("card_number")]
    public string CardNumber { get; }

    [JsonPropertyName("expiry_date")]
    public string ExpiryDate { get; }

    public string Currency { get; }

    public int Amount { get; }

    public string Cvv { get; }
}