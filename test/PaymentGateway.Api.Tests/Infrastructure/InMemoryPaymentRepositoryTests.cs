using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure;

namespace PaymentGateway.Api.Tests.Infrastructure;

public sealed class InMemoryPaymentRepositoryTests
{
    [Fact]
    public async Task SupportsConcurrentWritesAndReads()
    {
        var repository = new InMemoryPaymentRepository();
        var payments = Enumerable.Range(0, 250)
            .Select(index => new Payment(
                Guid.NewGuid(), PaymentStatus.Authorized, index.ToString("D4"), 7, 2031, "GBP", index + 1))
            .ToArray();

        await Task.WhenAll(payments.Select(payment => Task.Run(() => repository.Add(payment))));

        await Task.WhenAll(payments.Select(payment => Task.Run(() =>
            Assert.Equal(payment, repository.Get(payment.Id)))));
        Assert.Null(repository.Get(Guid.NewGuid()));
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Declined, "1234", 7, 2031, "USD", 100);
        repository.Add(payment);

        Assert.Throws<InvalidOperationException>(() => repository.Add(payment));
    }
}