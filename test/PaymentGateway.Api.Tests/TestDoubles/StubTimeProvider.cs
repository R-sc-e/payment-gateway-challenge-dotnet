namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}