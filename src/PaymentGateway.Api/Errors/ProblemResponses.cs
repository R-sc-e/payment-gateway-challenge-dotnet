using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api;

internal static class ProblemResponses
{
    public static ValidationProblemDetails Rejected(
        HttpContext context,
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Type = "https://httpstatuses.com/400",
            Title = "Payment request was rejected",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };
        problem.Extensions["paymentStatus"] = "Rejected";
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static ProblemDetails Create(HttpContext context, int status, string title, string type)
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }
}