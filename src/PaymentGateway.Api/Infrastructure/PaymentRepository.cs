using System.Collections.Concurrent;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentModel> _payments = new();

    public int Count => _payments.Count;

    public void Add(PaymentModel payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException($"Payment '{payment.Id}' already exists.");
        }
    }

    public PaymentModel? Get(Guid id) => _payments.GetValueOrDefault(id);
}
