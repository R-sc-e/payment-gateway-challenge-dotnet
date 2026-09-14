namespace PaymentGateway.Api.Domain;

public interface IPaymentRepository
{
    void Add(PaymentModel payment);

    PaymentModel? Get(Guid id);
}