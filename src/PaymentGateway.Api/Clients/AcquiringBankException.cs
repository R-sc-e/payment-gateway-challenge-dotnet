using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Clients;

public sealed class AcquiringBankException(
    AcquiringBankFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public AcquiringBankFailure Failure { get; } = failure;
}