using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.Infrastructure;

public sealed class PaymentRepositoryTests
{
    [Fact]
    public async Task SupportsConcurrentWritesAndReads()
    {
        // Arrange
        var repository = new PaymentRepository();
        var payments = Enumerable.Range(0, 250)
            .Select(index => new PaymentModel(
                Guid.NewGuid(), PaymentStatus.Authorized, index.ToString("D4"), 7, 2031, "GBP", index + 1))
            .ToArray();
        var unknownId = Guid.NewGuid();

        // Act
        await Task.WhenAll(payments.Select(payment => Task.Run(() => repository.Add(payment))));
        var results = await Task.WhenAll(payments.Select(payment =>
            Task.Run(() => repository.Get(payment.Id))));
        var missingPayment = repository.Get(unknownId);

        // Assert
        for (var index = 0; index < payments.Length; index++)
        {
            var payment = payments[index];
            var result = results[index];

            Assert.NotNull(result);
            Assert.Equal(payment.Id, result.Id);
            Assert.Equal(payment.Status, result.Status);
            Assert.Equal(payment.CardNumberLastFour, result.CardNumberLastFour);
            Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
            Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
            Assert.Equal(payment.Currency, result.Currency);
            Assert.Equal(payment.Amount, result.Amount);
        }

        Assert.Equal(payments.Length, repository.Count);
        Assert.Null(missingPayment);
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        // Arrange
        var repository = new PaymentRepository();
        var payment = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Declined, "1234", 7, 2031, "USD", 100);
        repository.Add(payment);

        // Act
        var exception = Record.Exception(() => repository.Add(payment));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }
}
