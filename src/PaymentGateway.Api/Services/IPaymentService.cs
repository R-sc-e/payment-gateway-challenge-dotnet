using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    PaymentModel? Get(Guid id);

    Task<PaymentModel> ProcessAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken);
}