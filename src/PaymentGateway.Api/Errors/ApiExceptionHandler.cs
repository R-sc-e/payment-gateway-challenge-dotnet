using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics;

using PaymentGateway.Api.Clients;

namespace PaymentGateway.Api;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var isBankFailure = exception is AcquiringBankException;
        var status = isBankFailure
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status500InternalServerError;
        var title = isBankFailure
            ? "The acquiring bank is unavailable"
            : "An unexpected error occurred";
        var type = isBankFailure
            ? "https://httpstatuses.com/502"
            : "https://httpstatuses.com/500";

        if (isBankFailure)
        {
            logger.LogWarning(exception, "Acquiring-bank request failed");
        }
        else
        {
            logger.LogError(exception, "Unhandled payment gateway exception");
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            ProblemResponses.Create(httpContext, status, title, type),
            cancellationToken: cancellationToken);
        return true;
    }
}