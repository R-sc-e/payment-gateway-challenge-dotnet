using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public sealed class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentsRepositoryMock;
    private readonly Mock<IAcquiringBankClient> _acquiringBankClientMock;
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        _paymentsRepositoryMock = new Mock<IPaymentRepository>();
        _acquiringBankClientMock = new Mock<IAcquiringBankClient>();
        _service = new PaymentService(
            _acquiringBankClientMock.Object,
            _paymentsRepositoryMock.Object,
            NullLogger<PaymentService>.Instance);
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessAsync_ProcessesAndStoresBankOutcome(
        bool authorized,
        PaymentStatus expectedStatus)
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", 1050, "123");
        PaymentModel? storedPayment = null;
        _acquiringBankClientMock
            .Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResponse(
                authorized,
                authorized ? "auth-code" : string.Empty));
        _paymentsRepositoryMock
            .Setup(repository => repository.Add(It.IsAny<PaymentModel>()))
            .Callback<PaymentModel>(payment => storedPayment = payment);

        // Act
        var result = await _service.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("8877", result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Same(result, storedPayment);
        _paymentsRepositoryMock.Verify(repository => repository.Add(result), Times.Once());

    }

    [Fact]
    public async Task ProcessAsync_SendsExactMappedRequestToBank()
    {
        // Arrange
        var request = new PostPaymentRequest(
            "1234567890123456", 4, 2032, "EUR", 42, "0123");
        BankPaymentRequest? sentRequest = null;
        _acquiringBankClientMock
            .Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<BankPaymentRequest, CancellationToken>((bankRequest, _) =>
                sentRequest = bankRequest)
            .ReturnsAsync(new BankPaymentResponse(true, "auth-code"));

        // Act
        await _service.ProcessAsync(request, CancellationToken.None);

        // Assert
        var sent = Assert.IsType<BankPaymentRequest>(sentRequest);
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
    public async Task ProcessAsync_RejectsMissingRequiredValuesAtServiceBoundary(string missingField)
    {
        // Arrange
        var request = new PostPaymentRequest(
            missingField == "cardNumber" ? null : "2222405343248877",
            7,
            2031,
            missingField == "currency" ? null : "GBP",
            1050,
            missingField == "cvv" ? null : "123");

        // Act
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ProcessAsync(request, CancellationToken.None));

        // Assert
        _acquiringBankClientMock.Verify(
            client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
        _paymentsRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<PaymentModel>()),
            Times.Never());
    }

    [Fact]
    public async Task ProcessAsync_DoesNotStorePaymentWhenBankFails()
    {
        // Arrange
        var request = new PostPaymentRequest(
            "2222405343248877", 7, 2031, "GBP", 1050, "987");
        _acquiringBankClientMock
            .Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcquiringBankException(
                AcquiringBankFailure.Unavailable,
                "unavailable"));

        // Act
        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            _service.ProcessAsync(request, CancellationToken.None));

        // Assert
        _paymentsRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<PaymentModel>()),
            Times.Never());
    }

    [Fact]
    public async Task ProcessAsync_PropagatesCallerCancellationAndDoesNotStorePayment()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _acquiringBankClientMock
            .Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns((BankPaymentRequest _, CancellationToken cancellationToken) =>
                Task.FromCanceled<BankPaymentResponse>(cancellationToken));

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessAsync(
                new PostPaymentRequest(
                    "2222405343248877", 7, 2031, "GBP", 1050, "123"),
                cancellation.Token));

        // Assert
        _paymentsRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<PaymentModel>()),
            Times.Never());
    }

    [Fact]
    public void Get_ReturnsExistingPaymentAndNullForUnknownId()
    {
        // Arrange
        var payment = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Authorized, "1234", 7, 2031, "GBP", 100);
        var unknownId = Guid.NewGuid();
        _paymentsRepositoryMock
            .Setup(repository => repository.Get(payment.Id))
            .Returns(payment);

        // Act
        var result = _service.Get(payment.Id);
        var missing = _service.Get(unknownId);

        // Assert
        Assert.Same(payment, result);
        Assert.Null(missing);
        _paymentsRepositoryMock.Verify(repository => repository.Get(payment.Id), Times.Once());
        _paymentsRepositoryMock.Verify(repository => repository.Get(unknownId), Times.Once());
    }

}
