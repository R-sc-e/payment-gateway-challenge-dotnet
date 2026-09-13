using System.Collections.Concurrent;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class RecordingPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public int Count => _payments.Count;

    public void Add(Payment payment) => _payments[payment.Id] = payment;

    public Payment? Get(Guid id) => _payments.GetValueOrDefault(id);
}