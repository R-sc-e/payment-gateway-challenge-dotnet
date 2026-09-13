using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Clients;

public sealed record BankPaymentResponse(
    bool? Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);