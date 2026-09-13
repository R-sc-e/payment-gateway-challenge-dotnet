using System.Diagnostics;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService(
    IAcquiringBankClient bankClient,
    IPaymentRepository repository,
    PaymentTelemetry telemetry,
    ILogger<PaymentService> logger)
{
    public PaymentResponse? Get(Guid id)
    {
        var payment = repository.Get(id);
        return payment is null ? null : PaymentResponse.From(payment);
    }

    public async Task<PaymentResponse> ProcessAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = telemetry.StartProcessing();
        var started = Stopwatch.GetTimestamp();
        var bankRequest = new BankPaymentRequest(
            request.CardNumber!,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear:D4}",
            request.Currency!,
            request.Amount,
            request.Cvv!);

        BankPaymentResponse bankResponse;
        try
        {
            bankResponse = await bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);
        }
        catch (AcquiringBankException exception)
        {
            telemetry.RecordBankFailure(exception.Failure, Stopwatch.GetElapsedTime(started));
            activity?.SetStatus(ActivityStatusCode.Error, exception.Failure.ToString());
            throw;
        }

        var status = bankResponse.Authorized!.Value
            ? PaymentStatus.Authorized
            : PaymentStatus.Declined;
        var payment = new Payment(
            Guid.NewGuid(),
            status,
            request.CardNumber![^4..],
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency!,
            request.Amount);

        repository.Add(payment);
        var elapsed = Stopwatch.GetElapsedTime(started);
        telemetry.RecordProcessed(status, elapsed);
        activity?.SetTag("payment.status", status.ToString());

        logger.LogInformation(
            "Payment {PaymentId} processed with status {PaymentStatus} in {ElapsedMilliseconds} ms",
            payment.Id,
            status,
            elapsed.TotalMilliseconds);

        return PaymentResponse.From(payment);
    }
}