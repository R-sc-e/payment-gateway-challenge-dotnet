using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace PaymentGateway.Api.Tests.Errors;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task DoesNotHandleCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(),
            CancellationToken.None);

        Assert.False(handled);
    }
}