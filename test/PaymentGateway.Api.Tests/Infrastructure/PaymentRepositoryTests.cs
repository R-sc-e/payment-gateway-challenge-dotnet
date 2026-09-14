using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.Infrastructure;

public sealed class PaymentRepositoryTests
{
    [Fact]
    public async Task SupportsConcurrentWritesAndReads()
    {
        var repository = new PaymentRepository();
        var payments = Enumerable.Range(0, 250)
            .Select(index => new PaymentModel(
                Guid.NewGuid(), PaymentStatus.Authorized, index.ToString("D4"), 7, 2031, "GBP", index + 1))
            .ToArray();

        await Task.WhenAll(payments.Select(payment => Task.Run(() => repository.Add(payment))));

        await Task.WhenAll(payments.Select(payment => Task.Run(() =>
        {
            var result = repository.Get(payment.Id);
            Assert.NotNull(result);
            Assert.Equal(payment.Id, result.Id);
            Assert.Equal(payment.Status, result.Status);
            Assert.Equal(payment.CardNumberLastFour, result.CardNumberLastFour);
            Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
            Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
            Assert.Equal(payment.Currency, result.Currency);
            Assert.Equal(payment.Amount, result.Amount);
        })));
        Assert.Equal(payments.Length, repository.Count);
        Assert.Null(repository.Get(Guid.NewGuid()));
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        var repository = new PaymentRepository();
        var payment = new PaymentModel(
            Guid.NewGuid(), PaymentStatus.Declined, "1234", 7, 2031, "USD", 100);
        repository.Add(payment);

        Assert.Throws<InvalidOperationException>(() => repository.Add(payment));
    }
}
