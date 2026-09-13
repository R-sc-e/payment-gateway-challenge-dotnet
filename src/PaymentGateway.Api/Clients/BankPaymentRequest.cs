using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Clients;

public sealed record BankPaymentRequest(
    [property: JsonPropertyName("card_number")] string CardNumber,
    [property: JsonPropertyName("expiry_date")] string ExpiryDate,
    string Currency,
    int Amount,
    string Cvv);