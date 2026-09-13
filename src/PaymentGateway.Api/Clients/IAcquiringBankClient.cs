namespace PaymentGateway.Api.Clients;

public interface IAcquiringBankClient
{
    Task<BankPaymentResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken);
}