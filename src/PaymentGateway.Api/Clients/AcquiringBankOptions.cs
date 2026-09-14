using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Clients;

public sealed class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";
    public required Uri BaseUrl { get; init; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;
}
