using PaymentGateway.Api.Clients;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class StubBankClient : IAcquiringBankClient
{
    public Func<BankPaymentRequest, CancellationToken, Task<BankPaymentResponse>> Handler { get; set; } =
        (_, _) => Task.FromResult(new BankPaymentResponse(true, "authorization-code"));

    public int CallCount { get; private set; }

    public BankPaymentRequest? LastRequest { get; private set; }

    public Task<BankPaymentResponse> ProcessPaymentAsync(
        BankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return Handler(request, cancellationToken);
    }
}