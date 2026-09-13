namespace PaymentGateway.Api.Domain;

public interface IPaymentRepository
{
    void Add(Payment payment);

    Payment? Get(Guid id);
}