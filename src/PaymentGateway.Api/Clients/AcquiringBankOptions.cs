using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Clients;

public sealed class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";

    [Required]
    public required Uri BaseUrl { get; init; }

    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 5;
}