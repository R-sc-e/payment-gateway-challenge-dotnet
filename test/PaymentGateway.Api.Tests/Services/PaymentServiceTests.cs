using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Services;

public sealed class PaymentServiceTests
{
    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessesAndStoresBankOutcome(bool authorized, PaymentStatus expectedStatus)
    {
        var bank = new StubBankClient
        {
            Handler = (_, _) => Task.FromResult(new BankPaymentResponse(
                authorized,
                authorized ? "auth-code" : string.Empty))
        };
        var repository = new RecordingPaymentRepository();
        using var telemetry = new PaymentTelemetry();
        var service = CreateService(bank, repository, telemetry);
        var request = TestRequests.Valid(cardNumber: "2222405343248877");

        var response = await service.ProcessAsync(request, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(expectedStatus, response.Status);
        Assert.Equal("8877", response.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, response.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, response.ExpiryYear);
        Assert.Equal(request.Currency, response.Currency);
        Assert.Equal(request.Amount, response.Amount);
        Assert.NotNull(repository.Get(response.Id));
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task SendsExactMappedRequestToBank()
    {
        var bank = new StubBankClient();
        using var telemetry = new PaymentTelemetry();
        var service = CreateService(bank, new RecordingPaymentRepository(), telemetry);
        var request = TestRequests.Valid(
            cardNumber: "1234567890123456",
            expiryMonth: 4,
            expiryYear: 2032,
            currency: "EUR",
            amount: 42,
            cvv: "0123");

        await service.ProcessAsync(request, CancellationToken.None);

        var sent = Assert.IsType<BankPaymentRequest>(bank.LastRequest);
        Assert.Equal("1234567890123456", sent.CardNumber);
        Assert.Equal("04/2032", sent.ExpiryDate);
        Assert.Equal("EUR", sent.Currency);
        Assert.Equal(42, sent.Amount);
        Assert.Equal("0123", sent.Cvv);
    }

    [Fact]
    public async Task DoesNotStorePaymentWhenBankFails()
    {
        var bank = new StubBankClient
        {
            Handler = (_, _) => throw new AcquiringBankException(
                AcquiringBankFailure.Unavailable,
                "unavailable")
        };
        var repository = new RecordingPaymentRepository();
        using var telemetry = new PaymentTelemetry();
        var service = CreateService(bank, repository, telemetry);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            service.ProcessAsync(TestRequests.Valid(), CancellationToken.None));

        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public async Task PropagatesCallerCancellationAndDoesNotStorePayment()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var bank = new StubBankClient
        {
            Handler = (_, token) => Task.FromCanceled<BankPaymentResponse>(token)
        };
        var repository = new RecordingPaymentRepository();
        using var telemetry = new PaymentTelemetry();
        var service = CreateService(bank, repository, telemetry);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProcessAsync(TestRequests.Valid(), cancellation.Token));

        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void GetsExistingPaymentAndReturnsNullForUnknownId()
    {
        var repository = new RecordingPaymentRepository();
        var payment = new Domain.Payment(
            Guid.NewGuid(), PaymentStatus.Authorized, "1234", 7, 2031, "GBP", 100);
        repository.Add(payment);
        using var telemetry = new PaymentTelemetry();
        var service = CreateService(new StubBankClient(), repository, telemetry);

        Assert.Equal(payment.Id, service.Get(payment.Id)?.Id);
        Assert.Null(service.Get(Guid.NewGuid()));
    }

    private static PaymentService CreateService(
        IAcquiringBankClient bank,
        Domain.IPaymentRepository repository,
        PaymentTelemetry telemetry) =>
        new(bank, repository, telemetry, NullLogger<PaymentService>.Instance);
}