using System.Diagnostics;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService(
    IAcquiringBankClient bankClient,
    IPaymentRepository repository,
    ILogger<PaymentService> logger) : IPaymentService
{
    public PaymentModel? Get(Guid id) => repository.Get(id);

    public async Task<PaymentModel> ProcessAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var cardNumber = request.CardNumber ??
            throw new ArgumentException("Card number cannot be null.", nameof(request));
        var currency = request.Currency ??
            throw new ArgumentException("Currency cannot be null.", nameof(request));
        var cvv = request.Cvv ??
            throw new ArgumentException("CVV cannot be null.", nameof(request));

        logger.LogDebug("Processing payment request");
        var started = Stopwatch.GetTimestamp();
        var bankRequest = new BankPaymentRequest(
            cardNumber,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear:D4}",
            currency,
            request.Amount,
            cvv);

        BankPaymentResponse bankResponse;
        try
        {
            bankResponse = await bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);
        }
        catch (AcquiringBankException exception)
        {
            var failureElapsed = Stopwatch.GetElapsedTime(started);
            if (exception.Failure is AcquiringBankFailure.InvalidResponse)
            {
                logger.LogError(
                    exception,
                    "Acquiring bank returned an invalid response after {ElapsedMilliseconds} ms",
                    failureElapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Acquiring-bank request failed with {BankFailure} after {ElapsedMilliseconds} ms",
                    exception.Failure,
                    failureElapsed.TotalMilliseconds);
            }

            throw;
        }

        var status = bankResponse.Authorized!.Value ? PaymentStatus.Authorized : PaymentStatus.Declined;
        var payment = new PaymentModel(
            Guid.NewGuid(),
            status,
            cardNumber[^4..],
            request.ExpiryMonth,
            request.ExpiryYear,
            currency,
            request.Amount);

        repository.Add(payment);
        var elapsed = Stopwatch.GetElapsedTime(started);
        logger.LogInformation(
            "Payment {PaymentId} processed with status {PaymentStatus} in {ElapsedMilliseconds} ms",
            payment.Id,
            status,
            elapsed.TotalMilliseconds);

        return payment;
    }
}
