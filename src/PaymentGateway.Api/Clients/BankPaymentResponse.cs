using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Clients;

public sealed class BankPaymentResponse
{
    public BankPaymentResponse(bool? authorized, string? authorizationCode)
    {
        Authorized = authorized;
        AuthorizationCode = authorizationCode;
    }

    public bool? Authorized { get; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; }
}