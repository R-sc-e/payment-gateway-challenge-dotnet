namespace PaymentGateway.Api.Clients;

public enum AcquiringBankFailure
{
    Unavailable,
    Timeout,
    InvalidResponse
}

public sealed class AcquiringBankException(
    AcquiringBankFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public AcquiringBankFailure Failure { get; } = failure;
}