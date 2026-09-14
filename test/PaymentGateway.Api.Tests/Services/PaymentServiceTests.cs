using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure;
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
        var repository = new PaymentRepository();
        var logger = new RecordingLogger<PaymentService>();
        var service = CreateService(bank, repository, logger);
        var request = TestRequests.Valid(cardNumber: "2222405343248877");

        var result = await service.ProcessAsync(request, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("8877", result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
        Assert.NotNull(repository.Get(result.Id));
        Assert.Equal(1, repository.Count);

        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Information);
        Assert.Equal(result.Id, entry.Properties["PaymentId"]);
        Assert.Equal(expectedStatus, entry.Properties["PaymentStatus"]);
        Assert.DoesNotContain(request.CardNumber!, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(request.Cvv!, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-code", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendsExactMappedRequestToBank()
    {
        var bank = new StubBankClient();
        var service = CreateService(bank, new PaymentRepository());
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

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("currency")]
    [InlineData("cvv")]
    public async Task RejectsMissingRequiredValuesAtServiceBoundary(string missingField)
    {
        var bank = new StubBankClient();
        var repository = new PaymentRepository();
        var service = CreateService(bank, repository);
        var request = TestRequests.Valid(
            cardNumber: missingField == "cardNumber" ? null : "2222405343248877",
            currency: missingField == "currency" ? null : "GBP",
            cvv: missingField == "cvv" ? null : "123");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ProcessAsync(request, CancellationToken.None));

        Assert.Equal(0, bank.CallCount);
        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public async Task DoesNotStorePaymentWhenBankFails()
    {
        const string cardNumber = "2222405343248877";
        const string cvv = "987";
        var bank = new StubBankClient
        {
            Handler = (_, _) => throw new AcquiringBankException(
                AcquiringBankFailure.Unavailable,
                "unavailable")
        };
        var repository = new PaymentRepository();
        var logger = new RecordingLogger<PaymentService>();
        var service = CreateService(bank, repository, logger);
        var request = TestRequests.Valid(cardNumber: cardNumber, cvv: cvv);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            service.ProcessAsync(request, CancellationToken.None));

        Assert.Equal(0, repository.Count);
        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Warning);
        Assert.Equal(AcquiringBankFailure.Unavailable, entry.Properties["BankFailure"]);
        AssertLogExcludesSensitiveValues(entry, cardNumber, cvv, "authorization-code");
    }

    [Fact]
    public async Task LogsInvalidBankResponseAsError()
    {
        const string cardNumber = "2222405343248877";
        const string cvv = "987";
        var bank = new StubBankClient
        {
            Handler = (_, _) => throw new AcquiringBankException(
                AcquiringBankFailure.InvalidResponse,
                "invalid response")
        };
        var logger = new RecordingLogger<PaymentService>();
        var service = CreateService(bank, new PaymentRepository(), logger);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            service.ProcessAsync(
                TestRequests.Valid(cardNumber: cardNumber, cvv: cvv),
                CancellationToken.None));

        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Error);
        AssertLogExcludesSensitiveValues(entry, cardNumber, cvv, "authorization-code");
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
        var repository = new PaymentRepository();
        var service = CreateService(bank, repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProcessAsync(TestRequests.Valid(), cancellation.Token));

        Assert.Equal(0, repository.Count);
    }

    [Fact]
    public void GetsExistingPaymentAndReturnsNullForUnknownId()
    {
        var repository = new PaymentRepository();
        var payment = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Authorized, "1234", 7, 2031, "GBP", 100);
        repository.Add(payment);
        var service = CreateService(new StubBankClient(), repository);

        var result = service.Get(payment.Id);

        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(payment.Status, result.Status);
        Assert.Equal(payment.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
        Assert.Equal(payment.Currency, result.Currency);
        Assert.Equal(payment.Amount, result.Amount);
        Assert.Null(service.Get(Guid.NewGuid()));
    }

    private static PaymentService CreateService(
        IAcquiringBankClient bank,
        IPaymentRepository repository,
        ILogger<PaymentService>? logger = null) =>
        new(bank, repository, logger ?? NullLogger<PaymentService>.Instance);

    private static void AssertLogExcludesSensitiveValues(RecordedLog entry, params string[] values)
    {
        var exception = entry.Exception?.ToString() ?? string.Empty;
        var properties = string.Join(',', entry.Properties.Select(property => property.Value));

        foreach (var value in values)
        {
            Assert.DoesNotContain(value, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(value, exception, StringComparison.Ordinal);
            Assert.DoesNotContain(value, properties, StringComparison.Ordinal);
        }
    }
}
