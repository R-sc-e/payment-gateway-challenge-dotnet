using System.Diagnostics;
using System.Diagnostics.Metrics;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Contracts;

namespace PaymentGateway.Api.Observability;

public sealed class PaymentTelemetry : IDisposable
{
    public const string ServiceName = "payment-gateway";
    public const string ActivitySourceName = "PaymentGateway.Payments";
    public const string MeterName = "PaymentGateway.Payments";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _processed;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _bankFailures;
    private readonly Histogram<double> _processingDuration;

    public PaymentTelemetry()
    {
        _processed = _meter.CreateCounter<long>("payment_gateway.payments.processed");
        _rejected = _meter.CreateCounter<long>("payment_gateway.payments.rejected");
        _bankFailures = _meter.CreateCounter<long>("payment_gateway.bank.failures");
        _processingDuration = _meter.CreateHistogram<double>(
            "payment_gateway.payments.duration",
            unit: "ms");
    }

    public Activity? StartProcessing() => _activitySource.StartActivity("process payment");

    public void RecordProcessed(PaymentStatus status, TimeSpan elapsed)
    {
        var tags = new TagList { { "payment.status", status.ToString() } };
        _processed.Add(1, tags);
        _processingDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void RecordRejected() => _rejected.Add(1);

    public void RecordBankFailure(AcquiringBankFailure failure, TimeSpan elapsed)
    {
        var tags = new TagList { { "bank.failure", failure.ToString() } };
        _bankFailures.Add(1, tags);
        _processingDuration.Record(elapsed.TotalMilliseconds, tags);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}